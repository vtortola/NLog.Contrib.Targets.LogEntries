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

        LogEntriesConnection _connection;
        int _closed = 0;

        private LogEntriesConnectionManager()
        {
            _queue = new BlockingCollection<byte[][]>(new ConcurrentQueue<byte[][]>(), 10000);
            _thread = new Thread(new ThreadStart(SendEventsSafeLoop))
            {
                IsBackground = true
            };
            Reconnect();
            _thread.Start();
        }

        public void Send(params byte[][] datas)
        {
            _queue.Add(datas);
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
                var retry = 0;
                while (!SendEntry(datas))
                {
                    Thread.Sleep(Math.Min(100 * retry, 1000));
                    unchecked
                    {
                        retry++;
                    }
                }
            }
        }

        private bool SendEntry(byte[][] datas)
        {
            try
            {
                _connection.Send(datas);
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
            _connection?.Dispose();
            _connection = new LogEntriesConnection();
        }
    }
}
