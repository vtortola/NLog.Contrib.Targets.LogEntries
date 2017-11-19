using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Threading;

namespace ConsoleCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var lfactory = new LoggerFactory();
            lfactory
                .AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true })
                .ConfigureNLog("nlog.config");
            
            var loggerA = lfactory.CreateLogger("LoggerA");
            var loggerB = lfactory.CreateLogger("LoggerB");

            var total = 10;
            for (int i = 0; i < total; i++)
            {
                loggerA.LogInformation($"TargetA {i:000000} of {total:000000} {Guid.NewGuid()} {Environment.OSVersion}");
                loggerB.LogInformation($"TargetB {i:000000} of {total:000000} {Guid.NewGuid()} {Environment.OSVersion}");
                Thread.Sleep(1000);
            }
            Console.WriteLine("end");
        }
    }
}
