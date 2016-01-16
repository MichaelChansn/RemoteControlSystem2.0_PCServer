﻿
using ControlServer1._0.DataPacket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ControlServer1._0.CommandProcess
{
    class CmdProcess
    {

        private static System.Timers.Timer timerShutdown = new System.Timers.Timer();
        public static String CODE = "天王盖地虎";
        static CmdProcess()
        {
            timerShutdown.Elapsed += new System.Timers.ElapsedEventHandler(timerTick);
            timerShutdown.Enabled = false;
        }
        #region
        //模拟键盘按键
        [DllImport("user32.dll")]
        //下面的bScan一定要加上，才能真正的模拟键盘按键，在硬件层上的模拟使用 MapVirtualKey((byte)right_button, 0)带的bScan
        //同时dwFlags参数必须加上KEYEVENTF_EXTENDEDKEY才行
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        [DllImport("user32.dll")]
        static extern byte MapVirtualKey(byte wCode, int wMap);
        const int KEYEVENTF_EXTENDEDKEY = 0x01;
        const int KEYEVENTF_KEYUP = 0x02;

        //模拟鼠标事件
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);   //该函数可以改变鼠标指针的位置。其中X，Y是相对于屏幕左上角的绝对位置。   

        [DllImport("user32")]
        private static extern int mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        const int MOUSEEVENTF_MOVE = 0x0001;
        const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        const int MOUSEEVENTF_LEFTUP = 0x0004;
        const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const int MOUSEEVENTF_RIGHTUP = 0x0010;
        const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        const int MOUSEEVENTF_WHEEL = 0x0800;
        //注意：
        //是在某些电脑上桌面上控制鼠标不灵不行，这个问题，我研究了两天是360在作怪，把360关掉就没问题了，一切正常

        /**game controller key map*/
        public static Keys up_button = Keys.W;
        public static Keys down_button = Keys.S;
        public static Keys left_button = Keys.A;
        public static Keys right_button = Keys.D;

        public static Keys A_button = Keys.J;
        public static Keys B_button = Keys.K;
        public static Keys C_button = Keys.U;
        public static Keys D_button = Keys.I;

        public static Keys Start = Keys.L;
        public static Keys Stop = Keys.O;
        public static Keys Other1 = Keys.D1;
        public static Keys Other2 = Keys.D2;
        public static Keys Other3 = Keys.D3;
        public static Keys Other4 = Keys.D4;

        #endregion
        #region
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokPriv1Luid
        {
            public int Count;
            public long Luid;
            public int Attr;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);
        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);
        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool ExitWindowsEx(int flg, int rea);
        internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const int TOKEN_QUERY = 0x00000008;
        internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        internal const int EWX_LOGOFF = 0x00000000;
        internal const int EWX_SHUTDOWN = 0x00000001;
        internal const int EWX_REBOOT = 0x00000002;
        internal const int EWX_FORCE = 0x00000004;
        internal const int EWX_POWEROFF = 0x00000008;
        internal const int EWX_FORCEIFHUNG = 0x00000010;
        [DllImport("user32.dll")]
        private static extern void LockWorkStation();
        //操控电脑函数
        private static void DoExitWin(int flg)
        {

            bool ok;
            TokPriv1Luid tp;
            IntPtr hproc = GetCurrentProcess();
            IntPtr htok = IntPtr.Zero;
            ok = OpenProcessToken(hproc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok);
            tp.Count = 1;
            tp.Luid = 0;
            tp.Attr = SE_PRIVILEGE_ENABLED;
            ok = LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref tp.Luid);
            ok = AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            ok = ExitWindowsEx(flg, 0);
        }
        #endregion
        private static int big2Small(int bigInt)
        {
            int ret = 0;
            byte b1 = (byte)(bigInt & 0x000000ff);
            byte b2 = (byte)((bigInt >> 8) & 0x000000ff);
            byte b3 = (byte)((bigInt >> 16) & 0x000000ff);
            byte b4 = (byte)((bigInt >> 24) & 0x000000ff);
            ret = ((b1 & 0x000000ff) << 24) | ((b2 & 0x000000ff) << 16) | ((b3 & 0x000000ff) << 8) | (b4 & 0x000000ff);
            return ret;
        }

        private const int UP = 1;
        private const int DOWN = 2;
        public static void processCmd(ServerForm mainForm, BinaryReader reader, ENUMS.MESSAGETYPE messageType)
        {
            try
            {
                Console.WriteLine(messageType);
                switch (messageType)
                {
                       
                    case ENUMS.MESSAGETYPE.EXIT:
                        mainForm.stopClient();//disconnect socket
                        break;
                    case ENUMS.MESSAGETYPE.CODE:
                        int mesLen = big2Small(reader.ReadInt32());
                        byte[] msg = reader.ReadBytes(mesLen);
                        if (Encoding.UTF8.GetString(msg) != "小鸡炖蘑菇")
                        {
                            mainForm.stopClient();
                        }
                        break;
                    case ENUMS.MESSAGETYPE.FUN_LOCK:
                        LockWorkStation();//lock PC
                        break;
                    case ENUMS.MESSAGETYPE.FUN_LOGOUT:
                        DoExitWin(EWX_LOGOFF);//logout
                        break;
                    case ENUMS.MESSAGETYPE.FUN_MANAGER://open the task manager
                        ProcessStartInfo ps = new ProcessStartInfo();
                        ps.FileName = @"C:\WINDOWS\system32\taskmgr.exe";
                        Process.Start(ps);
                        break;
                    case ENUMS.MESSAGETYPE.FUN_RESTART:
                        DoExitWin(EWX_REBOOT);
                        break;
                    case ENUMS.MESSAGETYPE.FUN_SHOW_DESKTOP:
                        Type shellType = Type.GetTypeFromProgID("Shell.Application");
                        object shellObject = System.Activator.CreateInstance(shellType);
                        shellType.InvokeMember("ToggleDesktop", System.Reflection.BindingFlags.InvokeMethod, null, shellObject, null);
                        break;
                    case ENUMS.MESSAGETYPE.FUN_SHUTDOWN:
                        DoExitWin(EWX_SHUTDOWN | EWX_FORCE);
                        break;
                    case ENUMS.MESSAGETYPE.FUN_SHUTDOWN_CANCEL:
                        cancelShutdown();
                        break;
                    case ENUMS.MESSAGETYPE.FUN_SHUTDOWN_TIME:
                        int timeSpan = big2Small(reader.ReadInt32());//delay shutdown, int data,
                        shutDownTime(timeSpan);
                        break;
                    case ENUMS.MESSAGETYPE.FUN_SLEEP:
                        Application.SetSuspendState(PowerState.Hibernate, true, true);
                        break;
                    case ENUMS.MESSAGETYPE.HOST_NANME:
                        int len = big2Small(reader.ReadInt32());
                        byte[] message = reader.ReadBytes(len);
                        String msg2 = Encoding.UTF8.GetString(message);
                        mainForm.setMessageHost(msg2);
                        break;
                    case ENUMS.MESSAGETYPE.KEY_DOWN:
                        ENUMS.SPECIALKEYS key = (ENUMS.SPECIALKEYS)reader.ReadByte();
                        processKeys(key, DOWN);
                        break;
                    case ENUMS.MESSAGETYPE.KEY_UP:
                        ENUMS.SPECIALKEYS key2 = (ENUMS.SPECIALKEYS)reader.ReadByte();
                        processKeys(key2, UP);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_LEFT_CLICK:
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_LEFT_DOUBLE_CLICK:
                        //双击左键
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        Thread.Sleep(200);
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_LEFT_DOWN:
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_LEFT_UP:
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_MOVE:
                        int xdis = big2Small(reader.ReadInt32());
                        int ydis = big2Small(reader.ReadInt32());
                        mouse_event(MOUSEEVENTF_MOVE, xdis, ydis, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_RIGHT_CLICK:
                        mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_RIGHT_DOUBLE_CLICK:
                        //双击右键
                        mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                        Thread.Sleep(200);
                        mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_RIGHT_DOWN:
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_RIGHT_UP:
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_WHEEL:
                        int wheel = big2Small(reader.ReadInt32());
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, wheel, 0);
                        break;
                    case ENUMS.MESSAGETYPE.MOUSE_SET:
                        int xPoint = big2Small(reader.ReadInt32());
                        int yPoint = big2Small(reader.ReadInt32());
                        //mouse_event(MOUSEEVENTF_ABSOLUTE, xPoint, yPoint, 0, 0);
                        SetCursorPos(xPoint, yPoint);
                        break;
                    case ENUMS.MESSAGETYPE.START_PIC:
                        mainForm.startSendPicFlags();
                        break;
                    case ENUMS.MESSAGETYPE.STOP_PIC:
                        mainForm.stopSendPicFlags();
                        break;
                    case ENUMS.MESSAGETYPE.TEXT:
                        int textLen = big2Small(reader.ReadInt32());
                        byte[] textByte = reader.ReadBytes(textLen);
                        SendKeys.SendWait(Encoding.UTF8.GetString(textByte));
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception("client is closed");
            }

        }

        private static void processKeys(ENUMS.SPECIALKEYS keys, int UPORDOWN)
        {
            byte key = (byte)Keys.None;
            switch (keys)
            {
                case ENUMS.SPECIALKEYS.ALT:
                    key = (byte)(Keys.Menu);
                    break;
                case ENUMS.SPECIALKEYS.ARROW_DOWN:
                    key = (byte)(Keys.Down);
                    break;
                case ENUMS.SPECIALKEYS.ARROW_LEFT:
                    key = (byte)(Keys.Left);
                    break;
                case ENUMS.SPECIALKEYS.ARROW_RIGHT:
                    key = (byte)(Keys.Right);
                    break;
                case ENUMS.SPECIALKEYS.ARROW_UP:
                    key = (byte)(Keys.Up);
                    break;
                case ENUMS.SPECIALKEYS.BACKSPACE:
                    key = (byte)(Keys.Back);
                    break;
                case ENUMS.SPECIALKEYS.CAPSLOCK:
                    key = (byte)(Keys.CapsLock);
                    break;
                case ENUMS.SPECIALKEYS.CTRL:
                    key = (byte)(Keys.ControlKey);
                    break;
                case ENUMS.SPECIALKEYS.DEL:
                    key = (byte)(Keys.Delete);
                    break;
                case ENUMS.SPECIALKEYS.END:
                    key = (byte)(Keys.End);
                    break;
                case ENUMS.SPECIALKEYS.ENTER:
                    key = (byte)(Keys.Enter);
                    break;
                case ENUMS.SPECIALKEYS.ESC:
                    key = (byte)(Keys.Escape);
                    break;
                case ENUMS.SPECIALKEYS.F1:
                    key = (byte)(Keys.F1);
                    break;
                case ENUMS.SPECIALKEYS.F10:
                    key = (byte)(Keys.F10);
                    break;
                case ENUMS.SPECIALKEYS.F11:
                    key = (byte)(Keys.F11);
                    break;
                case ENUMS.SPECIALKEYS.F12:
                    key = (byte)(Keys.F12);
                    break;
                case ENUMS.SPECIALKEYS.F2:
                    key = (byte)(Keys.F2);
                    break;
                case ENUMS.SPECIALKEYS.F3:
                    key = (byte)(Keys.F3);
                    break;
                case ENUMS.SPECIALKEYS.F4:
                    key = (byte)(Keys.F4);
                    break;
                case ENUMS.SPECIALKEYS.F5:
                    key = (byte)(Keys.F5);
                    break;
                case ENUMS.SPECIALKEYS.F6:
                    key = (byte)(Keys.F6);
                    break;
                case ENUMS.SPECIALKEYS.F7:
                    key = (byte)(Keys.F7);
                    break;
                case ENUMS.SPECIALKEYS.F8:
                    key = (byte)(Keys.F8);
                    break;
                case ENUMS.SPECIALKEYS.F9:
                    key = (byte)(Keys.F9);
                    break;
                case ENUMS.SPECIALKEYS.HOME:
                    key = (byte)(Keys.Home);
                    break;
                case ENUMS.SPECIALKEYS.INSERT:
                    key = (byte)(Keys.Insert);
                    break;
                case ENUMS.SPECIALKEYS.NUMLOCK:
                    key = (byte)(Keys.NumLock);
                    break;
                case ENUMS.SPECIALKEYS.PAGEDOWN:
                    key = (byte)(Keys.PageDown);
                    break;
                case ENUMS.SPECIALKEYS.PAGEUP:
                    key = (byte)(Keys.PageUp);
                    break;
                case ENUMS.SPECIALKEYS.PRTSC:
                    key = (byte)(Keys.PrintScreen);
                    break;
                case ENUMS.SPECIALKEYS.SHIFT:
                    key = (byte)(Keys.ShiftKey);
                    break;
                case ENUMS.SPECIALKEYS.SPACE:
                    key = (byte)(Keys.Space);
                    break;
                case ENUMS.SPECIALKEYS.TAB:
                    key = (byte)(Keys.Tab);
                    break;
                case ENUMS.SPECIALKEYS.WIN:
                    key = (byte)(Keys.LWin);
                    break;
                case ENUMS.SPECIALKEYS.GAME_A:
                    key = (byte)(A_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_B:
                    key = (byte)(B_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_C:
                    key = (byte)(C_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_D:
                    key = (byte)(D_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_DOWN:
                    key = (byte)(down_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_LEFT:
                    key = (byte)(left_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_OTHER1:
                    key = (byte)(Other1);
                    break;
                case ENUMS.SPECIALKEYS.GAME_OTHER2:
                    key = (byte)(Other2);
                    break;
                case ENUMS.SPECIALKEYS.GAME_OTHER3:
                    key = (byte)(Other3);
                    break;
                case ENUMS.SPECIALKEYS.GAME_OTHER4:
                    key = (byte)(Other4);
                    break;
                case ENUMS.SPECIALKEYS.GAME_RIGHT:
                    key = (byte)(right_button);
                    break;
                case ENUMS.SPECIALKEYS.GAME_START:
                    key = (byte)(Start);
                    break;
                case ENUMS.SPECIALKEYS.GAME_STOP:
                    key = (byte)(Stop);
                    break;
                case ENUMS.SPECIALKEYS.GAME_UP:
                    key = (byte)(up_button);
                    break;
                default:
                    break;

            }
            if (key == (byte)Keys.None) return;
            if (UPORDOWN == UP)
                keybd_event(key, MapVirtualKey(key, 0), KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
            else
                keybd_event(key, MapVirtualKey(key, 0), KEYEVENTF_EXTENDEDKEY | 0, 0);

        }

        private static void cancelShutdown()
        {
            timerShutdown.Stop();
        }
        private static void shutDownTime(int secondsFromNow)
        {
            timerShutdown.Stop();
            timerShutdown.Interval = secondsFromNow * 1000;
            timerShutdown.Start();

        }
        private static void timerTick(object sender, EventArgs e)
        {
            timerShutdown.Stop();
            DoExitWin(EWX_SHUTDOWN | EWX_FORCE);
        }
    }
}
