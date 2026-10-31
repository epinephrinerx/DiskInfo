#pragma once

#include <afxwin.h>
#include <afxtempl.h>
#include <cstddef>
#include <objbase.h>

// AtaSmart.h declares JMicron types only if this flag is set, and the flag
// comes from CompileOptions.h (which the GUI gets through stdafx.h).
#include "CompileOptions.h"
#include "AtaSmart.h"

namespace dicore
{
	// Balanced COM init for the calling thread. Init() does not do this itself
	// (AtaSmart.cpp:836 is commented out) but needs COM for WMI.
	class ComScope
	{
	public:
		ComScope()
		{
			hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
			uninit = SUCCEEDED(hr);                    // S_OK and S_FALSE both need CoUninitialize
		}
		~ComScope() { if (uninit) CoUninitialize(); }
		ComScope(const ComScope&) = delete;
		ComScope& operator=(const ComScope&) = delete;

		// RPC_E_CHANGED_MODE: the thread already has COM in another mode; use it as is.
		bool Usable() const { return SUCCEEDED(hr) || hr == RPC_E_CHANGED_MODE; }

	private:
		bool uninit = false;
		HRESULT hr = S_OK;
	};

	// UTF-8 into a fixed buffer, always NUL-terminated, cut on a character boundary.
	void CopyUtf8(const CString& src, char* dst, size_t cap);

	bool IsElevated();

	// Runs one full scan into `ata` with the GUI's defaults and computes DiskStatus for every disk.
	// Requires a usable dicore::ComScope alive on the calling thread for the lifetime of the CAtaSmart object.
	// CException* and other exceptions propagate; callers catch them at their ABI boundary.
	void Scan(CAtaSmart& ata, bool useWmi);

	// Computes attribute health status: 0 unknown, 1 good, 2 caution, 3 bad.
	// Callers must run dicore::Scan first (it sets AlarmTemperature and the caution thresholds).
	// Literal port of CDiskInfoDlg::UpdateListCtrl icon decision (DiskInfoDlgUpdate.cpp:325-726).
	int AttributeStatus(const CAtaSmart& ata, int disk, int attr);
}
