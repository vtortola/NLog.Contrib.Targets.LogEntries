using NLog;
using System;

namespace ConsoleClassic
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = LogManager.GetCurrentClassLogger();
            for (int i = 0; i < 10; i++)
            {
                logger.Info($"Classic {Guid.NewGuid()}  {Environment.OSVersion}");
            }
            Console.WriteLine("End");
        }
    }
}
