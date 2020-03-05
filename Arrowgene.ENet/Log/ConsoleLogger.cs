using System;

namespace Arrowgene.Enet.Log
{
    public class ConsoleLogger : ILogger
    {
        private Type _clazz;

        public ConsoleLogger(Type clazz)
        {
            _clazz = clazz;
        }

        public void Debug(string msg)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[Debug][{DateTime.Now:HH:mm:ss}][{_clazz.Name}] {msg}");
            Console.ResetColor();
        }

        public void Info(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Info][{DateTime.Now:HH:mm:ss}][{_clazz.Name}] {msg}");
            Console.ResetColor();
        }

        public void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error][{DateTime.Now:HH:mm:ss}][{_clazz.Name}] {msg}");
            Console.ResetColor();
        }

        public void Exception(string msg, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"[Exception][{DateTime.Now:HH:mm:ss}][{_clazz.Name}] {msg} ({ex})");
            Console.ResetColor();
        }
    }
}
