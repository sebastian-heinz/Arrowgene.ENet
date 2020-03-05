using Arrowgene.ENet.Protocol;

namespace Arrowgene.ENet
{
    public class ENetAcknowledgement
    {
        public uint  SentTime { get; set; }
        public ENetProtocol Command { get; set; }
    }
}
