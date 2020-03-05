using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolBandwidthLimit
    {        
        public const int Size = 12;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(4)]
        public uint IncomingBandwidth;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(8)]
        public uint OutgoingBandwidth;
    }
}
