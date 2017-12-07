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

        byte[] _token;
        LogEntriesConnectionManager _connection;

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            ConfigureToken();
            _connection = LogEntriesConnectionManager.Instance;
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
            _connection.Send(_token, base.Layout.Render(logEvent));
        }

        protected override void CloseTarget()
        {
            base.CloseTarget();
            _connection?.Close();
        }
    }
}
