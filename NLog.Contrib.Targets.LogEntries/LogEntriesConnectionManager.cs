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

        static readonly char[] _newLineReplacement = "\u2028".ToCharArray();
        static readonly byte _newLineByte = (byte)'\n';

        readonly BlockingCollection<Tuple<byte[], string>> _queue;
        readonly Thread _thread;
        readonly byte[] _buffer;
        readonly char[] _charBuffer;
        readonly Encoder _encoding;

        LogEntriesConnection _connection;
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
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception) { }
            }
        }
        
        private int BufferChars(string line, int offset)
        {
            int count = 0;
            var bufferPos = 0;

            for (; offset < line.Length; offset++)
            {
                if (_charBuffer.Length == bufferPos)
                    break;

                if(line[offset] == '\n' || line[offset] == '\r')
                {
                    if (_charBuffer.Length - bufferPos < _newLineReplacement.Length)
                        break;

                    for (int i = 0; i < _newLineReplacement.Length; i++)
                    {
                        _charBuffer[bufferPos++] = _newLineReplacement[i];
                    }

                    if (offset + 1 < line.Length && line[offset] == '\r' && line[offset] == '\n')
                    {
                        offset++;
                        count++;
                    }
                }
                else
                {
                    _charBuffer[bufferPos++] = line[offset];
                }
                count++;
            }
            return count;
        }

        private void ConsumeAndSendEvents()
        {
            foreach (var datas in _queue.GetConsumingEnumerable())
            {
                DoWithRetry(() => SendLogEntry(datas.Item1, datas.Item2));                
            }
        }

        private void SendLogEntry(byte[] token, string entry)
        {
            Array.Copy(token, 0, _buffer, 0, token.Length);

            var buffered = token.Length;
            var totalformatted = 0;
            while (totalformatted != entry.Length)
            {
                var formatted = BufferChars(entry, totalformatted);
                totalformatted += formatted;

                var completed = false;
                var charsUsed = 0;
                var bytesUsed = 0;

                while (!completed)
                {
                    _encoding.Convert(
                                chars: _charBuffer,
                                charIndex: charsUsed, 
                                charCount: formatted - charsUsed, 
                                bytes: _buffer,
                                byteIndex: buffered,
                                byteCount: _buffer.Length - buffered,
                                flush: formatted == charsUsed,
                                charsUsed: out charsUsed,
                                bytesUsed: out bytesUsed,
                                completed: out completed);

                    buffered += bytesUsed;

                    if (completed && totalformatted == entry.Length)
                    {
                        if(buffered < _buffer.Length)
                        {
                            _buffer[buffered++] = _newLineByte;
                        }
                        else
                        {
                            _connection.Send(_buffer, buffered);
                            _buffer[0] = _newLineByte;
                            buffered = 1;
                        }
                    }
                    
                    _connection.Send(_buffer, buffered);
                    buffered = 0;
                }               
            }
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

        private bool DoWithReconnect(Action action)
        {
            var success = false;
            try
            {
                if (!_connection.IsIdleForTooLong)
                {
                    action();
                    success = true;
                }
            }
            catch (Exception) { }

            if (!success)
            {
                Reconnect();
            }

            return success;
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
