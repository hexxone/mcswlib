using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mcswlib.ServerStatus
{
    public class ServerStatusFactory : IDisposable
    {
        private CancellationTokenSource _token;
        private Thread _thread;

        
        public readonly Dictionary<ServerStatusUpdater, List<ServerStatus>> Updaters = new();
        public bool AutoUpdating => _thread != null && _thread.IsAlive;
        

        /// <summary>
        ///     Start Auto-updating the servers with given Interval
        /// </summary>
        /// <param name="ms"></param>
        /// 
        public void StartAutoUpdate(int ms = 30000)
        {
            if (ms < 0) throw new Exception("Interval must be >= 0");
            StopAutoUpdate();
            _token = new CancellationTokenSource();
            _thread = new Thread(() => AutoUpdater(ms))
            {
                Name = "ServerStatusFactoryAutoUpdater"
            };
            _thread.Start();
        }

        /// <summary>
        ///     Stop Auto-Updating servers
        /// </summary>
        public void StopAutoUpdate()
        {
            if (_thread != null && _thread.IsAlive)
            {
                _token.Cancel();
                _thread.Join();
            }
            _thread = null;
        }

        /// <summary>
        ///     Execute & update all the servers and groups
        /// </summary>
        public void PingAll(int timeOutMs = 30000)
        {
            Parallel.ForEach(Updaters, Types.POptions, srv => srv.Key.Execute(timeOutMs));
        }

        /// <summary>
        ///     Will either reuse a given ServerStatusBase with same address or create one
        /// </summary>
        /// <param name="label"></param>
        /// <param name="addr"></param>
        /// <param name="port"></param>
        /// <returns>a new ServerStatus object with given params</returns>
        public ServerStatus Make(string addr, int port = 25565, bool forceNewBase = false, string label = "")
        {
            // Get ServerStatusBase or make & add one
            var exist = (forceNewBase ? null : GetByAddr(addr, port));
            var key = exist ?? new ServerStatusUpdater() { Address = addr, Port = port };
            // Make & add new status
            var entry = new ServerStatus(label, key);
            if(exist != null) Updaters[exist].Add(entry);
            else Updaters.Add(key, new List<ServerStatus>() {entry});
            return entry;
        }

        /// <summary>
        ///     Destroy a created ServerStatus object.
        ///     Will only destroy the underlying ServerStatusBase if its not used anymore.
        /// </summary>
        /// <param name="status"></param>
        /// <returns>Success indicator</returns>
        public bool Destroy(ServerStatus status)
        {
            if (!Updaters[status.Updater].Contains(status))
                return false;
            // away with it
            Updaters[status.Updater].Remove(status);
            // check if the base is still in use and if not remove it
            if (IsBeingUsed(status.Updater)) return true;
            Updaters.Remove(status.Updater);
            status.Updater.Dispose();
            // done
            return true;
        }

        /// <summary>
        ///     Destroy multiple ServersStatus objects
        /// </summary>
        /// <param name="statuses"></param>
        /// <returns></returns>
        public bool Destroy(ServerStatus[] statuses)
        {
            return statuses.Aggregate(true, (current, srv) => current & Destroy(srv));
        }

        /// <summary>
        ///     Will try to find a ServerStatusBase with given constraints
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private ServerStatusUpdater GetByAddr(string addr, int port)
        {
            return Updaters.FirstOrDefault(s => string.Equals(s.Key.Address, addr, StringComparison.CurrentCultureIgnoreCase) && s.Key.Port == port).Key;
        }

        /// <summary>
        ///     Returns whether the ServerStatusBase is used more than once
        /// </summary>
        /// <param name="ssb"></param>
        /// <returns></returns>
        private bool IsBeingUsed(ServerStatusUpdater ssb)
        {
            return Updaters[ssb].Any();
        }

        /// <summary>
        ///     Will run to repeatedly ping all servers in a separate thread.
        /// </summary>
        /// <param name="ms"></param>
        private void AutoUpdater(int ms)
        {
            while (true)
            {
                PingAll(ms);
                if (_token.IsCancellationRequested) return;
                Thread.Sleep(ms);
            }
        }

        /// <summary>
        ///     Dispose of all IDispose objects managed by this factory
        /// </summary>
        public void Dispose()
        {
            StopAutoUpdate();
            foreach (var updater in Updaters)
            {
                updater.Key.Dispose();
            }
        }
    }
}
