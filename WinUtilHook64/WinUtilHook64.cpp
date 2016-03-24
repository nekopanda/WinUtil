#include <windows.h>

LRESULT CALLBACK WindowProc(HWND, UINT, WPARAM, LPARAM);

typedef bool (__stdcall *WinUtil_SetHook_F)(HWND hWnd);
typedef bool (__stdcall *WinUtil_UnsetHook_F)();

WinUtil_SetHook_F WinUtil_SetHook = NULL;
WinUtil_UnsetHook_F WinUtil_UnsetHook = NULL;

HWND g_parentWnd = NULL;

int WINAPI WinMain(HINSTANCE hInst, HINSTANCE hPrevInst,
	LPSTR cmdLine, int nCmdShow)
{
	g_parentWnd = (HWND)strtol(cmdLine, NULL, 10);

	if (g_parentWnd == NULL) {
		MessageBox(NULL, "起動するのはこっちじゃないよ", "ダメダメ", MB_OK);
		return FALSE;
	}

	HMODULE hModule = LoadLibrary("WinUtilHookX64.dll");
	WinUtil_SetHook = (WinUtil_SetHook_F)GetProcAddress(hModule, "WinUtil_SetHook");
	WinUtil_UnsetHook = (WinUtil_UnsetHook_F)GetProcAddress(hModule, "WinUtil_UnsetHook");
	if (hModule == NULL || WinUtil_SetHook == NULL || WinUtil_UnsetHook == NULL) {
		MessageBox(NULL, "モジュールロードに失敗", "エラー", MB_OK);
		return FALSE;
	}

	LPCSTR pClassName = "WinUtilHook64Class";

	WNDCLASSEX wc;
	wc.cbSize = sizeof(WNDCLASSEX);
	wc.style = CS_HREDRAW | CS_VREDRAW;
	wc.lpfnWndProc = WindowProc;
	wc.cbClsExtra = 0;
	wc.cbWndExtra = 0;
	wc.hInstance = hInst;
	wc.hIcon = NULL;
	wc.hIconSm = NULL;
	wc.hCursor = LoadCursor(NULL, IDC_ARROW);
	wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
	wc.lpszMenuName = NULL;
	wc.lpszClassName = pClassName;

	if (!RegisterClassEx(&wc)) return FALSE;

	HWND hWindow = CreateWindow(pClassName, "WinUtilHook64 Dummy Window",
		WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
		NULL, NULL, hInst, NULL);

	if (!hWindow) return FALSE;

	if (!WinUtil_SetHook(g_parentWnd)) {
		MessageBox(NULL, "フックに失敗", "エラー", MB_OK);
		DestroyWindow(hWindow);
	}

	// 親にウィンドウハンドルを通知
	PostMessage(g_parentWnd, WM_APP + 2, (WPARAM)hWindow, NULL);

	MSG msg;
	while (GetMessage(&msg, NULL, 0, 0)) {
		TranslateMessage(&msg);
		DispatchMessage(&msg);
	}

	WinUtil_UnsetHook();

	return 0;
}

LRESULT CALLBACK WindowProc(HWND hWindow, UINT msg, WPARAM wp, LPARAM lp)
{
	switch (msg) {
	case WM_DESTROY:
		PostQuitMessage(0);
		break;
	case WM_APP+2:
		WinUtil_UnsetHook();
		DestroyWindow(hWindow);
		break;
	default:
		return DefWindowProc(hWindow, msg, wp, lp);
	}
	return 0;
}
