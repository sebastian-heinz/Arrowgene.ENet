namespace Arrowgene.ENet
{
    public enum ENetEventType
    {
        /**
         * no event occurred within the specified time limit
         */
        ENET_EVENT_TYPE_NONE = 0,

        /**
         * a connection request initiated by enet_host_connect has completed.
         * The peer field contains the peer which successfully connected.
         */
        ENET_EVENT_TYPE_CONNECT = 1,

        /**
         * a peer has disconnected.  This event is generated on a successful
         * completion of a disconnect initiated by enet_peer_disconnect, if
         * a peer has timed out, or if a connection request intialized by
         * enet_host_connect has timed out.  The peer field contains the peer
         * which disconnected. The data field contains user supplied data
         * describing the disconnection, or 0, if none is available.
         */
        ENET_EVENT_TYPE_DISCONNECT = 2,

        /**
         * a packet has been received from a peer.  The peer field specifies the
         * peer which sent the packet.  The channelID field specifies the channel
         * number upon which the packet was received.  The packet field contains
         * the packet that was received; this packet must be destroyed with
         * enet_packet_destroy after use.
         */
        ENET_EVENT_TYPE_RECEIVE = 3
    }
}
