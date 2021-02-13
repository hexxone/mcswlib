using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using mcswlib.ServerStatus.ServerInfo;

namespace mcswlib.ServerStatus
{
    public class ServerStatusUpdater : IDisposable
    {
        internal ServerStatusUpdater() { }

        // the time over which server infos are held in memory...
        public static TimeSpan ClearSpan = new TimeSpan(0, 6, 0, 0);

        // contains the received Server-Infos.
        private readonly List<ServerInfoBase> _history = new List<ServerInfoBase>();

        public string Address { get; set; }
        public int Port { get; set; }

        public ServerInfoBase[] History => _history.ToArray();


        /// <summary>
        ///     This method will ping the server to request infos.
        ///     This is done in context of a task and 30 second timeout
        /// </summary>
        public ServerInfoBase Ping(int timeOut = 30)
        {
            try
            {
                using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeOut));
                var token = tokenSource.Token;
                var task = Task.Run(() => Ping(token), token);
                task.Wait(token);
                return task.Result;
            }
            catch (Exception e)
            {
                Logger.WriteLine("Ping Timeout? [" + Address + ":" + Port + "]");
                _history.Add(new ServerInfoBase(DateTime.Now.Subtract(TimeSpan.FromSeconds(timeOut)), timeOut * 1000, new TimeoutException()));
            }
            return null;
        }


        private ServerInfoBase Ping(CancellationToken ct)
        {
            var srv = "[" + Address + ":" + Port + "]";
            Logger.WriteLine("Pinging server " + srv);
            ServerInfoBase current = null;
            // safety-wrapper
            try
            {
                // current server-info object
                for (var i = 0; i < 2; i++)
                    if ((current = GetMethod(i, ct)).HadSuccess || ct.IsCancellationRequested)
                        break;

                // if the result is null, nothing to do here
                if (current != null)
                {
                    Logger.WriteLine("Ping result " + srv + " is " + current.HadSuccess, Types.LogLevel.Debug);
                    _history.Add(current);
                    // cleanup, done
                    ClearMem();
                    return current;
                }

                Logger.WriteLine("Ping result null " + srv, Types.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Fatal Error when Pinging... " + ex, Types.LogLevel.Error);
            }
            return null;
        }

        /// <summary>
        ///     Returns the latest (successfull) ServerInfo
        /// </summary>
        /// <param name="successful">filter for the Last successfull</param>
        /// <returns></returns>
        public ServerInfoBase GetLatestServerInfo(bool successful = false)
        {
            var tmpList = new List<ServerInfoBase>();
            tmpList.AddRange(successful ? _history.FindAll(o => o.HadSuccess) : _history);
            return tmpList.Count > 0
                ? tmpList.OrderByDescending(ob => ob.RequestDate.AddMilliseconds(ob.RequestTime)).First()
                : null;
        }

        /// <summary>
        ///     This method will request the server infos for the given version/method.
        ///     it is run as task to make it cancelable
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private ServerInfoBase GetMethod(int method, CancellationToken ct)
        {
            return method switch
            {
                1 => new GetServerInfoOld(Address, Port).DoAsync(ct).Result,
                _ => new GetServerInfoNew(Address, Port).DoAsync(ct).Result
            };
        }

        /// <summary>
        ///     Remove all objects of which the Timestamp exceeds the Clearspan and run GC.
        /// </summary>
        private void ClearMem()
        {
            // TODO TEST
            _history.FindAll(o => o.RequestDate < DateTime.Now.Subtract(ClearSpan)).ForEach(d =>
            {
                _history.Remove(d);
                d.Dispose();
            });
            GC.Collect();
        }

        /// <summary>
        ///     Dispose all gathered elements explicitely
        /// </summary>
        public void Dispose()
        {
            _history.ForEach(d => d.Dispose());
        }
    }
}