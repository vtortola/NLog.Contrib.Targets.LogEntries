using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        readonly int _capacityUpperThreshold = (int)(LogEntriesSettings.BufferCapacity * .8);
        readonly int _capacityLowerThreshold = (int)(LogEntriesSettings.BufferCapacity * .4);

        int _closed = 0;
        int _additionalThreadCount = 0;
        DateTime _lastAddedThread = DateTime.Now;
               
        private LogEntriesConnectionManager()
        {
            _queue = new BlockingCollection<Tuple<byte[], string>>(new ConcurrentQueue<Tuple<byte[], string>>(), LogEntriesSettings.BufferCapacity);
            _thread = new Thread(new ThreadStart(SendEventsSafeLoop)){ IsBackground = true };
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
            }
        }

        private void SendEventsSafeLoop()
        {
            while (!_queue.IsCompleted)
            {
                try
                {
                    MasterConsumeAndSendEvents();
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception) { }
            }
        }

        private void MasterConsumeAndSendEvents()
        {
            LogEntriesConnection connection = null;
            try
            {
                Reconnect(ref connection);
                foreach (var datas in _queue.GetConsumingEnumerable())
                {
                    DoWithRetry(ref connection, datas.Item1, datas.Item2);
                    UpscaleIfNeeded();
                }
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private bool CanUpscale()
        {
            if (_queue.Count < _capacityUpperThreshold)
                return false; // not need to upscale

            if (_additionalThreadCount >= LogEntriesSettings.MaxAdditionalThreads)
                return false; // maximum upscale reached

            var now = DateTime.Now;
            if (_lastAddedThread.AddSeconds(LogEntriesSettings.SecondsBetweenNewThreads) > now)
                return false; // too fast

            return true;
        }

        private void UpscaleIfNeeded()
        {
            if (!CanUpscale())
                return;

            lock (_queue)
            {
                if (!CanUpscale())
                    return;

                _lastAddedThread = DateTime.Now;

                Interlocked.Increment(ref _additionalThreadCount);
                new Thread(new ThreadStart(ConsumeAndSendEvents)){ IsBackground = true }.Start();

                Trace.WriteLine($"{DateTime.Now:HH:mm:ss.fff} Added new thread. Current count {_additionalThreadCount}. State {(_closed == 1 ? "closed" : "running")}. Queue {_queue.Count}.");
            }
        }

        private void ConsumeAndSendEvents()
        {
            LogEntriesConnection connection = null;
            try
            {
                Reconnect(ref connection);
                foreach (var datas in _queue.GetConsumingEnumerable())
                {
                    DoWithRetry(ref connection, datas.Item1, datas.Item2);
                    Trace.Write(".");
                    if (!ContinueConsuming())
                    {
                        Interlocked.Decrement(ref _additionalThreadCount);
                        Trace.WriteLine($"Removed thread. Current count {_additionalThreadCount}. State {(_closed== 1?"closed":"running")}. Queue {_queue.Count}.");
                        return;
                    }
                }
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private bool ContinueConsuming()
        {
            if (_closed == 1)
                return false; // component is closing

            if (_queue.Count <= _capacityLowerThreshold)
                return false; // queue is on good limits now

            return true;
        }

        private void DoWithRetry(ref LogEntriesConnection connection, byte[] token, string entry)
        {
            var retry = 0;
            var error = (Exception)null;
            while (!DoWithReconnect(ref connection, token, entry, out  error))
            {
                retry++;
                if (retry >= LogEntriesSettings.MaxRetries)
                {
                    // At least try to signal that the component was unable to save
                    // the entry. In case it is because this component fault, the error
                    // would be recorded somewhere.
                    error = error ?? new Exception("No error provided");
                    DoWithReconnect(ref connection, token, $"[NLog.Contrib.Targets.LogEntries] Entry Dropped because: ({error.GetType().Name}): {error.Message} \n {error.StackTrace}", out error);
                    return;
                }
            }
        }

        private bool DoWithReconnect(ref LogEntriesConnection connection, byte[] token, string entry, out Exception error)
        {
            var success = false;
            error = null;
            try
            {
                if (!connection.IsIdleForTooLong)
                {
                    LogEntryWriter.Write(token, entry, connection);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (!success)
            {
                Reconnect(ref connection);
            }

            return success;
        }

        private void Reconnect(ref LogEntriesConnection connection)
        {
            connection?.Dispose();
            connection = null;
            while (connection == null && _closed == 0)
            {
                try
                {
                    connection = new LogEntriesConnection();
                }
                catch(Exception ex) // Unable to connect to Logentries.
                {
                    Trace.WriteLine($"Connection failed: {ex.Message}.");
                    Thread.Sleep(LogEntriesSettings.PauseBetweenReconnections);
                }
            }
        }
    }
}
