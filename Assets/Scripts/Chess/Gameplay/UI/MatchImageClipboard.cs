using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.ComponentModel;
using System.Runtime.InteropServices;
#endif

/// <summary>Copies image data, never a filename or a text substitute.</summary>
public static class MatchImageClipboard
{
    // Only these application-owned messages may bypass the general OS-error
    // sanitizer. Native exceptions can contain localized or technical details.
    public sealed class CopyException : Exception
    {
        internal CopyException(string message) : base(message) { }
    }

    public static bool IsSupported
    {
        get
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            return true;
#else
            return false;
#endif
        }
    }

    public static async Task CopyPngAsync(byte[] png, CancellationToken cancellation = default)
    {
        if (png == null || png.Length < 24 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71)
            throw new CopyException("There is no result image to copy. Use Refresh and try again.");
        cancellation.ThrowIfCancellationRequested();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Color32[] pixels; int width, height;
        var image = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (!image.LoadImage(png)) throw new CopyException("The result image could not be decoded. Use Refresh and try again.");
            width = image.width; height = image.height; pixels = image.GetPixels32();
        }
        finally
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(image);
            else UnityEngine.Object.DestroyImmediate(image);
        }
        // PNG is the lossless original. CF_DIB adds compatibility with image
        // editors/chat apps that consume the standard Windows bitmap format.
        await Task.Run(() => WindowsClipboard.Copy(png, pixels, width, height, cancellation), cancellation);
#elif UNITY_ANDROID && !UNITY_EDITOR
        string path = Path.Combine(Application.temporaryCachePath, "cbw-clipboard-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await Task.Run(() => File.WriteAllBytes(path, png), cancellation);
            cancellation.ThrowIfCancellationRequested();
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.chessbutweird.share.ResultImageProvider"))
                bridge.CallStatic("copy", activity, path);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
#else
        await Task.CompletedTask;
        throw new CopyException("Image clipboard is unavailable on this platform. Use Save Image instead.");
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private static class WindowsClipboard
    {
        private const uint DibFormat = 8, MovableMemory = 2;

        public static void Copy(byte[] png, Color32[] pixels, int width, int height, CancellationToken cancellation)
        {
            int stride = checked((width * 3 + 3) & ~3);
            byte[] dib = new byte[checked(40 + stride * height)];
            using (var writer = new BinaryWriter(new MemoryStream(dib)))
            {
                writer.Write(40); writer.Write(width); writer.Write(height);
                writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0);
                writer.Write(stride * height); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            }
            // Unity pixels and positive-height DIB rows both start at the bottom.
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                Color32 color = pixels[y * width + x]; int offset = 40 + y * stride + x * 3;
                dib[offset] = color.b; dib[offset + 1] = color.g; dib[offset + 2] = color.r;
            }
            IntPtr pngMemory = IntPtr.Zero, dibMemory = IntPtr.Zero, owner = IntPtr.Zero;
            bool opened = false;
            try
            {
                uint pngFormat = RegisterClipboardFormat("PNG");
                if (pngFormat == 0) throw NativeError();
                pngMemory = Allocate(png); dibMemory = Allocate(dib);
                // A real nonvisible owner avoids OpenClipboard(NULL)'s null-owner
                // failure. All window/clipboard calls stay on this worker thread.
                owner = CreateWindowEx(0, "STATIC", "CbW Image Clipboard", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (owner == IntPtr.Zero) throw NativeError();
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (OpenClipboard(owner)) { opened = true; break; }
                    Thread.Sleep(25);
                }
                if (!opened) throw new CopyException("The clipboard is busy. Try Copy Image again.");
                cancellation.ThrowIfCancellationRequested();
                if (!EmptyClipboard()) throw NativeError();
                if (SetClipboardData(pngFormat, pngMemory) == IntPtr.Zero) throw NativeError();
                pngMemory = IntPtr.Zero; // Windows owns successful clipboard handles.
                if (SetClipboardData(DibFormat, dibMemory) == IntPtr.Zero) throw NativeError();
                dibMemory = IntPtr.Zero;
            }
            finally
            {
                if (opened) CloseClipboard();
                if (owner != IntPtr.Zero) DestroyWindow(owner);
                if (pngMemory != IntPtr.Zero) GlobalFree(pngMemory);
                if (dibMemory != IntPtr.Zero) GlobalFree(dibMemory);
            }
        }

        private static Exception NativeError() => new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not copy the result image.");
        private static IntPtr Allocate(byte[] bytes)
        {
            IntPtr handle = GlobalAlloc(MovableMemory, new UIntPtr((uint)bytes.Length));
            if (handle == IntPtr.Zero) throw NativeError();
            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) { GlobalFree(handle); throw NativeError(); }
            try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
            catch { GlobalUnlock(handle); GlobalFree(handle); throw; }
            GlobalUnlock(handle); return handle;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint RegisterClipboardFormat(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint exStyle, string className, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
    }
#endif
}
