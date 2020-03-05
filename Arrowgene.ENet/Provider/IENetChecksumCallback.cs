namespace Arrowgene.ENet
{
    public interface IENetChecksumCallback
    {
        uint Calculate(ENetBuffer[] inBuffers, uint bufferIndex, uint bufferCount);
    }
}
