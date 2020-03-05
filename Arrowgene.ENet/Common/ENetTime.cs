using System;

namespace Arrowgene.ENet.Common
{
    public class ENetTime
    {
        public const uint ENET_TIME_OVERFLOW = 86400000;

        private uint _timeBase;

        public ENetTime()
        {
            _timeBase = 0;
        }

        public uint Difference(uint a, uint b)
        {
            return a - b >= ENET_TIME_OVERFLOW ? b - a : a - b;
        }

        public bool GreaterEqual(uint a, uint b)
        {
            return !Less(a, b);
        }

        public bool Less(uint a, uint b)
        {
            return a - b >= ENET_TIME_OVERFLOW;
        }

        public uint GetTime()
        {
            return Time() - _timeBase;
        }

        public void SetTime(uint newTimeBase)
        {
            _timeBase = Time() - newTimeBase;
        }

        private uint Time()
        {
            return (uint)Environment.TickCount;
            //  return (uint) DateTime.UtcNow.Ticks;
        }
    }
}
