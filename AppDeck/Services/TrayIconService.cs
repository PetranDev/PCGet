using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace AppDeck.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint WmCommand = 0x0111;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmApp = 0x8000;
    private const uint TrayCallbackMessage = WmApp + 1;

    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;

    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;

    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;

    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const uint OpenCommandId = 1;
    private const uint CheckUpdatesCommandId = 2;
    private const uint ExitCommandId = 3;

    private static readonly Dictionary<nint, TrayIconService> Instances = [];
    private static readonly WndProcDelegate WndProc = WindowProcedure;
    private static ushort _windowClassAtom;

    private readonly nint _windowHandle;
    private readonly nint _iconHandle;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? CheckUpdatesRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        RegisterWindowClass();

        var moduleHandle = GetModuleHandle(null);

        _windowHandle = CreateWindowEx(
            0,
            "AppDeckTrayWindow",
            "AppDeck Tray",
            0,
            0,
            0,
            0,
            0,
            nint.Zero,
            nint.Zero,
            moduleHandle,
            nint.Zero);

        if (_windowHandle == nint.Zero)
            throw new InvalidOperationException("Unable to create the AppDeck tray window.");

        Instances[_windowHandle] = this;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppDeckSmall.ico");

        if (!File.Exists(iconPath))
            throw new FileNotFoundException("The AppDeck tray icon could not be found.", iconPath);

        _iconHandle = LoadImage(nint.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);

        if (_iconHandle == nint.Zero)
            throw new InvalidOperationException("Unable to load the AppDeck tray icon.");

        var data = CreateNotifyIconData();
        data.Flags = NifMessage | NifIcon | NifTip;
        data.CallbackMessage = TrayCallbackMessage;
        data.IconHandle = _iconHandle;
        data.ToolTip = "AppDeck";

        if (!Shell_NotifyIcon(NimAdd, ref data))
            throw new InvalidOperationException("Unable to add the AppDeck system tray icon.");
    }

    private NotifyIconData CreateNotifyIconData()
    {
        return new NotifyIconData
        {
            Size = Marshal.SizeOf<NotifyIconData>(),
            WindowHandle = _windowHandle,
            Id = 1
        };
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();

        if (menu == nint.Zero)
            return;

        try
        {
            AppendMenu(menu, MfString, OpenCommandId, "Open AppDeck");
            AppendMenu(menu, MfString, CheckUpdatesCommandId, "Check for updates");
            AppendMenu(menu, MfSeparator, 0, string.Empty);
            AppendMenu(menu, MfString, ExitCommandId, "Exit");

            GetCursorPos(out var point);
            SetForegroundWindow(_windowHandle);

            var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCmd, point.X, point.Y, 0, _windowHandle, nint.Zero);

            if (command == OpenCommandId)
                OpenRequested?.Invoke(this, EventArgs.Empty);
            else if (command == CheckUpdatesCommandId)
                CheckUpdatesRequested?.Invoke(this, EventArgs.Empty);
            else if (command == ExitCommandId)
                ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static void RegisterWindowClass()
    {
        if (_windowClassAtom != 0)
            return;

        var windowClass = new WindowClass
        {
            WindowProcedure = WndProc,
            Instance = GetModuleHandle(null),
            ClassName = "AppDeckTrayWindow"
        };

        _windowClassAtom = RegisterClass(ref windowClass);

        if (_windowClassAtom == 0)
            throw new InvalidOperationException("Unable to register the AppDeck tray window class.");
    }

    private static nint WindowProcedure(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        if (Instances.TryGetValue(windowHandle, out var instance))
        {
            if (message == TrayCallbackMessage)
            {
                var mouseMessage = unchecked((uint)lParam.ToInt64());

                if (mouseMessage == WmRButtonUp)
                {
                    instance.ShowContextMenu();
                    return nint.Zero;
                }

                if (mouseMessage == WmLButtonDblClk)
                {
                    instance.OpenRequested?.Invoke(instance, EventArgs.Empty);
                    return nint.Zero;
                }
            }

            if (message == WmCommand)
            {
                var command = unchecked((uint)wParam.ToInt64());

                if (command == OpenCommandId)
                {
                    instance.OpenRequested?.Invoke(instance, EventArgs.Empty);
                    return nint.Zero;
                }

                if (command == CheckUpdatesCommandId)
                {
                    instance.CheckUpdatesRequested?.Invoke(instance, EventArgs.Empty);
                    return nint.Zero;
                }

                if (command == ExitCommandId)
                {
                    instance.ExitRequested?.Invoke(instance, EventArgs.Empty);
                    return nint.Zero;
                }
            }
        }

        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_windowHandle != nint.Zero)
        {
            var data = CreateNotifyIconData();
            Shell_NotifyIcon(NimDelete, ref data);
            Instances.Remove(_windowHandle);
            DestroyWindow(_windowHandle);
        }

        if (_iconHandle != nint.Zero)
            DestroyIcon(_iconHandle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size;
        public nint WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ToolTip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid GuidItem;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public WndProcDelegate WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private delegate nint WndProcDelegate(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint instance, string name, uint type, int desiredWidth, int desiredHeight, uint load);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint menu, uint flags, nuint newItemId, string newItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint windowHandle, nint rectangle);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}