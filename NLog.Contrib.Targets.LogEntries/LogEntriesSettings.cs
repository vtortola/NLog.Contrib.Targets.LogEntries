namespace NLog.Contrib.Targets.LogEntries
{
    public sealed class LogEntriesSettings
    {
        public static string ApiUrl = "api.logentries.com";
        public static int TokenTlsPort = 20000;
        public static int MaxIdleSeconds = 30;

        public static int MaxRetries = 20;
        public static int BufferCapacity = 10000;
        public static int PauseBetweenReconnections = 1000;
        public static int SecondsBetweenNewThreads = 5;
        public static int MaxAdditionalThreads = 4;

        public static int SocketBufferSize = 8192;
        public static int SocketTimeoutSeconds = 2;
    }
}
