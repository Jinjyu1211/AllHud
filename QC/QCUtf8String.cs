using System.Text;
using System.Runtime.InteropServices;

namespace AllHud;

/// <summary>
/// QoLBar 风格的 UTF8String 结构体，用于 ProcessChatBox 直接命令注入。
/// 与 FFXIVClientStructs 的 Utf8String 布局兼容 (0x68 字节)。
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 0x68)]
public readonly struct QCUtf8String : IDisposable {
    public const int Size = 0x68;

    public readonly nint StringPtr;
    public readonly ulong Capacity;
    public readonly ulong Length;
    public readonly ulong Unknown;
    public readonly byte IsEmpty;
    public readonly byte NotReallocated;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x40)]
    public readonly byte[] Buffer;

    public QCUtf8String(nint loc, string text) : this(loc, Encoding.UTF8.GetBytes(text)) { }

    public QCUtf8String(nint loc, byte[] text) {
        Capacity = 0x40;
        Length = (ulong)text.Length + 1;
        Buffer = new byte[Capacity];
        if (Length > Capacity) {
            StringPtr = Marshal.AllocHGlobal(text.Length + 1);
            Capacity = Length;
            Marshal.Copy(text, 0, StringPtr, text.Length);
            Marshal.WriteByte(StringPtr, text.Length, 0);
            NotReallocated = 0;
        } else {
            StringPtr = loc + 0x22;
            text.CopyTo(Buffer, 0);
            NotReallocated = 1;
        }
        IsEmpty = (byte)((Length == 1) ? 1 : 0);
        Unknown = 0;
    }

    public void Dispose() {
        if (NotReallocated == 0)
            Marshal.FreeHGlobal(StringPtr);
    }
}