using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NLog.Contrib.Targets.LogEntries
{
    internal sealed class LogEntriesConnectionManager
    {
        static Lazy<LogEntriesConnectionManager> _instance
            = new Lazy<LogEntriesConnectionManager>(() => new LogEntriesConnectionManager(), true);

        public static LogEntriesConnectionManager Instance 
            => _instance.Value;

        readonly BlockingCollection<byte[][]> _queue;
        readonly Thread _thread;
        readonly byte[] _buffer;

        LogEntriesConnection _connection;
        int _bufferLength = 0;
        int _closed = 0;
        
        private LogEntriesConnectionManager()
        {
            _buffer = new byte[8192];
            _queue = new BlockingCollection<byte[][]>(new ConcurrentQueue<byte[][]>(), 100000);
            _thread = new Thread(new ThreadStart(SendEventsSafeLoop))
            {
                IsBackground = true
            };
            Reconnect();
            _thread.Start();
        }

        public void Send(params byte[][] datas)
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.Add(datas);
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

        private void ConsumeAndSendEvents()
        {
            foreach (var datas in _queue.GetConsumingEnumerable())
            {
                // empty buffer if no enough space available
                if (_buffer.Length - _bufferLength < datas[0].Length + datas[1].Length)
                {
                    SendBufferWithRetry();
                }

                // very long log entry
                if (_buffer.Length < datas[0].Length + datas[1].Length)
                {  
                    DoWithRetry(() => 
                    {
                        foreach (var data in datas)
                            _connection.Send(data, data.Length);
                    });
                }
                else
                {
                    Buffer(datas[0]);
                    Buffer(datas[1]);
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
                Thread.Sleep(Math.Min(Math.Max(0, 100 * retry), 2000));
                unchecked
                {
                    retry++;
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
                    // why would this happen?
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
