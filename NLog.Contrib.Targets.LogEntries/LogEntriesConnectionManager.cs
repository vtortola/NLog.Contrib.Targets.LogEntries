using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace NLog.Contrib.Targets.LogEntries
{
    internal sealed class LogEntriesConnectionManager
    {
        static Lazy<LogEntriesConnectionManager> _instance
            = new Lazy<LogEntriesConnectionManager>(() => new LogEntriesConnectionManager(), true);

        public static LogEntriesConnectionManager Instance 
            => _instance.Value;

        readonly BlockingCollection<Tuple<byte[], string>> _queue;
        readonly Thread _thread;
        readonly byte[] _buffer;
        readonly char[] _charBuffer;
        readonly Encoder _encoding;

        LogEntriesConnection _connection;
        int _bufferLength = 0;
        int _closed = 0;
        
        private LogEntriesConnectionManager()
        {
            _buffer = new byte[8192];
            _charBuffer = new char[8192];
            _queue = new BlockingCollection<Tuple<byte[], string>>(new ConcurrentQueue<Tuple<byte[], string>>(), 100000);
            _encoding = Encoding.UTF8.GetEncoder();
            _thread = new Thread(new ThreadStart(SendEventsSafeLoop))
            {
                IsBackground = true
            };
            Reconnect();
            _thread.Start();
        }

        public void Send(byte[] token, string line)
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.Add(new Tuple<byte[], string>(token, line));
            }
        }

        public void Close()
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) == 0)
            {
                _queue.CompleteAdding();
                _thread.Join();
                SendBufferWithRetry();
                _connection.Dispose();
            }
        }

        private void SendEventsSafeLoop()
        {
            while (!_queue.IsCompleted)
            {
                try
                {
                    ConsumeAndSendEvents();
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        static char[] _newLine = "\u2028".ToCharArray();

        private int BufferChars(string line, int offset)
        {
            int count = 0;
            var bufferPos = 0;
            for (; offset < line.Length; offset++)
            {
                if (_charBuffer.Length == bufferPos)
                    break;

                if(line[offset] == '\n')
                {
                    if (_charBuffer.Length - _bufferLength < _newLine.Length)
                        break;

                    for (int i = 0; i < _newLine.Length; i++)
                    {
                        _charBuffer[bufferPos++] = _newLine[i];
                    }
                }
                else if ( offset+1<line.Length && line[offset] == '\r' && line[offset+1] == '\n')
                {
                    if (_charBuffer.Length - _bufferLength < _newLine.Length)
                        break;

                    for (int i = 0; i < _newLine.Length; i++)
                    {
                        _charBuffer[bufferPos++] = _newLine[i];
                    }
                    offset++;
                    count++;
                }
                count++;
            }
            return count;
        }

        private void ConsumeAndSendEvents()
        {
            foreach (var datas in _queue.GetConsumingEnumerable())
            {
                var encoded = 0;
                while(encoded != datas.Item2.Length)
                {
                    encoded = BufferChars(datas.Item2, encoded);

                    _encoding.Convert(_charBuffer, 0, encoded,  )

                }

                var entry = Encoding.UTF8.GetBytes(Format(datas.Item2) + "\n");
                var entryLength = datas.Item1.Length + entry.Length;


                
                
                // empty buffer if no enough space available
                if (_buffer.Length - _bufferLength < entryLength)
                {
                    SendBufferWithRetry();
                }

                // log entry bigger than buffer
                if (_buffer.Length < entryLength)
                {  
                    DoWithRetry(() => 
                    {
                        _connection.Send(datas.Item1, datas.Item1.Length);
                        _connection.Send(entry, entry.Length);
                    });
                }
                else
                {
                    Buffer(datas.Item1);
                    Buffer(entry);
                }

                // if there are no pending items in the queue
                // send buffer immediately
                if(_bufferLength > 0 && _queue.Count == 0)
                    SendBufferWithRetry();
            }
        }

        private void SendBufferWithRetry()
        {
            if (_bufferLength == 0 )
                return;

            DoWithRetry(() => _connection.Send(_buffer, _bufferLength));
            _bufferLength = 0;
        }

        private void DoWithRetry(Action action)
        {
            var retry = 0;
            while (!DoWithReconnect(action))
            {
                Thread.Sleep(Math.Min(1000, 100 * retry));
                retry++;
                if (retry >= 20)
                {
                    return;
                }
            }
        }

        private void Buffer(byte[] data)
        {
            Array.Copy(data, 0, _buffer, _bufferLength, data.Length);
            _bufferLength += data.Length;
        }

        private bool DoWithReconnect(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception)
            {
                Reconnect();
                return false;
            }
        }

        private void Reconnect()
        {
            var connection = _connection;
            _connection = null;
            connection?.Dispose();
            while (_connection == null)
            {
                try
                {
                    _connection = new LogEntriesConnection();
                }
                catch(Exception)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
