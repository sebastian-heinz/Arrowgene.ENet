using System;
using System.Collections.Generic;

namespace Arrowgene.ENet.Common
{
    public static class Extension
    {
        public static IEnumerable<T> FastReverse<T>(this IList<T> items)
        {
            for (int i = items.Count-1; i >= 0; i--)
            {
                yield return items[i];
            }
        }
        
        public static byte[] SliceEx(this byte[] bytes, int start, int len)
        {
            return ((Span<byte>) bytes).Slice(start, len).ToArray();
        }
    }
}
