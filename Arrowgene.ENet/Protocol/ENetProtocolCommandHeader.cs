using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolCommandHeader
    {        
        public const int Size = 4;
        
        [MarshalAs(UnmanagedType.U1)]
        [FieldOffset(0)]
        public byte Command;
        
        [MarshalAs(UnmanagedType.U1)]
        [FieldOffset(1)]
        public byte ChannelId;
        
        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(2)]
        public ushort ReliableSequenceNumber;
    }
}
