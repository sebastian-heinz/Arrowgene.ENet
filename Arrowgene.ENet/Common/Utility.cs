using System;
using System.Text;

namespace Arrowgene.ENet.Common
{
    public class Utility
    {
               
        public static string ToHexString(byte[] data, string separator = null)
        {
            StringBuilder sb = new StringBuilder();
            int len = data.Length;
            for (int i = 0; i < len; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (separator != null && i < len - 1)
                {
                    sb.Append(separator);
                }
            }
            return sb.ToString();
        }

        public static string ToAsciiString(byte[] data, string separator = "  ")
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                char c = '.';
                if (data[i] >= 'A' && data[i] <= 'Z') c = (char) data[i];
                if (data[i] >= 'a' && data[i] <= 'z') c = (char) data[i];
                if (data[i] >= '0' && data[i] <= '9') c = (char) data[i];
                if (separator != null && i != 0)
                {
                    sb.Append(separator);
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
        
        public static byte[] FromHexString(string hexString)
        {
            if ((hexString.Length & 1) != 0)
            {
                throw new ArgumentException("Input must have even number of characters");
            }
            byte[] ret = new byte[hexString.Length/2];
            for (int i = 0; i < ret.Length; i++)
            {
                int high = hexString[i*2];
                int low = hexString[i*2+1];
                high = (high & 0xf) + ((high & 0x40) >> 6) * 9;
                low = (low & 0xf) + ((low & 0x40) >> 6) * 9;

                ret[i] = (byte)((high << 4) | low);
            }
            return ret;
        }
    }
}
