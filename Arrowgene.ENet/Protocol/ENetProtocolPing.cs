using System.Runtime.InteropServices;

namespace Arrowgene.ENet.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
    public struct ENetProtocolPing
    {
        public const int Size = 4;
        
        [FieldOffset(0)]
        public ENetProtocolCommandHeader Header;
    }
}
