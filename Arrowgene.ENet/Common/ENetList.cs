using System;
using System.Collections.Generic;

namespace Arrowgene.ENet.Common
{
    public class ENetList
    {
        public static int enet_list_size<T>(IEnumerable<T> list, Func<T, int> counter)
        {
            if (list == null)
            {
                return 0;
            }

            int size = 0;
            foreach (T item in list)
            {
                if (item != null)
                {
                    size += counter(item);
                }
            }

            return size;
        }
    }
}
