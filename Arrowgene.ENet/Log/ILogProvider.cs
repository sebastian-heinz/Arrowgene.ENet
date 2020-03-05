using System;

namespace Arrowgene.Enet.Log
{
    public interface ILogProvider
    {
        ILogger GetLogger(Type clazz);
    }
}
