using NLog.Targets;
using System;
using System.Text;

namespace NLog.Contrib.Targets.LogEntries
{
    [Target("LogEntries")]
    public class LogEntriesTarget : TargetWithLayout
    {
        public string Token { get; set; }
        public string TokenEnvVar { get; set; }

        readonly LogEntriesConnectionManager _connection;
        byte[] _token;

        public LogEntriesTarget()
        {
            _connection = LogEntriesConnectionManager.Instance;
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            ConfigureToken();
        }

        private void ConfigureToken()
        {
            if (!string.IsNullOrWhiteSpace(TokenEnvVar))
            {
                var envToken = Environment.GetEnvironmentVariable(TokenEnvVar);
                if (!string.IsNullOrWhiteSpace(envToken))
                {
                    Token = envToken;
                }
            }
            if (string.IsNullOrWhiteSpace(Token))
            {
                throw new NLogConfigurationException("The API token to connect to LogEntries is mandatory.");
            }
            _token = Encoding.UTF8.GetBytes(Token);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var entry = Encoding.UTF8.GetBytes(Format(base.Layout.Render(logEvent)));
            _connection.Send(_token, entry);
        }

        protected override void CloseTarget()
        {
            base.CloseTarget();
            _connection.Close();
        }

        static string Format(string entry)
            => entry
                .Replace("\r\n", "\u2028")
                .Replace("\n", "\u2028") + '\n';
    }
}
