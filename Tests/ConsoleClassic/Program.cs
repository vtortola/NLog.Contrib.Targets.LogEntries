using NLog;
using System;
using System.Threading;

namespace ConsoleClassic
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = LogManager.GetCurrentClassLogger();
            var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (o, e) => cancel.Cancel();
            var counter = 0;
            while(!cancel.IsCancellationRequested)
            {
                logger.Info("BB " + counter++);
                Thread.Sleep(1000);
            }
            Console.WriteLine("End");
        }
    }
}
