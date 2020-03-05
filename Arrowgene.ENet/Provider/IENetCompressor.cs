namespace Arrowgene.ENet
{
    public interface IENetCompressor
    {
        object Context { get; }

        /**
         * Compresses from inBuffers[0:inBufferCount-1],
         * containing inLimit bytes, to outData, outputting at most outLimit bytes.
         * Should return 0 on failure.
         */
        uint Compress(ENetBuffer[] inBuffers, uint bufferIndex, uint inBufferCount, uint inLimit, out byte[] outData, uint outLimit);

        /**
         * Decompresses from inData, containing inLimit bytes, to outData, outputting at most outLimit bytes.
         * Should return 0 on failure.
         */
        uint Decompress(byte inData, uint inLimit, out byte[] outData, uint outLimit);
    }
}
