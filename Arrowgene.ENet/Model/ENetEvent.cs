namespace Arrowgene.ENet
{
    public class ENetEvent
    {
        public ENetEventType Type { get; set; }
        public ENetPeer Peer { get; set; }
        public uint Data { get; set; }
        public byte ChannelId { get; set; }
        public ENetPacket Packet { get; set; }
    }
}
