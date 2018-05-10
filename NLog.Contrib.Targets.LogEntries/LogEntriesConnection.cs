using System;
using System.Net.Security;
using System.Net.Sockets;

namespace NLog.Contrib.Targets.LogEntries
{
    internal sealed class LogEntriesConnection : IDisposable
    {
        readonly TcpClient _socket;
        readonly SslStream _stream;

        DateTime _lastActivity;

        public bool IsIdleForTooLong => DateTime.Now.Subtract(_lastActivity).TotalSeconds > LogEntriesSettings.MaxIdleSeconds;

        public LogEntriesConnection()
        {
            _socket = new TcpClient(LogEntriesSettings.ApiUrl, LogEntriesSettings.TokenTlsPort);
            ConfigureSocket(_socket);
            _stream = new SslStream(_socket.GetStream());
            _stream.AuthenticateAsClient(LogEntriesSettings.ApiUrl);
            _lastActivity = DateTime.Now;
        }

        static void ConfigureSocket(TcpClient socket)
        {
            // no naggle algorithm
            socket.NoDelay = true;

            socket.SendBufferSize = LogEntriesSettings.SocketBufferSize;

            // Timeout for Socket.Send. 5 seconds because large entries takes much more time
            socket.SendTimeout = LogEntriesSettings.SocketTimeoutSeconds * 1000;

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
