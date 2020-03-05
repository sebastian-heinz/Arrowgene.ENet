using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolThrottleConfigure
    {        
        public const int Size = 16;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(4)]
        public uint PacketThrottleInterval;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(8)]
        public uint PacketThrottleAcceleration;
        
        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(12)]
        public uint PacketThrottleDeceleration;
    }
}
