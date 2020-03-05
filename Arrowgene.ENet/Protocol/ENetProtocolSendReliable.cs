using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolSendReliable
    {        
        public const int Size = 6;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;
        
        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(4)]
        public ushort DataLength;
    }
}
