using System;

namespace Arrowgene.Enet.Log
{
    public interface ILogger
    {
        void Debug(string msg);
        void Info(string msg);
        void Error(string msg);
        void Exception(string msg, Exception ex);
    }
}
