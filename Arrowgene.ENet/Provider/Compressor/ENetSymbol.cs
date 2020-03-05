namespace Arrowgene.ENet.Provider.Compressor
{
    public struct ENetSymbol
    {
        /* binary indexed tree of symbols */
       public byte value;
       public  byte count;
       public  ushort under;
       public  ushort left;
       public ushort right;

        /* context defined by this symbol */
        public  ushort symbols;
        public  ushort escapes;
        public  ushort total;
        public   ushort parent;
    }
}
