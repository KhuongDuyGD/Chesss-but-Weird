using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

// Independent native readback and restoration of the original text/image clipboard.
public sealed class MatchClipboardAudit : IDisposable
{
    private readonly Dictionary<uint, byte[]> saved = new Dictionary<uint, byte[]>();
    private static uint PngFormat => RegisterClipboardFormat("PNG");
    public MatchClipboardAudit()
    {
        Open();
        try
        {
            foreach (uint format in new uint[] { PngFormat, 17, 8, 13, 15, RegisterClipboardFormat("HTML Format"), RegisterClipboardFormat("Rich Text Format") })
            {
                if (saved.ContainsKey(format) || !IsClipboardFormatAvailable(format)) continue;
                byte[] bytes = ReadOpened(format); if (bytes != null) saved[format] = bytes;
            }
        }
        finally { CloseClipboard(); }
    }
    private static void Open()
    {
        for (int i = 0; i < 20; i++) { if (OpenClipboard(IntPtr.Zero)) return; Thread.Sleep(20); }
        throw new Exception("Could not open the clipboard for the isolated audit.");
    }
    private static byte[] ReadOpened(uint format)
    {
        IntPtr handle = GetClipboardData(format); ulong size = GlobalSize(handle).ToUInt64();
        if (handle == IntPtr.Zero || size == 0 || size > 64 * 1024 * 1024) return null;
        IntPtr pointer = GlobalLock(handle); if (pointer == IntPtr.Zero) return null;
        try { var bytes = new byte[(int)size]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return bytes; }
        finally { GlobalUnlock(handle); }
    }
    public static void CheckImage(byte[] expected, Action<bool, string> check)
    {
        byte[] png, dib; Open();
        try
        {
            check(IsClipboardFormatAvailable(PngFormat) && IsClipboardFormatAvailable(8), "Clipboard contains actual PNG and standard bitmap formats");
            png = ReadOpened(PngFormat); dib = ReadOpened(8);
        }
        finally { CloseClipboard(); }
        check(png != null && png.Take(expected.Length).SequenceEqual(expected), "Clipboard PNG preserves the rendered image bytes");
        var decoded = new Texture2D(2, 2); decoded.LoadImage(expected);
        try
        {
            int width = decoded.width, height = decoded.height, stride = (width * 3 + 3) & ~3;
            check(dib != null && BitConverter.ToInt32(dib, 0) == 40 && BitConverter.ToInt32(dib, 4) == width &&
                BitConverter.ToInt32(dib, 8) == height && BitConverter.ToInt16(dib, 14) == 24 && dib.Length >= 40 + stride * height,
                "Clipboard bitmap has correct dimensions, row layout and color depth");
            Color32[] pixels = decoded.GetPixels32(); bool equal = true;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int offset = 40 + y * stride + x * 3; Color32 color = pixels[y * width + x];
                equal &= dib[offset] == color.b && dib[offset + 1] == color.g && dib[offset + 2] == color.r;
            }
            check(equal, "Every clipboard bitmap pixel matches the preview without inversion or channel swaps");
        }
        finally { UnityEngine.Object.DestroyImmediate(decoded); }
    }
    public static IDisposable HoldOpen() { Open(); return new ClipboardLease(); }
    private sealed class ClipboardLease : IDisposable { public void Dispose() => CloseClipboard(); }
    public void Dispose()
    {
        IntPtr owner = CreateWindowEx(0, "STATIC", "Clipboard audit restore", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (owner == IntPtr.Zero) throw new Exception("Could not create the clipboard restore owner.");
        bool opened = false;
        try
        {
            for (int i = 0; i < 20 && !opened; i++) { opened = OpenClipboard(owner); if (!opened) Thread.Sleep(20); }
            if (!opened || !EmptyClipboard()) throw new Exception("Could not restore the original clipboard.");
            foreach (var format in saved)
            {
                IntPtr memory = GlobalAlloc(2, new UIntPtr((uint)format.Value.Length));
                if (memory == IntPtr.Zero) throw new Exception("Clipboard restore allocation failed.");
                try
                {
                    IntPtr pointer = GlobalLock(memory); if (pointer == IntPtr.Zero) throw new Exception("Clipboard restore lock failed.");
                    try { Marshal.Copy(format.Value, 0, pointer, format.Value.Length); }
                    finally { GlobalUnlock(memory); }
                    if (SetClipboardData(format.Key, memory) == IntPtr.Zero) throw new Exception("Clipboard format restore failed.");
                    memory = IntPtr.Zero;
                }
                finally { if (memory != IntPtr.Zero) GlobalFree(memory); }
            }
        }
        finally { if (opened) CloseClipboard(); DestroyWindow(owner); }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr data);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr data);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr data);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint exStyle, string className, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
}
