using System;
using System.Threading;

namespace Arrowgene.ENet.Playground
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
        }

        public Program()
        {
        }

        public void Run()
        {
            ENetServer server = new ENetServer();
            server.Start();
            while (Console.ReadKey().Key != ConsoleKey.E)
            {
                Thread.Sleep(250);
            }

            server.Stop();
        }
    }
}
