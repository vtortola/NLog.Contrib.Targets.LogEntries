using System;
using System.Net.Security;
using System.Net.Sockets;

namespace NLog.Contrib.Targets.LogEntries
{
    internal sealed class LogEntriesConnection : IDisposable
    {
        const string _url = "api.logentries.com";
        const int _tokenTlsPort = 20000;
        const double _maxIdleSeconds = 30;

        readonly TcpClient _socket;
        readonly SslStream _stream;

        DateTime _lastActivity;

        public bool IsIdleForTooLong => DateTime.Now.Subtract(_lastActivity).TotalSeconds > _maxIdleSeconds;

        public LogEntriesConnection()
        {
            _socket = new TcpClient(_url, _tokenTlsPort);
            ConfigureSocket(_socket);
            _stream = new SslStream(_socket.GetStream());
            _stream.AuthenticateAsClient(_url);
            _lastActivity = DateTime.Now;
        }

        static void ConfigureSocket(TcpClient socket)
        {
            // no naggle algorithm
            socket.NoDelay = true;

            // When using the internal buffer, TcpClient buffers the data but connection
            // maybe already dead, so it would not be possible to retry the entry
            // in case of exception.
            socket.SendBufferSize = 0;

            // Timeout for Socket.Send. 5 seconds because large entries takes much more time
            socket.SendTimeout = 5000;

            // keep alive
            //http://tldp.org/HOWTO/TCP-Keepalive-HOWTO/usingkeepalive.html
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }

        public void Send(byte[] data, int count)
        {
            _stream.Write(data, 0, count);
            _lastActivity = DateTime.Now;
        }

        public void Dispose()
        {
            _socket.Dispose();
            _stream?.Dispose();
        }
    }
}
