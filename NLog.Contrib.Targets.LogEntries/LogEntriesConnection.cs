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
            // no naggle algorithm
            client.NoDelay = true;

            // When using the internal buffer, component buffers the data but connection
            // maybe already dead, so it would not be possible to retry the entry
            // in case of exception.
            client.SendBufferSize = 0;

            // Timeout for Socket.Send
            client.SendTimeout = 400;
        }

        internal void Send(byte[] data, int count)
        {
            if (!IsConnected())
                throw new InvalidOperationException("Unable to connect to LogEntries.");

            _stream.Write(data, 0, count);
        }

        private bool IsConnected()
        {
            if (_connecting.Status == TaskStatus.RanToCompletion)
                return _socket.Connected;
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
