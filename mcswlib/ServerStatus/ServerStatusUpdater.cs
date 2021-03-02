using System;
using System.Threading;
using System.Threading.Tasks;
using mcswlib.ServerStatus.ServerInfo;

namespace mcswlib.ServerStatus
{
    public class ServerStatusUpdater : IDisposable
    {
        // tries before a server is determined offline
        public static int Retries = 3;
        public static int RetryMs = 3000;

        internal ServerStatusUpdater() { }
        

        public string Address { get; set; }
        public int Port { get; set; }
        

        public ServerInfoBase Latest;

        public EventHandler<ServerInfoBase> UpdatedEvent;


        /// <summary>
        ///     This method will ping the server to request infos.
        ///     This is done in context of a task and 10 second timeout
        /// </summary>
        public void Execute(int timeOutMs = 30000)
        {
            var tSpan = TimeSpan.FromMilliseconds(timeOutMs);
            try
            {
                using var tokenSource = new CancellationTokenSource(tSpan);
                var token = tokenSource.Token;
                var task = Task.Run(() => Execute(token));
                task.Wait(token);
                Latest = task.Result;
                if (Latest == null) throw new Exception("null");
                UpdatedEvent?.Invoke(this, Latest);
            }
            catch (Exception e)
            {
                Logger.WriteLine("Execute Error? [" + Address + ":" + Port + "]: " + e);
                Latest = new ServerInfoBase(DateTime.Now - tSpan, e);
            }
        }


        private ServerInfoBase Execute(CancellationToken ct)
        {
            var srv = "[" + Address + ":" + Port + "]";
            Logger.WriteLine("Pinging server " + srv);
            // safety-wrapper
            try
            {
                // current server-info object
                var dt = DateTime.Now;
                ServerInfoBase current = null;
                var si = new ServerInfo.ServerInfo(Address, Port);
                for (var r = 0; r < Retries; r++)
                {
                    current = si.GetAsync(ct, dt).Result;
                    if (current.HadSuccess || ct.IsCancellationRequested) break;
                    Task.Delay(RetryMs).Wait(ct);
                }

                // if the result is null, nothing to do here
                if (current != null)
                {
                    Logger.WriteLine("Execute result: " + srv + " is: " + current.HadSuccess + " Err: " + current.LastError, Types.LogLevel.Debug);
                    return current;
                }

                Logger.WriteLine("Execute result null: " + srv, Types.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Fatal Error when Pinging... " + ex, Types.LogLevel.Error);
            }
            return null;
        }
        

        /// <summary>
        ///     Dispose all gathered elements explicitly
        /// </summary>
        public void Dispose()
        {
            Latest?.Dispose();
        }
    }
}