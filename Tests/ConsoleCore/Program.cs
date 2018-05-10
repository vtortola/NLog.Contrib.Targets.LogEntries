using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;

namespace ConsoleCore
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(System.Console.Out));

            NLog.LogManager.LoadConfiguration("nlog.config");
            var lfactory = new LoggerFactory();
            lfactory
                .AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            
            var loggerA = lfactory.CreateLogger("LoggerA");
            var loggerB = lfactory.CreateLogger("LoggerB");

            var randomLongString = string.Empty.PadLeft(500, '#');

            var total = 1000;
            for (int i = 0; i < total; i++)
            {
                loggerA.LogInformation($"ZZ TargetA {i:000000} of {total:000000} {Guid.NewGuid()} {Environment.OSVersion} {randomLongString}");
                loggerB.LogInformation($"ZZ TargetB {i:000000} of {total:000000} {Guid.NewGuid()} {Environment.OSVersion} {randomLongString}");
            }

            Thread.Sleep(5000);

            for (int i = 0; i < total; i++)
            {
                loggerA.LogInformation($"ZZ TargetA {i:000000} of {total:000000} {Guid.NewGuid()} {Environment.OSVersion} {randomLongString}");
                loggerB.LogInformation($"ZZ TargetB {i:000000} of {total:000000} {Guid.NewGuid()} {Environment.OSVersion} {randomLongString}");
            }

            Console.WriteLine("end");
            Console.ReadKey(true);
        }
    }
}
