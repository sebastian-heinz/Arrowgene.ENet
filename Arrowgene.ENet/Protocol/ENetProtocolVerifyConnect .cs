using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolVerifyConnect
    {
        public const int Size = 44;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;

        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(4)]
        public ushort OutgoingPeerId;

        [MarshalAs(UnmanagedType.U1)]
        [FieldOffset(6)]
        public byte IncomingSessionId;

        [MarshalAs(UnmanagedType.U1)]
        [FieldOffset(7)]
        public byte OutgoingSessionId;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(8)]
        public uint Mtu;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(12)]
        public uint WindowSize;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(16)]
        public uint ChannelCount;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(20)]
        public uint IncomingBandwidth;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(24)]
        public uint OutgoingBandwidth;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(28)]
        public uint PacketThrottleInterval;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(32)]
        public uint PacketThrottleAcceleration;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(36)]
        public uint PacketThrottleDeceleration;

        [MarshalAs(UnmanagedType.U4)]
        [FieldOffset(40)]
        public uint ConnectId;
    }
}
