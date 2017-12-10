using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NLog.Contrib.Targets.LogEntries
{
    // Lazy singleton
    internal sealed class LogEntriesConnectionManager
    {
        static Lazy<LogEntriesConnectionManager> _instance
            = new Lazy<LogEntriesConnectionManager>(() => new LogEntriesConnectionManager(), true);

        public static LogEntriesConnectionManager Instance 
            => _instance.Value;

        readonly BlockingCollection<Tuple<byte[], string>> _queue;
        readonly Thread _thread;

        LogEntriesConnection _connection;
        int _closed = 0;
        
        private LogEntriesConnectionManager()
        {
            _queue = new BlockingCollection<Tuple<byte[], string>>(new ConcurrentQueue<Tuple<byte[], string>>(), 100000);
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
        
        private void ConsumeAndSendEvents()
        {
            foreach (var datas in _queue.GetConsumingEnumerable())
            {
                DoWithRetry(datas.Item1, datas.Item2);                
            }
        }

        private void DoWithRetry(byte[] token, string entry)
        {
            var retry = 0;
            var error = (Exception)null;
            while (!DoWithReconnect(token, entry, out  error))
            {
                Thread.Sleep(Math.Min(1000, 100 * retry));
                retry++;
                if (retry >= 20)
                {
                    // At least try to signal that the component was unable to save
                    // the entry. In case it is because this component fault, the error
                    // would be recorded somewhere.
                    error = error ?? new Exception("No error provided");
                    DoWithReconnect(token, $"[NLog.Contrib.Targets.LogEntries] Entry Dropped because: ({error.GetType().Name}): {error.Message} \n {error.StackTrace}", out error);
                    return;
                }
            }
        }

        private bool DoWithReconnect(byte[] token, string entry, out Exception error)
        {
            var success = false;
            error = null;
            try
            {
                if (!_connection.IsIdleForTooLong)
                {
                    LogEntryWriter.Write(token, entry, _connection);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

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
            while (_connection == null && _closed == 0)
            {
                try
                {
                    _connection = new LogEntriesConnection();
                }
                catch(Exception) // Unable to connect to Logentries.
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
