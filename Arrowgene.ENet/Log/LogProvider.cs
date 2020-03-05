using System;

namespace Arrowgene.Enet.Log
{
    public class LogProvider : ILogProvider
    {
        public static void SetProvider(ILogProvider provider)
        {
            _provider = provider;
        }

        private static ILogProvider _provider = new LogProvider();

        public static ILogger Logger(Type clazz)
        {
            return _provider.GetLogger(clazz);
        }

        public ILogger GetLogger(Type clazz)
        {
            return new ConsoleLogger(clazz);
        }
    }
}
