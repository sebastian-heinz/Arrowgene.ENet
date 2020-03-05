using Arrowgene.ENet.Protocol;

namespace Arrowgene.ENet
{
    public class ENetIncomingCommand
    {
        //  ENetListNode     incomingCommandList;
        public ushort ReliableSequenceNumber { get; set; }
        public ushort UnreliableSequenceNumber { get; set; }
        public ENetProtocol Command { get; set; }
        public uint FragmentCount { get; set; }
        public uint FragmentsRemaining { get; set; }
        public uint[] Fragments { get; set; }
        public ENetPacket Packet { get; set; }
    }
}
