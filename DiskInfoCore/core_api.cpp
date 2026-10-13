// Wraps CAtaSmart behind the C ABI in core_api.h. No existing file is changed.
// Every export is read-only on the disks and keeps C++/MFC exceptions inside.

#include <afxwin.h>
#include <afxtempl.h>
#include <objbase.h>

#include <string>
#include <vector>

// AtaSmart.h declares JMicron types only if this flag is set, and the flag
// comes from CompileOptions.h (which the GUI gets through stdafx.h).
#include "CompileOptions.h"
#include "AtaSmart.h"
#include "core_api.h"

struct di_handle
{
	uint32_t flags = 0;
	std::vector<di_disk_info_t> disks; // plain copies, so no CString leaves this file
};

class CDiskInfoCoreApp : public CWinApp {}; // a static-MFC DLL needs an app object
static CDiskInfoCoreApp theApp;

// CAtaSmart logs through DebugPrint(), and the original (Priscilla/UtilityFx.cpp)
// deletes "<host exe>.log" on its first call, before it checks the debug mode.
// A library must not delete files next to its host, so DiskInfoCore.vcxproj
// compiles UtilityFx.cpp with DebugPrint renamed away and this no-op takes its place.
void DebugPrint(CString) {}

namespace
{
	// UTF-8 into a fixed buffer, always NUL-terminated, cut on a character boundary.
	void CopyUtf8(const CString& src, char* dst, size_t cap)
	{
		dst[0] = '\0';
		if (src.IsEmpty() || cap < 2)
		{
			return;
		}
		int need = WideCharToMultiByte(CP_UTF8, 0, src, -1, nullptr, 0, nullptr, nullptr);
		if (need <= 1)
		{
			return;
		}
		std::string tmp(static_cast<size_t>(need), '\0');
		WideCharToMultiByte(CP_UTF8, 0, src, -1, &tmp[0], need, nullptr, nullptr);
		tmp.resize(static_cast<size_t>(need) - 1); // drop the NUL

		size_t n = tmp.size();
		if (n > cap - 1)
		{
			n = cap - 1;
			while (n > 0 && (static_cast<unsigned char>(tmp[n]) & 0xC0) == 0x80)
			{
				--n; // do not split a multi-byte sequence
			}
		}
		memcpy(dst, tmp.data(), n);
		dst[n] = '\0';
	}

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
		CopyUtf8(a.Model, d.model, sizeof(d.model));
		CopyUtf8(a.SerialNumber, d.serial, sizeof(d.serial));
		CopyUtf8(a.FirmwareRev, d.firmware, sizeof(d.firmware));

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

	bool IsElevated()
	{
		HANDLE token = nullptr;
		if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
		{
			return false;
		}
		TOKEN_ELEVATION e = {};
		DWORD got = 0;
		BOOL ok = GetTokenInformation(token, TokenElevation, &e, sizeof(e), &got);
		CloseHandle(token);
		return ok && e.TokenIsElevated;
	}

	// Balanced COM init for the calling thread. Init() does not do this itself
	// (AtaSmart.cpp:836 is commented out) but needs COM for WMI.
	struct ComScope
	{
		bool uninit = false;
		HRESULT hr = S_OK;
		ComScope()
		{
			hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
			uninit = SUCCEEDED(hr);                    // S_OK and S_FALSE both need CoUninitialize
		}
		~ComScope() { if (uninit) CoUninitialize(); }
		// RPC_E_CHANGED_MODE: the thread already has COM in another mode; use it as is.
		bool Usable() const { return SUCCEEDED(hr) || hr == RPC_E_CHANGED_MODE; }
	};
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
		ComScope com;
		if (!com.Usable())
		{
			return DI_E_IO;
		}
		BOOL changed = FALSE;
		CAtaSmart ata; // fresh each scan, so no state carries over (Init compares against earlier vars)

		// The GUI switches these bridge/RAID probes on by default (DiskInfoDlgInit.cpp:167-216,
		// DiskInfoDlg.cpp:238) and CAtaSmart leaves them FALSE. Without them USB bridges are
		// skipped entirely. Same defaults as the GUI; the ones the GUI ships off stay off.
		ata.FlagUsbSat = TRUE;
		ata.FlagUsbIodata = TRUE;
		ata.FlagUsbSunplus = TRUE;
		ata.FlagUsbLogitec = TRUE;
		ata.FlagUsbProlific = TRUE;
		ata.FlagUsbJmicron = TRUE;
		ata.FlagUsbCypress = TRUE;
		ata.FlagUsbNVMeJMicron = TRUE;
		ata.FlagUsbNVMeASMedia = TRUE;
		ata.FlagUsbNVMeRealtek = TRUE;
		ata.FlagAMD_RC2 = TRUE;
		ata.FlagMegaRAID = TRUE;
		ata.FlagIntelVROC = TRUE;
		ata.CsmiType = CAtaSmart::CSMI_TYPE_ENABLE_AUTO;
		ata.Init((h->flags & DI_OPEN_NO_WMI) ? FALSE : TRUE, // useWmi
		         TRUE,  // advancedDiskSearch
		         &changed,
		         FALSE, FALSE, // workaroundHD204UI, workaroundAdataSsd
		         FALSE,        // flagHideNoSmartDisk: report disks even without SMART
		         FALSE,        // flagSortDriveLetter
		         FALSE);       // flagHideRAIDVolume

		std::vector<di_disk_info_t> next;
		next.reserve(static_cast<size_t>(ata.vars.GetCount()));
		for (INT_PTR i = 0; i < ata.vars.GetCount(); ++i)
		{
			// Init() leaves DiskStatus at 0; the GUI computes it itself right after
			// (DiskInfoDlgInit.cpp:446), so the DLL must too.
			// The caution thresholds also come from the GUI's INI (defaults 1, 1, 1, 10,
			// DiskInfoDlgInit.cpp:441-444); at 0 an HDD with reallocated sectors reads Good.
			ata.vars[i].Threshold05 = 1;
			ata.vars[i].ThresholdC5 = 1;
			ata.vars[i].ThresholdC6 = 1;
			ata.vars[i].ThresholdFF = 10;
			ata.vars[i].DiskStatus = ata.CheckDiskStatus(static_cast<DWORD>(i));
			di_disk_info_t d;
			Fill(ata.vars[i], d);
			next.push_back(d);
		}
		h->disks.swap(next);
		// Measured 2026-10-12: without elevation CAtaSmart finds no disk at all
		// (0 disks in 2 ms). Say so instead of looking like an empty machine.
		if (h->disks.empty() && !IsElevated())
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
