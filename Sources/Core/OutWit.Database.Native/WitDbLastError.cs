using System.Runtime.InteropServices;
using System.Text;

namespace OutWit.Database.Native;

internal static class WitDbLastError
{
    private const int MaxBytes = 4096;

    private static readonly Lock s_gate = new();
    private static string? s_message;
    private static byte[]? s_buffer;
    private static GCHandle s_pin;

    public static void Set(string? message)
    {
        lock (s_gate)
        {
            s_message = message;
        }
    }

    public static string? GetMessage()
    {
        lock (s_gate)
        {
            return s_message;
        }
    }

    public static IntPtr GetUtf8Pointer()
    {
        lock (s_gate)
        {
            var msg = s_message ?? string.Empty;
            s_buffer ??= new byte[MaxBytes];
            if (!s_pin.IsAllocated)
            {
                s_pin = GCHandle.Alloc(s_buffer, GCHandleType.Pinned);
            }

            var len = Encoding.UTF8.GetBytes(msg, 0, msg.Length, s_buffer, 0);
            if (len >= MaxBytes)
            {
                len = MaxBytes - 1;
            }

            s_buffer[len] = 0;
            return s_pin.AddrOfPinnedObject();
        }
    }
}
