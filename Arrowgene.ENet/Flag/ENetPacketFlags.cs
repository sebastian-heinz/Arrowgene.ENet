using System;

namespace Arrowgene.ENet
{
    [Flags]
    public enum ENetPacketFlags
    {
        /**
         * packet must be received by the target peer and resend attempts should be
         * made until the packet is delivered
         */
        ENET_PACKET_FLAG_RELIABLE = (1 << 0),

        /**
         * packet will not be sequenced with other packets
         * not supported for reliable packets
         */
        ENET_PACKET_FLAG_UNSEQUENCED = (1 << 1),

        /**
         * packet will not allocate data, and user must supply it instead
         */
        ENET_PACKET_FLAG_NO_ALLOCATE = (1 << 2),

        /**
         * packet will be fragmented using unreliable (instead of reliable) sends
         * if it exceeds the MTU
         */
        ENET_PACKET_FLAG_UNRELIABLE_FRAGMENT = (1 << 3),

        /**
         * whether the packet has been sent from all queues it has been entered into
         */
        ENET_PACKET_FLAG_SENT = (1 << 8)
    }
}
