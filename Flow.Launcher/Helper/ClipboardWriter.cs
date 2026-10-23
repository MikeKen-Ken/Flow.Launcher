using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.Helper;

/// <summary>
/// Puts text or a file on the clipboard.
/// WPF <see cref="System.Windows.Clipboard.SetText"/> and
/// <see cref="System.Windows.Clipboard.SetFileDropList"/> call OleFlushClipboard.
/// A clipboard viewer that opens the clipboard to read the new data makes that flush
/// fail with CLIPBRD_E_CANT_OPEN after about a second, and the caller reports a failed
/// copy even though the data was already placed. These helpers copy the bytes with
/// SetClipboardData and do not flush.
/// </summary>
internal static class ClipboardWriter
{
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;
    private const uint CfUnicodeText = 13;
    private const uint CfHdrop = 15;
    private const int ClipbrdECantOpen = unchecked((int)0x800401D0);

    internal static void SetText(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        var bytes = Encoding.Unicode.GetBytes(text + "\0");
        Set(CfUnicodeText, bytes);
    }

    internal static void SetFileDrop(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path is required.", nameof(path));

        // DROPFILES is 20 bytes, then a double-null-terminated UTF-16 path list.
        var pathBytes = Encoding.Unicode.GetBytes(path + "\0\0");
        var bytes = new byte[20 + pathBytes.Length];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), 20);
        BitConverter.TryWriteBytes(bytes.AsSpan(16, 4), 1);
        pathBytes.CopyTo(bytes.AsSpan(20));
        Set(CfHdrop, bytes);
    }

    private static void Set(uint format, byte[] bytes)
    {
        if (!OpenClipboard(IntPtr.Zero))
            throw new COMException("OpenClipboard Failed (0x800401D0 (CLIPBRD_E_CANT_OPEN))", ClipbrdECantOpen);

        var owned = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
                throw new COMException("EmptyClipboard failed.", Marshal.GetHRForLastWin32Error());

            owned = GlobalAlloc(GmemMoveable | GmemZeroInit, (nuint)bytes.Length);
            if (owned == IntPtr.Zero)
                throw new COMException("GlobalAlloc failed.", Marshal.GetHRForLastWin32Error());

            var locked = GlobalLock(owned);
            if (locked == IntPtr.Zero)
                throw new COMException("GlobalLock failed.", Marshal.GetHRForLastWin32Error());

            try
            {
                Marshal.Copy(bytes, 0, locked, bytes.Length);
            }
            finally
            {
                GlobalUnlock(owned);
            }

            if (SetClipboardData(format, owned) == IntPtr.Zero)
                throw new COMException("SetClipboardData failed.", Marshal.GetHRForLastWin32Error());

            owned = IntPtr.Zero;
        }
        finally
        {
            if (owned != IntPtr.Zero)
                GlobalFree(owned);

            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
