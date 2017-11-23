using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NLog.Contrib.Targets.LogEntries
{
    internal sealed class LogEntriesConnection : IDisposable
    {
        const string _url = "api.logentries.com";
        const int _tokenTlsPort = 20000;

        readonly TcpClient _socket;
        readonly Task _connecting;
        volatile SslStream _stream;

        public LogEntriesConnection()
        {
            _socket = new TcpClient();
            ConfigureSocket(_socket);
            _connecting =
                _socket
                   .ConnectAsync(_url, _tokenTlsPort)
                   .ContinueWith(t =>
                   {
                       _stream = new SslStream(_socket.GetStream());
                       _stream.AuthenticateAsClient(_url);
                   });
        }

        static void ConfigureSocket(TcpClient client)
        {
            client.NoDelay = true;
        }

        internal void Send(byte[][] datas)
        {
            if (!IsConnected())
                throw new InvalidOperationException("Unable to connect to LogEntries.");

            foreach (var data in datas)
                _stream.Write(data, 0, data.Length);
        }

        private bool IsConnected()
        {
            if (_connecting.Status == TaskStatus.RanToCompletion)
                return true;
            if (_connecting.IsCanceled)
                throw new TaskCanceledException();
            if (_connecting.IsFaulted)
                throw _connecting.Exception;

            return _connecting.Wait(2000);
        }

        public void Dispose()
        {
            _socket.Dispose();
            _stream?.Dispose();
        }
    }
}
