using mcswlib.ServerStatus.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mcswlib.ServerStatus
{
    public class ServerStatusFactory : IDisposable
    {
        private static int internalCount = 0;


        private readonly List<ServerStatusUpdater> updaters = new List<ServerStatusUpdater>();

        private readonly List<ServerStatus> states = new List<ServerStatus>();

        private bool token = false;

        private Thread thread;


        public bool AutoUpdating => thread != null && thread.IsAlive;

        public ServerStatus[] Entries => states.ToArray();


        /// <summary>
        ///     NOTE: This event may only be triggered when running in Auto-Update mode!
        /// </summary>
        public event EventHandler<EventBase[]> ServerChanged;

        /// <summary>
        ///     Start Auto-updating the servers with given Interval
        /// </summary>
        /// <param name="secInterval"></param>
        /// 
        public void StartAutoUpdate(int secInterval = 30)
        {
            if (secInterval < 0) throw new Exception("Interval must be >= 0");
            if (ServerChanged != null && ServerChanged.Target == null) throw new Exception("Event-listener must be registered before auto-updating!");
            StopAutoUpdate();
            token = false;
            thread = new Thread(() => AutoUpdater(secInterval))
            {
                Name = "ServerStatusFactoryAutoUpdater_" + internalCount++
            };
            thread.Start();
        }

        /// <summary>
        ///     Stop Auto-Updating servers
        /// </summary>
        public void StopAutoUpdate()
        {
            if (thread != null && thread.IsAlive)
            {
                token = true;
                thread.Join();
            }
            thread = null;
        }

        /// <summary>
        ///     Ping & update all the servers and groups
        /// </summary>
        public void PingAll(int timeOut = 30)
        {
            Parallel.ForEach(updaters, new ParallelOptions { MaxDegreeOfParallelism = 10 }, srv => srv.Ping(timeOut));
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
            // Get Serverstatusbase or make & add one
            var found = forceNewBase ? null : GetByAddr(addr, port);
            if (found == null)
                updaters.Add(found = new ServerStatusUpdater() { Address = addr, Port = port });
            // Make & add new status
            var state = new ServerStatus(label, found);
            states.Add(state);
            return state;
        }

        /// <summary>
        ///     Destroy a created ServerStatus object.
        ///     Will only destroy the underlying ServerStatusBase if its not used anymore.
        /// </summary>
        /// <param name="status"></param>
        /// <returns>Success indicator</returns>
        public bool Destroy(ServerStatus status)
        {
            if (!states.Contains(status))
                return false;
            // away with it
            states.Remove(status);
            // check if the base is still in use and if not remove it
            if (IsBeingUsed(status.Parent)) return true;
            updaters.Remove(status.Parent);
            status.Parent.Dispose();
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
            return updaters.FirstOrDefault(s => s.Address.ToLower() == addr.ToLower() && s.Port == port);
        }

        /// <summary>
        ///     Returns whether the ServerStatusBase is used more than once
        /// </summary>
        /// <param name="ssb"></param>
        /// <returns></returns>
        private bool IsBeingUsed(ServerStatusUpdater ssb)
        {
            return states.FindAll(s => ssb.Equals(s.Parent)).Count() > 1;
        }

        /// <summary>
        ///     Will run to repeatedly ping all servers in a seperate thread.
        /// </summary>
        /// <param name="secInterval"></param>
        private void AutoUpdater(int secInterval)
        {
            while (!token)
            {
                PingAll();
                states.ForEach(ss =>
                {
                    var evts = ss.Update();
                    if (evts.Length > 0) ServerChanged(ss, evts);
                });
                Thread.Sleep(secInterval * 1000);
            }
        }

        /// <summary>
        ///     Dispose of all IDispose objects managed by this factory
        /// </summary>
        public void Dispose()
        {
            StopAutoUpdate();
            updaters.ForEach(ssb => ssb.Dispose());
        }
    }
}
