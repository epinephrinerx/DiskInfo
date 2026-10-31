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
			ata.vars[i].Threshold05 = 1;
			ata.vars[i].ThresholdC5 = 1;
			ata.vars[i].ThresholdC6 = 1;
			ata.vars[i].ThresholdFF = 10;
			ata.vars[i].DiskStatus = ata.CheckDiskStatus(static_cast<DWORD>(i));
		}
	}
}
