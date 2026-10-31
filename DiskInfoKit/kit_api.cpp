// Wraps CAtaSmart behind the portable C API in diskinfokit.h.
// Every export is read-only on the disks and keeps C++/MFC exceptions inside.

#include <afxwin.h>
#include <afxtempl.h>
#include <cstddef>
#include <vector>

#include "diskinfokit.h"
#include "engine.h"

static_assert(sizeof(dik_disk_t) == 496, "dik_disk_t size mismatch");
static_assert(offsetof(dik_disk_t, struct_size) == 0, "struct_size offset");
static_assert(offsetof(dik_disk_t, physical_drive_id) == 4, "physical_drive_id offset");
static_assert(offsetof(dik_disk_t, model) == 8, "model offset");
static_assert(offsetof(dik_disk_t, serial) == 104, "serial offset");
static_assert(offsetof(dik_disk_t, firmware) == 152, "firmware offset");
static_assert(offsetof(dik_disk_t, interface_name) == 176, "interface_name offset");
static_assert(offsetof(dik_disk_t, transfer_current) == 208, "transfer_current offset");
static_assert(offsetof(dik_disk_t, transfer_max) == 240, "transfer_max offset");
static_assert(offsetof(dik_disk_t, standard) == 272, "standard offset");
static_assert(offsetof(dik_disk_t, drive_letters) == 336, "drive_letters offset");
static_assert(offsetof(dik_disk_t, smart_key) == 368, "smart_key offset");
static_assert(offsetof(dik_disk_t, interface_kind) == 400, "interface_kind offset");
static_assert(offsetof(dik_disk_t, media) == 404, "media offset");
static_assert(offsetof(dik_disk_t, health) == 408, "health offset");
static_assert(offsetof(dik_disk_t, temperature_c) == 412, "temperature_c offset");
static_assert(offsetof(dik_disk_t, life_percent) == 416, "life_percent offset");
static_assert(offsetof(dik_disk_t, rotation_rpm) == 420, "rotation_rpm offset");
static_assert(offsetof(dik_disk_t, power_on_hours) == 424, "power_on_hours offset");
static_assert(offsetof(dik_disk_t, power_on_count) == 432, "power_on_count offset");
static_assert(offsetof(dik_disk_t, capacity_mb) == 440, "capacity_mb offset");
static_assert(offsetof(dik_disk_t, capacity_bytes) == 448, "capacity_bytes offset");
static_assert(offsetof(dik_disk_t, host_reads_gb) == 456, "host_reads_gb offset");
static_assert(offsetof(dik_disk_t, host_writes_gb) == 464, "host_writes_gb offset");
static_assert(offsetof(dik_disk_t, nand_writes_gb) == 472, "nand_writes_gb offset");
static_assert(offsetof(dik_disk_t, features) == 480, "features offset");
static_assert(offsetof(dik_disk_t, access) == 484, "access offset");
static_assert(offsetof(dik_disk_t, source) == 488, "source offset");
static_assert(offsetof(dik_disk_t, attribute_count) == 492, "attribute_count offset");

static_assert(sizeof(dik_attribute_t) == 48, "dik_attribute_t size mismatch");
static_assert(offsetof(dik_attribute_t, struct_size) == 0, "struct_size offset");
static_assert(offsetof(dik_attribute_t, id) == 4, "id offset");
static_assert(offsetof(dik_attribute_t, raw_len) == 5, "raw_len offset");
static_assert(offsetof(dik_attribute_t, reserved) == 6, "reserved offset");
static_assert(offsetof(dik_attribute_t, current) == 8, "current offset");
static_assert(offsetof(dik_attribute_t, worst) == 12, "worst offset");
static_assert(offsetof(dik_attribute_t, threshold) == 16, "threshold offset");
static_assert(offsetof(dik_attribute_t, raw) == 20, "raw offset");
static_assert(offsetof(dik_attribute_t, reserved2) == 28, "reserved2 offset");
static_assert(offsetof(dik_attribute_t, raw_value) == 32, "raw_value offset");
static_assert(offsetof(dik_attribute_t, status) == 40, "status offset");
static_assert(offsetof(dik_attribute_t, reserved3) == 44, "reserved3 offset");

struct dik_handle
{
	uint32_t flags = 0;
	std::vector<dik_disk_t> disks;
	std::vector<std::vector<dik_attribute_t>> attributes;
};

class CDiskInfoKitApp : public CWinApp {}; // a static-MFC DLL needs an app object
static CDiskInfoKitApp theApp;

namespace
{
	// CAtaSmart marks "no value" with -1000 for temperature and -1 for life/hours
	// (AtaSmart.cpp:2741, 2745, 2736); 0 is a real reading.
	int32_t Temperature(int v) { return v == -1000 ? DIK_NA : v; }
	int32_t Life(int v)        { return v < 0 ? DIK_NA : v; }
	int64_t Hours(int v)       { return v < 0 ? DIK_NA64 : v; }

	void FillDisk(const CAtaSmart& ata, int diskIdx, dik_disk_t& d, std::vector<dik_attribute_t>& attrs)
	{
		const CAtaSmart::ATA_SMART_INFO& a = ata.vars[diskIdx];
		memset(&d, 0, sizeof(d));
		d.struct_size = sizeof(d);
		d.physical_drive_id = a.PhysicalDriveId;

		dicore::CopyUtf8(a.Model, d.model, sizeof(d.model));
		dicore::CopyUtf8(a.SerialNumber, d.serial, sizeof(d.serial));
		dicore::CopyUtf8(a.FirmwareRev, d.firmware, sizeof(d.firmware));
		dicore::CopyUtf8(a.Interface, d.interface_name, sizeof(d.interface_name));
		dicore::CopyUtf8(a.CurrentTransferMode, d.transfer_current, sizeof(d.transfer_current));
		dicore::CopyUtf8(a.MaxTransferMode, d.transfer_max, sizeof(d.transfer_max));

		// Mirror DiskInfoDlgUpdate.cpp:1557-1564 (m_AtaAtapi)
		CString standard = a.MajorVersion;
		if (!a.MinorVersion.IsEmpty())
		{
			standard += _T(" | ") + a.MinorVersion;
		}
		dicore::CopyUtf8(standard, d.standard, sizeof(d.standard));

		dicore::CopyUtf8(a.DriveMap, d.drive_letters, sizeof(d.drive_letters));
		dicore::CopyUtf8(a.SmartKeyName, d.smart_key, sizeof(d.smart_key));

		int ifk = static_cast<int>(a.InterfaceType);
		d.interface_kind = (ifk >= DIK_IF_UNKNOWN && ifk <= DIK_IF_NVME) ? ifk : DIK_IF_OTHER;
		d.media = a.IsSsd ? DIK_MEDIA_SSD : DIK_MEDIA_HDD;
		d.health = (a.DiskStatus <= CAtaSmart::DISK_STATUS_BAD) ? static_cast<int32_t>(a.DiskStatus) : DIK_HEALTH_UNKNOWN;
		d.temperature_c = Temperature(a.Temperature);
		d.life_percent = Life(a.Life);

		// Rotation RPM: NominalMediaRotationRate if > 1; 1 means SSD -> DIK_NA; 0 -> DIK_NA
		d.rotation_rpm = (a.NominalMediaRotationRate > 1) ? static_cast<int32_t>(a.NominalMediaRotationRate) : DIK_NA;

		d.power_on_hours = Hours(a.DetectedPowerOnHours);
		d.power_on_count = (a.PowerOnCount > 0) ? static_cast<int64_t>(a.PowerOnCount) : DIK_NA64;

		// Capacity MB: TotalDiskSize as is (the number the GUI prints; DiskInfoDlgUpdate.cpp:1533-1544)
		d.capacity_mb = (a.TotalDiskSize > 0) ? static_cast<int64_t>(a.TotalDiskSize) : DIK_NA64;

		// Capacity bytes: NumberOfSectors * LogicalSectorSize only if both are non-zero.
		// Reading the source shows NumberOfSectors is set on the ATA path (AtaSmart.cpp:3191, 3195, 3205)
		// but remains 0 on NVMe (AtaSmart.cpp:3879); on NVMe this returns DIK_NA64.
		if (a.NumberOfSectors > 0 && a.LogicalSectorSize > 0)
		{
			d.capacity_bytes = static_cast<int64_t>(a.NumberOfSectors) * static_cast<int64_t>(a.LogicalSectorSize);
		}
		else
		{
			d.capacity_bytes = DIK_NA64;
		}

		// Host reads / writes / NAND writes (already in GB in CAtaSmart; AtaSmart.cpp:4063-4064, 4557-4590, 4493-4495)
		d.host_reads_gb = (a.HostReads >= 0) ? static_cast<int64_t>(a.HostReads) : DIK_NA64;
		d.host_writes_gb = (a.HostWrites >= 0) ? static_cast<int64_t>(a.HostWrites) : DIK_NA64;
		d.nand_writes_gb = (a.NandWrites >= 0) ? static_cast<int64_t>(a.NandWrites) : DIK_NA64;

		// Features bitmask
		uint32_t feats = 0;
		if (a.IsSmartSupported)             feats |= DIK_FEAT_SMART;
		if (a.IsApmSupported)               feats |= DIK_FEAT_APM;
		if (a.IsAamSupported)               feats |= DIK_FEAT_AAM;
		if (a.IsNcqSupported)               feats |= DIK_FEAT_NCQ;
		if (a.IsTrimSupported)              feats |= DIK_FEAT_TRIM;
		if (a.IsDeviceSleepSupported)       feats |= DIK_FEAT_DEVSLEEP;
		if (a.IsStreamingSupported)         feats |= DIK_FEAT_STREAMING;
		if (a.IsGplSupported)               feats |= DIK_FEAT_GPL;
		if (a.IsVolatileWriteCachePresent)  feats |= DIK_FEAT_VWC;
		if (a.IsLba48Supported)             feats |= DIK_FEAT_LBA48;
		if (a.IsNvCacheSupported)           feats |= DIK_FEAT_NVCACHE;
		if (a.IsApmEnabled)                 feats |= DIK_FEAT_APM_ON;
		if (a.IsAamEnabled)                 feats |= DIK_FEAT_AAM_ON;
		d.features = feats;

		bool gotData = a.AttributeCount > 0 || a.Temperature != -1000;
		d.access = (a.IsSmartCorrect && !a.IsCheckSumError && gotData) ? DIK_ACCESS_FULL : DIK_ACCESS_PARTIAL;
		d.source = static_cast<uint32_t>(a.CommandType);

		// Attribute rows: mirror UpdateListCtrl skipping Id == 0x00 and Id == 0xFF
		attrs.clear();
		for (DWORD j = 0; j < a.AttributeCount; ++j)
		{
			if (a.Attribute[j].Id == 0x00 || a.Attribute[j].Id == 0xFF)
			{
				continue;
			}

			dik_attribute_t row;
			memset(&row, 0, sizeof(row));
			row.struct_size = sizeof(row);
			row.id = a.Attribute[j].Id;

			// Attribute values per vendor/type (UpdateListCtrl lines 753-1025)
			if (a.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDFORCE)
			{
				row.current = a.Attribute[j].CurrentValue;
				row.worst = a.Attribute[j].WorstValue;
				row.threshold = a.IsThresholdCorrect ? a.Threshold[j].ThresholdValue : DIK_NA;

				row.raw_len = 7;
				for (int b = 0; b < 6; ++b) row.raw[b] = a.Attribute[j].RawValue[b];
				row.raw[6] = a.Attribute[j].Reserved;
			}
			else if (a.DiskVendorId == CAtaSmart::SSD_VENDOR_JMICRON && a.IsRawValues8)
			{
				row.current = a.Attribute[j].CurrentValue;
				row.worst = DIK_NA;
				row.threshold = DIK_NA;

				row.raw_len = 8;
				row.raw[0] = a.Attribute[j].WorstValue;
				for (int b = 0; b < 6; ++b) row.raw[b + 1] = a.Attribute[j].RawValue[b];
				row.raw[7] = a.Attribute[j].Reserved;
			}
			else if (a.DiskVendorId == CAtaSmart::SSD_VENDOR_INDILINX)
			{
				row.current = DIK_NA;
				row.worst = DIK_NA;
				row.threshold = DIK_NA;

				row.raw_len = 8;
				row.raw[0] = a.Attribute[j].CurrentValue;
				row.raw[1] = a.Attribute[j].WorstValue;
				for (int b = 0; b < 6; ++b) row.raw[b + 2] = a.Attribute[j].RawValue[b];
			}
			else if (a.DiskVendorId == CAtaSmart::SSD_VENDOR_NVME)
			{
				row.current = DIK_NA;
				row.worst = DIK_NA;
				row.threshold = DIK_NA;

				row.raw_len = 7;
				for (int b = 0; b < 6; ++b) row.raw[b] = a.Attribute[j].RawValue[b];
				row.raw[6] = a.Attribute[j].Reserved;
			}
			else
			{
				row.current = a.Attribute[j].CurrentValue;
				row.worst = a.Attribute[j].WorstValue;
				row.threshold = a.IsThresholdCorrect ? a.Threshold[j].ThresholdValue : DIK_NA;

				row.raw_len = 6;
				for (int b = 0; b < 6; ++b) row.raw[b] = a.Attribute[j].RawValue[b];
			}

			// raw_value as little-endian integer from raw[0..raw_len-1]
			uint64_t val = 0;
			for (int b = 0; b < row.raw_len; ++b)
			{
				val |= (static_cast<uint64_t>(row.raw[b]) << (8 * b));
			}
			row.raw_value = val;

			row.status = dicore::AttributeStatus(ata, diskIdx, static_cast<int>(j));

			attrs.push_back(row);
		}

		d.attribute_count = static_cast<int32_t>(attrs.size());
	}
}

extern "C" {

uint32_t DIK_CALL dik_api_version(void)
{
	return (static_cast<uint32_t>(DIK_API_MAJOR) << 16) | DIK_API_MINOR;
}

int DIK_CALL dik_open(dik_handle** out, uint32_t flags)
{
	if (out == nullptr)
	{
		return DIK_E_ARG;
	}
	*out = nullptr;
	try
	{
		dik_handle* h = new dik_handle();
		h->flags = flags;
		*out = h;
		return DIK_OK;
	}
	catch (...)
	{
		return DIK_E_IO;
	}
}

void DIK_CALL dik_close(dik_handle* h)
{
	try { delete h; } catch (...) {}
}

int DIK_CALL dik_scan(dik_handle* h)
{
	if (h == nullptr)
	{
		return DIK_E_ARG;
	}
	try
	{
		dicore::ComScope com;
		if (!com.Usable())
		{
			return DIK_E_IO;
		}

		CAtaSmart ata; // fresh each scan
		dicore::Scan(ata, !(h->flags & DIK_OPEN_NO_WMI));

		std::vector<dik_disk_t> nextDisks;
		std::vector<std::vector<dik_attribute_t>> nextAttrs;
		size_t count = static_cast<size_t>(ata.vars.GetCount());
		nextDisks.reserve(count);
		nextAttrs.reserve(count);

		for (INT_PTR i = 0; i < ata.vars.GetCount(); ++i)
		{
			dik_disk_t d;
			std::vector<dik_attribute_t> attrs;
			FillDisk(ata, static_cast<int>(i), d, attrs);
			nextDisks.push_back(d);
			nextAttrs.push_back(std::move(attrs));
		}

		h->disks.swap(nextDisks);
		h->attributes.swap(nextAttrs);

		if (h->disks.empty() && !dicore::IsElevated())
		{
			return DIK_E_ACCESS;
		}
		return DIK_OK;
	}
	catch (CException* e)
	{
		e->Delete();
		return DIK_E_IO;
	}
	catch (...)
	{
		return DIK_E_IO;
	}
}

int DIK_CALL dik_disk_count(const dik_handle* h)
{
	if (h == nullptr)
	{
		return DIK_E_ARG;
	}
	return static_cast<int>(h->disks.size());
}

int DIK_CALL dik_disk(const dik_handle* h, int disk, dik_disk_t* out)
{
	if (h == nullptr || out == nullptr)
	{
		return DIK_E_ARG;
	}
	if (out->struct_size < sizeof(dik_disk_t))
	{
		return DIK_E_STRUCT_SIZE;
	}
	if (disk < 0 || static_cast<size_t>(disk) >= h->disks.size())
	{
		return DIK_E_NOT_FOUND;
	}
	*out = h->disks[static_cast<size_t>(disk)];
	out->struct_size = sizeof(dik_disk_t);
	return DIK_OK;
}

int DIK_CALL dik_attribute(const dik_handle* h, int disk, int row, dik_attribute_t* out)
{
	if (h == nullptr || out == nullptr)
	{
		return DIK_E_ARG;
	}
	if (out->struct_size < sizeof(dik_attribute_t))
	{
		return DIK_E_STRUCT_SIZE;
	}
	if (disk < 0 || static_cast<size_t>(disk) >= h->disks.size())
	{
		return DIK_E_NOT_FOUND;
	}
	const auto& attrs = h->attributes[static_cast<size_t>(disk)];
	if (row < 0 || static_cast<size_t>(row) >= attrs.size())
	{
		return DIK_E_NOT_FOUND;
	}
	*out = attrs[static_cast<size_t>(row)];
	out->struct_size = sizeof(dik_attribute_t);
	return DIK_OK;
}

} // extern "C"
