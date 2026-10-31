#include <afxwin.h>
#include <afxtempl.h>

#include <string>

#include "engine.h"

// CAtaSmart logs through DebugPrint(), and the original (Priscilla/UtilityFx.cpp)
// deletes "<host exe>.log" on its first call, before it checks the debug mode.
// A library must not delete files next to its host, so DiskInfoCoreLib.vcxproj
// compiles UtilityFx.cpp with DebugPrint renamed away and this no-op takes its place.
void DebugPrint(CString) {}

namespace dicore
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

	// Runs one full scan into `ata` with the GUI's defaults and computes DiskStatus for every disk.
	// Requires a usable dicore::ComScope alive on the calling thread for the lifetime of the CAtaSmart object.
	// CException* and other exceptions propagate; callers catch them at their ABI boundary.
	void Scan(CAtaSmart& ata, bool useWmi)
	{
		BOOL changed = FALSE;

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
		ata.Init(useWmi ? TRUE : FALSE, // useWmi
		         TRUE,  // advancedDiskSearch
		         &changed,
		         FALSE, FALSE, // workaroundHD204UI, workaroundAdataSsd
		         FALSE,        // flagHideNoSmartDisk: report disks even without SMART
		         FALSE,        // flagSortDriveLetter
		         FALSE);       // flagHideRAIDVolume

		for (INT_PTR i = 0; i < ata.vars.GetCount(); ++i)
		{
			// Init() leaves DiskStatus at 0; the GUI computes it itself right after
			// (DiskInfoDlgInit.cpp:446), so the core must too.
			// The caution thresholds also come from the GUI's INI (defaults 1, 1, 1, 10,
			// DiskInfoDlgInit.cpp:441-444); at 0 an HDD with reallocated sectors reads Good.
			// Alarm temperature defaults (DiskInfoDlgInit.cpp:431-438) are 60 for SSD, 50 for HDD.
			ata.vars[i].AlarmTemperature = ata.vars[i].IsSsd ? 60 : 50;
			ata.vars[i].Threshold05 = 1;
			ata.vars[i].ThresholdC5 = 1;
			ata.vars[i].ThresholdC6 = 1;
			ata.vars[i].ThresholdFF = 10;
			ata.vars[i].DiskStatus = ata.CheckDiskStatus(static_cast<DWORD>(i));
		}
	}

	// Returns 0 unknown, 1 good, 2 caution, 3 bad.
	// Literal port of CDiskInfoDlg::UpdateListCtrl icon decision (DiskInfoDlgUpdate.cpp:325-726).
	int AttributeStatus(const CAtaSmart& ata, int disk, int attr)
	{
		if (disk < 0 || disk >= ata.vars.GetCount())
		{
			return 0;
		}
		const auto& d = ata.vars[disk];
		if (attr < 0 || attr >= static_cast<int>(d.AttributeCount))
		{
			return 0;
		}
		if (d.Attribute[attr].Id == 0x00 || d.Attribute[attr].Id == 0xFF)
		{
			return 0;
		}

		// NVMe branch (DiskInfoDlgUpdate.cpp:325-383)
		if (d.IsNVMe)
		{
			if (d.Model.Find(_T("Parallels")) == 0
			||  d.Model.Find(_T("VMware")) == 0
			||  d.Model.Find(_T("QEMU")) == 0)
			{
				return 0; // ICON_UNKNOWN
			}
			else if (d.Attribute[attr].Id == 0x01 && d.Attribute[attr].RawValue[0])
			{
				return 3; // ICON_BAD
			}
			else if (d.Attribute[attr].Id == 0x02)
			{
				if ((MAKEWORD(d.Attribute[1].RawValue[0], d.Attribute[1].RawValue[1]) - 273) >= d.AlarmTemperature)
				{
					return 3; // ICON_BAD
				}
				else if ((MAKEWORD(d.Attribute[1].RawValue[0], d.Attribute[1].RawValue[1]) - 273) == d.AlarmTemperature)
				{
					return 2; // ICON_CAUTION
				}
			}
			// 2022/10/02 Workaround for no support Available Spare Threshold devices
			// https://github.com/hiyohiyo/CrystalDiskInfo/issues/186
			else if (d.Attribute[attr].Id == 0x03 && d.Attribute[3].RawValue[0] > 100)
			{
				return 1; // ICON_GOOD
			}
			// 2022/03/26 Workaround for WD_BLACK AN1500 (No support Available Spare/Available Spare Threshold)
			else if (d.Attribute[attr].Id == 0x03 && (d.Attribute[2].RawValue[0] == 0 && d.Attribute[3].RawValue[0] == 0))
			{
				return 1; // ICON_GOOD
			}
			else if (d.Attribute[attr].Id == 0x03 && (d.Attribute[2].RawValue[0] < d.Attribute[3].RawValue[0]))
			{
				return 3; // ICON_BAD
			}
			else if (d.Attribute[attr].Id == 0x03 && (d.Attribute[2].RawValue[0] == d.Attribute[3].RawValue[0] && d.Attribute[3].RawValue[0] != 100))
			{
				return 2; // ICON_CAUTION
			}
			else if (d.Attribute[attr].Id == 0x05 && ((100 - d.Attribute[attr].RawValue[0]) <= d.ThresholdFF))
			{
				return 2; // ICON_CAUTION
			}

			return 1; // ICON_GOOD
		}
		// ATA branch with IsSmartCorrect && IsThresholdCorrect && !IsThresholdBug (DiskInfoDlgUpdate.cpp:384-715)
		else if (d.IsSmartCorrect && d.IsThresholdCorrect && !d.IsThresholdBug)
		{
			// DiskInfoDlgUpdate.cpp:386-449
			if (!d.IsSsd &&
				(d.Attribute[attr].Id == 0x05 // Reallocated Sectors Count
					|| d.Attribute[attr].Id == 0xC5 // Current Pending Sector Count
					|| d.Attribute[attr].Id == 0xC6 // Off-Line Scan Uncorrectable Sector Count
					))
			{
				WORD raw = MAKEWORD(d.Attribute[attr].RawValue[0], d.Attribute[attr].RawValue[1]);
				WORD threshold = 0;
				switch (d.Attribute[attr].Id)
				{
				case 0x05:
					threshold = d.Threshold05;
					break;
				case 0xC5:
					threshold = d.ThresholdC5;
					break;
				case 0xC6:
					threshold = d.ThresholdC6;
					break;
				}
				DWORD caution = (threshold > 0 && raw >= threshold) ? 1 : 0;

				if (d.Threshold[attr].ThresholdValue != 0
					&& d.Attribute[attr].CurrentValue < d.Threshold[attr].ThresholdValue)
				{
					return 3; // ICON_BAD
				}
				else if (caution > 0)
				{
					return 2; // ICON_CAUTION
				}
				else
				{
					return 1; // ICON_GOOD
				}
			}
			// [2021/12/15] Workaround for SanDisk USB Memory (DiskInfoDlgUpdate.cpp:450-461)
			else if (d.Attribute[attr].Id == 0xE8 && d.FlagLifeSanDiskUsbMemory)
			{
				return 1; // ICON_GOOD
			}
			// Temperature (DiskInfoDlgUpdate.cpp:463-473)
			else if (d.Attribute[attr].Id == 0xC2)
			{
				return 1; // ICON_GOOD
			}
			// End-to-End Error (DiskInfoDlgUpdate.cpp:474-487)
			else if (d.Attribute[attr].Id == 0xB8)
			{
				return 1; // ICON_GOOD
			}
			// Life for WDC/SanDisk (DiskInfoDlgUpdate.cpp:488-560)
			else if (d.Attribute[attr].Id == 0xE6 && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_WDC || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK))
			{
				int life = -1;

				if (d.FlagLifeSanDiskUsbMemory)
				{
					life = -1;
				}
				else if (d.FlagLifeSanDisk0_1)
				{
					life = 100 - (d.Attribute[attr].RawValue[1] * 256 + d.Attribute[attr].RawValue[0]) / 100;
				}
				else if (d.FlagLifeSanDisk1)
				{
					life = 100 - d.Attribute[attr].RawValue[1];
				}
				else if (d.FlagLifeSanDiskLenovo)
				{
					life = d.Attribute[attr].CurrentValue;
				}
				else
				{
					life = 100 - d.Attribute[attr].RawValue[1];
				}

				if (life <= 0) { life = 0; }

				if (d.FlagLifeSanDiskUsbMemory)
				{
					return 1; // ICON_GOOD
				}
				else if (life == 0)
				{
					return 3; // ICON_BAD
				}
				else if (life <= d.ThresholdFF)
				{
					return 2; // ICON_CAUTION
				}
				else
				{
					return 1; // ICON_GOOD
				}
			}
			// Life (DiskInfoDlgUpdate.cpp:561-626)
			else if (
				   (d.Attribute[attr].Id == 0xA9 && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_REALTEK || (d.DiskVendorId == CAtaSmart::SSD_VENDOR_KINGSTON && d.HostReadsWritesUnit == CAtaSmart::HOST_READS_WRITES_32MB) || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SILICONMOTION))
				|| (d.Attribute[attr].Id == 0xAD && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_TOSHIBA || d.DiskVendorId == CAtaSmart::SSD_VENDOR_KIOXIA))
				|| (d.Attribute[attr].Id == 0xB1 && d.DiskVendorId == CAtaSmart::SSD_VENDOR_SAMSUNG)
				|| (d.Attribute[attr].Id == 0xBB && d.DiskVendorId == CAtaSmart::SSD_VENDOR_MTRON)
				|| (d.Attribute[attr].Id == 0xCA && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_MICRON || d.DiskVendorId == CAtaSmart::SSD_VENDOR_MICRON_MU03 || d.DiskVendorId == CAtaSmart::SSD_VENDOR_INTEL_DC || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SILICONMOTION_CVC))
				|| (d.Attribute[attr].Id == 0xD1 && d.DiskVendorId == CAtaSmart::SSD_VENDOR_INDILINX)
				|| (d.Attribute[attr].Id == 0xE7 && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDFORCE || d.DiskVendorId == CAtaSmart::SSD_VENDOR_CORSAIR || d.DiskVendorId == CAtaSmart::SSD_VENDOR_KINGSTON || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SKHYNIX
				                                          || d.DiskVendorId == CAtaSmart::SSD_VENDOR_REALTEK || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SSSTC || d.DiskVendorId == CAtaSmart::SSD_VENDOR_APACER
														  || d.DiskVendorId == CAtaSmart::SSD_VENDOR_JMICRON || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SEAGATE || d.DiskVendorId == CAtaSmart::SSD_VENDOR_MAXIOTEK
														  || d.DiskVendorId == CAtaSmart::SSD_VENDOR_YMTC || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SCY || d.DiskVendorId == CAtaSmart::SSD_VENDOR_RECADATA || d.DiskVendorId == CAtaSmart::SSD_VENDOR_ADATA_INDUSTRIAL))
				|| (d.Attribute[attr].Id == 0xE8 && d.DiskVendorId == CAtaSmart::SSD_VENDOR_PLEXTOR)
				|| (d.Attribute[attr].Id == 0xE9 && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_INTEL || d.DiskVendorId == CAtaSmart::SSD_VENDOR_OCZ || d.DiskVendorId == CAtaSmart::SSD_VENDOR_OCZ_VECTOR || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SKHYNIX))
				|| (d.Attribute[attr].Id == 0xE9 && d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK_LENOVO_HELEN_VENUS)
				|| (d.Attribute[attr].Id == 0xE6 && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK_LENOVO || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK_DELL))
				|| (d.Attribute[attr].Id == 0xC9 && (d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK_HP || d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDISK_HP_VENUS))
				)
			{
				if (d.FlagLifeRawValue || d.FlagLifeRawValueIncrement)
				{
					return 1; // ICON_GOOD
				}
				else if (d.Attribute[attr].CurrentValue == 0
					|| d.Attribute[attr].CurrentValue < d.Threshold[attr].ThresholdValue)
				{
					return 3; // ICON_BAD
				}
				else if (d.Attribute[attr].CurrentValue <= d.ThresholdFF)
				{
					return 2; // ICON_CAUTION
				}
				else
				{
					return 1; // ICON_GOOD
				}
			}
			// Read Error Rate for SandForce Bug (DiskInfoDlgUpdate.cpp:627-666)
			else if (d.Attribute[attr].Id == 0x01
				&& d.DiskVendorId == CAtaSmart::SSD_VENDOR_SANDFORCE)
			{
				if (d.Attribute[attr].CurrentValue == 0
					&& d.Attribute[attr].RawValue[0] == 0
					&& d.Attribute[attr].RawValue[1] == 0)
				{
					return 1; // ICON_GOOD
				}
				else if (d.Threshold[attr].ThresholdValue != 0
					&& d.Attribute[attr].CurrentValue < d.Threshold[attr].ThresholdValue)
				{
					return 3; // ICON_BAD
				}
				else
				{
					return 1; // ICON_GOOD
				}
			}
			// Generic range check with threshold (DiskInfoDlgUpdate.cpp:667-703)
			else if ((d.IsSsd && !d.IsRawValues8)
				|| ((0x01 <= d.Attribute[attr].Id && d.Attribute[attr].Id <= 0x0D)
					||  d.Attribute[attr].Id == 0x16
					|| (0xBB <= d.Attribute[attr].Id && d.Attribute[attr].Id <= 0xC1)
					|| (0xC3 <= d.Attribute[attr].Id && d.Attribute[attr].Id <= 0xD1)
					|| (0xD3 <= d.Attribute[attr].Id && d.Attribute[attr].Id <= 0xD4)
					|| (0xDC <= d.Attribute[attr].Id && d.Attribute[attr].Id <= 0xE4)
					|| (0xE6 <= d.Attribute[attr].Id && d.Attribute[attr].Id <= 0xE7)
					||  d.Attribute[attr].Id == 0xF0
					||  d.Attribute[attr].Id == 0xFA
					||  d.Attribute[attr].Id == 0xFE
					))
			{
				if (d.Threshold[attr].ThresholdValue != 0
					&& d.Attribute[attr].CurrentValue < d.Threshold[attr].ThresholdValue)
				{
					return 3; // ICON_BAD
				}
				else
				{
					return 1; // ICON_GOOD
				}
			}
			// DiskInfoDlgUpdate.cpp:704-714
			else
			{
				return 1; // ICON_GOOD
			}
		}
		// DiskInfoDlgUpdate.cpp:716-726
		else
		{
			return 0; // ICON_UNKNOWN
		}
	}
}
