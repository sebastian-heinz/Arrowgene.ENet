namespace Arrowgene.ENet.Common
{
    public static class Bitmask
    {
        public static bool IsSet(byte value, byte flag)
        {
            return (value & flag) == flag;
        }
    }
}
