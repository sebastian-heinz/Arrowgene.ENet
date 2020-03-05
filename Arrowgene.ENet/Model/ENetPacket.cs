namespace Arrowgene.ENet
{
    public class ENetPacket
    {
        public ENetPacket()
        {
        }

        public ENetPacket(byte[] data, uint dataLength, ENetPacketFlags flags)
        {
            Data = data;
            DataLength = dataLength;
            Flags = flags;
        }
        
        public uint ReferenceCount { get; set; }
        public ENetPacketFlags Flags { get; set; }
        public byte[] Data { get; set; }
        public uint DataLength { get; set; }
    }
}
