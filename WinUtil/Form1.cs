using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinUtil
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public extern static bool EnumWindows(EnumWindowsDelegate lpEnumFunc,
            IntPtr lparam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd,
            StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd,
            StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hwnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter,
            int x, int y, int cx, int cy, int uFlags);

        [DllImport("user32")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("WinUtilHook.dll")]
        extern static void WinUtil_SetHook(IntPtr hWnd);

        [DllImport("WinUtilHook.dll")]
        extern static void WinUtil_UnsetHook();

        IntPtr hook64Wnd;

        Timer displayFixTimer = new Timer();
        Timer windowFixTimer = new Timer();
        Timer displayChangedTimer = new Timer();

        bool windowChanged = false;
        bool discardChanges = false;

        class WindowData
        {
            public IntPtr wnd;
            public string clsName;
            public string text;
            public RECT rect;

            public override string ToString()
            {
                return wnd.ToString() + "," + text + "," + clsName + "," + rect.left + "," + rect.right + "," + rect.top + "," + rect.bottom;
            }
        }

        class ScreenWindowData
        {
            public string key;
            public List<WindowData> windowList;
        }

        Dictionary<String, ScreenWindowData> screenData = new Dictionary<string, ScreenWindowData>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ScreenWindowData data = GetCurrent();
            data.windowList = GetWindowData();
            AddLog("現在のウィンドウ構成を記憶");

            displayFixTimer.Interval = 30 * 1000;
            displayFixTimer.Tick += DisplayFixTimer_Tick;

            windowFixTimer.Interval = 200;
            windowFixTimer.Tick += WindowFixTimer_Tick;

            displayChangedTimer.Interval = 30 * 1000;
            displayChangedTimer.Tick += DisplayChangedTimer_Tick;

            // 64bitプロセスを開始
            System.Diagnostics.Process.Start("WinUtilHook64.exe", Handle.ToString());

            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            WinUtil_SetHook(Handle);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 64bitプロセスを削除
            PostMessage(hook64Wnd, 0x8000 + 2, 0, 0);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            WinUtil_UnsetHook();
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        }

        private void DisplayFixTimer_Tick(object sender, EventArgs e)
        {
            discardChanges = false;
            displayFixTimer.Enabled = false;

            string key = DisplaySettingString();
            if (!screenData.ContainsKey(key))
            {
                //新しい解像度が30秒以上継続したら記憶しておく
                ScreenWindowData data = GetCurrent();
                data.windowList = GetWindowData();
                AddLog("新しい解像度を記憶");
            }
        }

        private void WindowFixTimer_Tick(object sender, EventArgs e)
        {
            if(discardChanges)
            {
                windowChanged = false;
                windowFixTimer.Enabled = false;
                AddLog("discardによるタイマー無効２");
            }
            else if(windowChanged)
            {
                windowChanged = false;
                AddLog("まだ移動中");
            }
            else
            {
                // 記憶
                ScreenWindowData data = GetCurrent();
                data.windowList = GetWindowData();
                windowFixTimer.Enabled = false;
                AddLog("移動が終わったので記憶");
            }
        }

        private void DisplayChangedTimer_Tick(object sender, EventArgs e)
        {
            displayChangedTimer.Enabled = false;

            string key = DisplaySettingString();
            if (screenData.ContainsKey(key))
            {
                SetWindowData(screenData[key].windowList);
                discardChanges = false;
                AddLog("ウィンドウを戻す");
            }
        }

        private void OnWindowChanged()
        {
            if (discardChanges)
            {
                if (windowChanged)
                {
                    windowChanged = false;
                    windowFixTimer.Enabled = false;
                    AddLog("discardによるタイマー無効");
                }
            }
            else if (windowChanged == false)
            {
                windowFixTimer.Enabled = true;
                windowChanged = true;

                AddLog("タイマー有効化");
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if(m.Msg == 0x8000 + 1)
            {
                OnWindowChanged();
                /*
                if(prevWnd != m.WParam)
                {
                    prevWnd = m.WParam;
                    prevItem = "C: " + DateTime.Now.ToLongTimeString() + ": " + GetWindowText(m.WParam);
                    prevCnt = 0;
                }
                else
                {
                    listBox1.Items.Remove(prevItem);
                    prevItem = "C: " + DateTime.Now.ToLongTimeString() + ": (" + (prevCnt++) + ")" + GetWindowText(m.WParam);
                }
                listBox1.Items.Add(prevItem);
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
                */
            }
            else if(m.Msg == 0x8000 + 2)
            {
                // 64bitプロセスのウィンドウハンドル取得
                hook64Wnd = m.WParam;
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            discardChanges = true;
            // タイマーをリセット
            displayFixTimer.Enabled = false;
            displayChangedTimer.Enabled = false;
            displayFixTimer.Enabled = true;
            displayChangedTimer.Enabled = true;

            AddLog("構成変更を検知" + DisplaySettingString());
            //listBox1.Items.Add(DateTime.Now.ToLongTimeString() + ": " + DisplaySettingString());
        }

        private ScreenWindowData GetCurrent()
        {
            string key = DisplaySettingString();
            if (!screenData.ContainsKey(key))
            {
                screenData[key] = new ScreenWindowData();
            }
            ScreenWindowData data = screenData[key];
            data.key = key;
            return data;
        }

        private static string DisplaySettingString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(Screen item in Screen.AllScreens)
            {
                sb.Append("(" + item.Bounds.X + "," + item.Bounds.Y + "," + item.Bounds.Width + "," + item.Bounds.Height + ")");
            }
            return sb.ToString();
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            int textLen = GetWindowTextLength(hWnd);
            if(textLen > 0)
            {
                StringBuilder sb = new StringBuilder(textLen + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            return "";
        }

        private List<WindowData> GetWindowData()
        {
            enumTmp = new List<WindowData>();
            EnumWindows(new EnumWindowsDelegate(EnumWindowCallBack), IntPtr.Zero);
            return enumTmp;
        }

        private static List<WindowData> enumTmp;
        private static bool EnumWindowCallBack(IntPtr hWnd, IntPtr lparam)
        {
            if(IsWindowVisible(hWnd))
            {
                WindowData data = new WindowData();
                data.wnd = hWnd;

                StringBuilder sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);
                data.clsName = sb.ToString();

                int textLen = GetWindowTextLength(hWnd);

                data.text = "";
                data.text = GetWindowText(hWnd);

                if (data.clsName != "Progman" && textLen > 0)
                {
                    GetWindowRect(hWnd, ref data.rect);
                    enumTmp.Add(data);
                }
            }

            return true;
        }

        private void SetWindowData(List<WindowData> windowList)
        {
            const int SWP_NOACTIVATE = 0x0010;
            const int SWP_NOOWNERZORDER = 0x200;
            const int SWP_NOZORDER = 4;

            StringBuilder sb = new StringBuilder(256);

            foreach(WindowData data in windowList)
            {
                // クラス名が一致したら
                if(GetClassName(data.wnd, sb, sb.Capacity) != 0 && data.clsName == sb.ToString())
                {
                    AddLog("SetPos: " + GetWindowText(data.wnd));

                    SetWindowPos(data.wnd, 0, data.rect.left, data.rect.top,
                        data.rect.right - data.rect.left, data.rect.bottom - data.rect.top,
                        SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOZORDER);
                }
            }
        }

        private void AddLog(string text)
        {
            if(listBox1.Items.Count > 100)
            {
                listBox1.Items.RemoveAt(100);
            }
            listBox1.Items.Insert(0, DateTime.Now.ToLongTimeString() + ": " + text);
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            if(displayChangedTimer.Enabled)
            {
                DisplayChangedTimer_Tick(null, null);
            }
        }
    }
}
