using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolHeader
    {
        public const int Size = 4;
        public const int SizeWithoutSentTime = 2;
        public const int SizeOfChecksum = 4;
        
        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(0)]
        public ushort PeerId;
        
        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(2)]
        public ushort SentTime;
    }
}
