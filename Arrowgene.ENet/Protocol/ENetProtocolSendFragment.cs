using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolSendFragment
    {
        public const int Size = 24;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;
        
        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(4)]
        public ushort StartSequenceNumber;
        
        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(6)]
        public ushort DataLength;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(8)]
        public uint FragmentCount;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(12)]
        public uint FragmentNumber;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(16)]
        public uint TotalLength;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(20)]
        public uint FragmentOffset;
    }
}
