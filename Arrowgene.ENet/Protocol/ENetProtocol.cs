﻿using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ENetProtocol
    {
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;

        [FieldOffset(0)]
        public ENetProtocolAcknowledge Acknowledge;

        [FieldOffset(0)]
        public ENetProtocolConnect Connect;

        [FieldOffset(0)]
        public ENetProtocolVerifyConnect VerifyConnect;

        [FieldOffset(0)]
        public ENetProtocolDisconnect Disconnect;

        [FieldOffset(0)]
        public ENetProtocolPing Ping;

        [FieldOffset(0)]
        public ENetProtocolSendReliable SendReliable;

        [FieldOffset(0)]
        public ENetProtocolSendUnreliable SendUnreliable;

        [FieldOffset(0)]
        public ENetProtocolSendUnsequenced SendUnsequenced;

        [FieldOffset(0)]
        public ENetProtocolSendFragment SendFragment;

        [FieldOffset(0)]
        public ENetProtocolBandwidthLimit BandwidthLimit;

        [FieldOffset(0)]
        public ENetProtocolThrottleConfigure ThrottleConfigure;
    }
}
