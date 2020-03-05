using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolDisconnect
    {
        public const int Size = 8;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(4)]
        public uint Data;
    }
}
