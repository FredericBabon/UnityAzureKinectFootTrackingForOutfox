using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Windows;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public enum KEYCODE
{
    VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_UP = 0x26, VK_DOWN = 0x28,
    VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45, VK_F = 0x46, VK_G = 0x47,
    VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F,
    VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54, VK_U = 0x55, VK_V = 0x56, VK_W = 0x57,
    VK_X = 0x58, VK_Y = 0x59, VK_Z = 0x5A, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3
}
public static class Keyboard
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);
    
    private const uint MAPVK_VK_TO_VSC = 0;

    [DllImport("user32.dll", SetLastError = true)]
    static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint cInputs, [In, Out] INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public DUMMYUNIONNAME input;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DUMMYUNIONNAME
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint INPUT_HARDWARE = 2;

    private const uint KEYEVENTF_KEYDOWN = 0;
    private const uint KEYEVENTF_KEYUP = 2;

    private const uint KEYEVENTF_SCANCODE = 8;

    private const uint KEYEVENTF_EXTENDEDKEY = 1;


    public static void Delay(int delay)
    {
        System.Threading.Thread.Sleep(delay);
    }

    public static void KeyDown(System.Windows.Forms.Keys keycode)
    {
        //keybd_event((byte)keycode, 0x0, 0, 0);// presses     

        INPUT input = new INPUT { type = INPUT_KEYBOARD };
        input.input.ki.wVk = (ushort)keycode;
        input.input.ki.wScan = (ushort)MapVirtualKey((uint)keycode, MAPVK_VK_TO_VSC);
        input.input.ki.time = 0;
        //input.input.ki.dwExtraInfo = (UIntPtr)GetMessageExtraInfo().ToInt32();
        //input.input.ki.dwFlags = KEYEVENTF_KEYUP;
        input.input.ki.dwFlags = KEYEVENTF_KEYDOWN + KEYEVENTF_EXTENDEDKEY;
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void KeyPress(KEYCODE keycode, int delay = 0)
    {
        keybd_event((byte)keycode, 0x0, 0, 0);// presses
        System.Threading.Thread.Sleep(delay);
        keybd_event((byte)keycode, 0x0, 2, 0); //releases
    }

    public static void KeyUp(System.Windows.Forms.Keys keycode)
    {
        //keybd_event((byte)keycode, 0, 2, 0); //release

        INPUT input = new INPUT { type = INPUT_KEYBOARD };
        input.input.ki.wVk = (ushort)keycode;
        input.input.ki.wScan = (ushort)MapVirtualKey((uint)keycode, MAPVK_VK_TO_VSC);
        input.input.ki.time = 0;
        //input.input.ki.dwExtraInfo = (UIntPtr)GetMessageExtraInfo().ToInt32();
        input.input.ki.dwFlags = KEYEVENTF_KEYUP + KEYEVENTF_EXTENDEDKEY;
        //input.input.ki.dwFlags = KEYEVENTF_KEYDOWN;
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    

}