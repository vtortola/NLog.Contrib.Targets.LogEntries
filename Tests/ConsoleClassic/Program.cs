using NLog;
using NLog.Fluent;
using System;
using System.Diagnostics;
using System.Text;
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
            var run = Guid.NewGuid().ToString("N");
            logger.Info($"Starging test {run}.");
            var sw = new Stopwatch();
            while (!cancel.IsCancellationRequested)
            {
                sw.Start();
                var message = RandomMessage();
                var gen = sw.ElapsedMilliseconds;
                logger
                     .Info()
                     .Property("run", run)
                     .Property("length", message.Length)
                     .Property("count", counter++)
                     .Message(message)
                     .Write();
                var write = sw.ElapsedMilliseconds;
                sw.Reset();
                Console.WriteLine($"IT:{counter}, gen:{gen:0.00}, write:{write:0.00}");
                Thread.Sleep(100);
            }
            Console.WriteLine("End");
            Console.ReadKey();
        }

        static Random ran = new Random(); // no seed
        private static string RandomMessage()
        {
            var builder = new StringBuilder();

            var length = ran.Next(10, 10000);
            for (int i = 0; i < length; i++)
            {
                builder.Append((char)ran.Next(32, 128));
                if (i % 47 == 0)
                    builder.Append("\r\n");
                else if (i % 43 == 0)
                    builder.Append("\n");
                else if (i % 41 == 0)
                    builder.Append("\r");
            }
            return builder.ToString();
        }
    }
}
