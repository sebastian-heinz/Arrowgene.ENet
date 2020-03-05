using System.Collections.Generic;

namespace Arrowgene.ENet
{
    public class ENetChannel
    {
        public ushort OutgoingReliableSequenceNumber { get; set; }
        public ushort OutgoingUnreliableSequenceNumber { get; set; }
        public ushort UsedReliableWindows { get; set; }
        public ushort[] ReliableWindows { get; private set; }
        public ushort IncomingReliableSequenceNumber { get; set; }
        public ushort IncomingUnreliableSequenceNumber { get; set; }
        public List<ENetIncomingCommand> IncomingReliableCommands { get; set; }
        public List<ENetIncomingCommand> IncomingUnreliableCommands { get; set; }

        public ENetChannel()
        {
            ReliableWindows = new ushort[ENetPeer.ENET_PEER_RELIABLE_WINDOWS];
            IncomingReliableCommands = new List<ENetIncomingCommand>();
            IncomingUnreliableCommands = new List<ENetIncomingCommand>();
        }
    }
}
