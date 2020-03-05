using System;
using System.Net;

namespace Arrowgene.ENet.Common
{
    public class ENetSocket
    {
        public static byte[] Combine(ENetBuffer[] buffer, uint bufferCount)
        {
            int totalSize = 0;
            for (int i = 0; i < bufferCount; i++)
            {
                totalSize += (int) buffer[i].DataLength;
            }

            byte[] totalData = new byte[totalSize];
            int currentIdex = 0;
            for (int i = 0; i < bufferCount; i++)
            {
                int dataSize = (int) buffer[i].DataLength;
                Buffer.BlockCopy(buffer[i].Data, 0, totalData, currentIdex, dataSize);
                currentIdex += dataSize;
            }

            return totalData;
        }

        public static uint ENET_HOST_TO_NET_32(uint value)
        {
            return (uint) IPAddress.HostToNetworkOrder((int) value);
        }

        public static uint ENET_NET_TO_HOST_32(uint value)
        {
            return (uint) IPAddress.NetworkToHostOrder((int) value);
        }

        public static ushort ENET_HOST_TO_NET_16(ushort value)
        {
            return (ushort) IPAddress.HostToNetworkOrder((short) value);
        }

        public static ushort ENET_NET_TO_HOST_16(ushort value)
        {
            return (ushort) IPAddress.NetworkToHostOrder((short) value);
        }

        public static bool IpEndPointEquals(IPEndPoint a, IPEndPoint b)
        {
            return a.Equals(b);
        }

        public static bool IpAddressEquals(IPAddress a, IPAddress b)
        {
            return a.Equals(b);
        }
    }
}
