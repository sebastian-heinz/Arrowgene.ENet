using System;

namespace Arrowgene.ENet
{
    [Flags]
    public enum ENetSocketWait
    {
        ENET_SOCKET_WAIT_NONE = 0,
        ENET_SOCKET_WAIT_SEND = (1 << 0),
        ENET_SOCKET_WAIT_RECEIVE = (1 << 1),
        ENET_SOCKET_WAIT_INTERRUPT = (1 << 2)
    }
}
