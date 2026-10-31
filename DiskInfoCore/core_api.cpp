// Wraps CAtaSmart behind the C ABI in core_api.h. No existing file is changed.
// Every export is read-only on the disks and keeps C++/MFC exceptions inside.

#include <afxwin.h>
#include <afxtempl.h>
#include <vector>

#include "core_api.h"
#include "engine.h"

struct di_handle
{
	uint32_t flags = 0;
	std::vector<di_disk_info_t> disks; // plain copies, so no CString leaves this file
};

class CDiskInfoCoreApp : public CWinApp {}; // a static-MFC DLL needs an app object
static CDiskInfoCoreApp theApp;

namespace
{
	// CAtaSmart marks "no value" with -1000 for temperature and -1 for life/hours
	// (AtaSmart.cpp:2741, 2745, 2736); 0 is a real reading.
	int32_t Temperature(int v) { return v == -1000 ? DI_NA : v; }
	int32_t Life(int v)        { return v < 0 ? DI_NA : v; }
	int64_t Hours(int v)       { return v < 0 ? DI_NA64 : v; }

	void Fill(const CAtaSmart::ATA_SMART_INFO& a, di_disk_info_t& d)
	{
		memset(&d, 0, sizeof(d));
		d.struct_size = sizeof(d);
		d.physical_drive_id = a.PhysicalDriveId;
		dicore::CopyUtf8(a.Model, d.model, sizeof(d.model));
		dicore::CopyUtf8(a.SerialNumber, d.serial, sizeof(d.serial));
		dicore::CopyUtf8(a.FirmwareRev, d.firmware, sizeof(d.firmware));

		int ifk = static_cast<int>(a.InterfaceType);
		d.interface_kind = (ifk >= DI_IF_UNKNOWN && ifk <= DI_IF_NVME) ? ifk : DI_IF_OTHER;
		// IsSsd is only meaningful once SMART/identify data was read; an unread disk reports HDD here.
		d.media = a.IsSsd ? DI_MEDIA_SSD : DI_MEDIA_HDD;
		d.temperature_c = Temperature(a.Temperature);
		d.health = (a.DiskStatus <= CAtaSmart::DISK_STATUS_BAD) ? static_cast<int32_t>(a.DiskStatus)
		                                                         : DI_HEALTH_UNKNOWN;
		d.life_percent = Life(a.Life);
		d.power_on_hours = Hours(a.DetectedPowerOnHours);
		// AddDiskNVMe sets IsSmartCorrect before it tries to read SMART (AtaSmart.cpp:3844), so the
		// flag alone says nothing about a failed read; require some data to have come back too.
		bool gotData = a.AttributeCount > 0 || a.Temperature != -1000;
		d.access = (a.IsSmartCorrect && !a.IsCheckSumError && gotData) ? DI_ACCESS_FULL : DI_ACCESS_PARTIAL;
		d.source = static_cast<uint32_t>(a.CommandType);
	}
}

extern "C" {

uint32_t di_abi_version(void)
{
	return (static_cast<uint32_t>(DI_ABI_MAJOR) << 16) | DI_ABI_MINOR;
}

int di_open(di_handle** out, uint32_t flags)
{
	if (out == nullptr)
	{
		return DI_E_ARG;
	}
	*out = nullptr;
	try
	{
		di_handle* h = new di_handle();
		h->flags = flags;
		*out = h;
		return DI_OK;
	}
	catch (...)
	{
		return DI_E_IO;
	}
}

void di_close(di_handle* h)
{
	try { delete h; } catch (...) {}
}

int di_scan(di_handle* h)
{
	if (h == nullptr)
	{
		return DI_E_ARG;
	}
	try
	{
		dicore::ComScope com;
		if (!com.Usable())
		{
			return DI_E_IO;
		}

		CAtaSmart ata; // fresh each scan, so no state carries over (Init compares against earlier vars)
		dicore::Scan(ata, !(h->flags & DI_OPEN_NO_WMI));

		std::vector<di_disk_info_t> next;
		next.reserve(static_cast<size_t>(ata.vars.GetCount()));
		for (INT_PTR i = 0; i < ata.vars.GetCount(); ++i)
		{
			di_disk_info_t d;
			Fill(ata.vars[i], d);
			next.push_back(d);
		}
		h->disks.swap(next);
		// Measured 2026-10-12: without elevation CAtaSmart finds no disk at all
		// (0 disks in 2 ms). Say so instead of looking like an empty machine.
		if (h->disks.empty() && !dicore::IsElevated())
		{
			return DI_E_ACCESS;
		}
		return DI_OK;
	}
	catch (CException* e)
	{
		e->Delete();
		return DI_E_IO;
	}
	catch (...)
	{
		return DI_E_IO;
	}
}

int di_disk_count(const di_handle* h)
{
	if (h == nullptr)
	{
		return DI_E_ARG;
	}
	return static_cast<int>(h->disks.size());
}

int di_disk_info(const di_handle* h, int idx, di_disk_info_t* out)
{
	if (h == nullptr || out == nullptr)
	{
		return DI_E_ARG;
	}
	// A caller built against a later, longer struct is fine: fill the v1 part and leave the rest.
	// Only a caller whose struct is shorter than v1 cannot be served.
	if (out->struct_size < sizeof(di_disk_info_t))
	{
		return DI_E_STRUCT_SIZE;
	}
	if (idx < 0 || static_cast<size_t>(idx) >= h->disks.size())
	{
		return DI_E_NOT_FOUND;
	}
	*out = h->disks[static_cast<size_t>(idx)]; // sets struct_size to the size actually filled
	return DI_OK;
}

} // extern "C"
