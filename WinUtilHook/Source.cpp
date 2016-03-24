
#include <Windows.h>

#pragma data_seg(".shareddata")
HHOOK g_hHook = NULL;
HWND g_hWnd = NULL;
#pragma data_seg()

HINSTANCE g_hInst;

extern "C" bool __stdcall WinUtil_SetHook(HWND hWnd);
extern "C" bool __stdcall WinUtil_UnsetHook();

LRESULT CALLBACK WinUtil_HookProc(int nCode, WPARAM wp, LPARAM lp)
{
	if (nCode == HC_ACTION) {
		CWPSTRUCT* msg = (CWPSTRUCT*)lp;
		if (msg->message == WM_WINDOWPOSCHANGED) {
			PostMessage(g_hWnd, WM_APP + 1, (WPARAM)(msg->hwnd), 0);
		}
	}
	return CallNextHookEx(g_hHook, nCode, wp, lp);
}

bool __stdcall WinUtil_SetHook(HWND hWnd)
{
	WinUtil_UnsetHook();
	g_hHook = SetWindowsHookEx(WH_CALLWNDPROC, WinUtil_HookProc, g_hInst, 0);
	if (g_hHook == NULL) {
		return false;
	}
	g_hWnd = hWnd;
	return true;
}

bool __stdcall WinUtil_UnsetHook()
{
	if (g_hHook != NULL) {
		if (UnhookWindowsHookEx(g_hHook) == FALSE) {
			return false;
		}
	}
	g_hHook = NULL;
	return true;
}

// エントリポイント
BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
	)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		// アタッチ
		g_hInst = hModule;
		break;
	case DLL_PROCESS_DETACH:
		// デタッチ
		break;
	}
	return TRUE;
}
