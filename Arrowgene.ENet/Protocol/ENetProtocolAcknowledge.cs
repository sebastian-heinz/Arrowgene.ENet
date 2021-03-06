﻿using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolAcknowledge
    {
        public const int Size = 8;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;

        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(4)]
        public ushort ReceivedReliableSequenceNumber;

        [MarshalAs(UnmanagedType.U2)]
        [FieldOffset(6)]
        public ushort ReceivedSentTime;
    }
}
