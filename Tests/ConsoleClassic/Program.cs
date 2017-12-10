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
                Thread.Sleep(1000);
            }
            Console.WriteLine("End");
            Console.ReadKey();
        }

        static Random ran = new Random(); // no seed
        private static string RandomMessage()
        {
            var builder = new StringBuilder("<START>");

            var length = ran.Next(10, 1000);
            for (int i = 0; i < length; i++)
            {
                var charSelector = ran.Next(0, 3);
                switch (charSelector)
                {
                    case 0:
                        builder.Append((char)ran.Next(1024, 1280)); // cyrillic
                        break;
                    case 1:
                        builder.Append((char)ran.Next(12352, 12448)); // japanese
                        break;
                    default:
                        var c = ran.Next(32, 128);
                        c = c == 34 ? 35 : c; // avoid double quotation mark
                        builder.Append((char)c);
                        break;
                }
                
                if(i % 7 == 0)
                    builder.Append(" ");
                else if (i % 13 == 0)
                    builder.Append("\r\n");
                else if (i % 23 == 0)
                    builder.Append("\n");
                else if (i % 42 == 0)
                    builder.Append("\r");
            }
            builder.Append(" <END>");
            return builder.ToString();
        }
    }
}
