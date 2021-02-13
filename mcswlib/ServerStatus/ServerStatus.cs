using mcswlib.ServerStatus.Event;
using mcswlib.ServerStatus.ServerInfo;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace mcswlib.ServerStatus
{
    public class ServerStatus
    {
        // Identity
        public string Label { get; }
        public ServerStatusUpdater Parent { get; }


        internal ServerStatus(string label, ServerStatusUpdater prnt)
        {
            Label = label;
            Parent = prnt;
            // default settings
            NotifyServer = true;
            NotifyCount = true;
            NotifyNames = true;
        }

        // Settings

        public bool NotifyServer { get; set; }
        public bool NotifyCount { get; set; }
        public bool NotifyNames { get; set; }


        /// <summary>
        ///     Include a list of Names or UID's of Minecraft-Users.
        ///     When they join or leave, the PlayerStateChangedEvent will be triggerd.
        ///     NOTE; Only new Servers(1.11+) support this feature! Also, large Servers
        ///     don't usually return the actual/complete player list. Hence, this may
        ///     not work for some cases.
        /// </summary>
        private readonly Dictionary<string, string> userNames = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> userStates = new Dictionary<string, bool>();

        public ServerInfoBase Last { get; private set; }

        /// <summary>
        ///     Will compare the Last status with the current one and return event updates.
        /// </summary>
        /// <returns></returns>
        public EventBase[] Update()
        {
            // event-queue
            var events = new List<EventBase>();
            var isFirst = Last == null;
            var current = Parent.GetLatestServerInfo();
            if (current != null)
            {
                // if first info, or Last success was different from this (either went online or went offline) => invoke
                if (NotifyServer && (isFirst || Last.HadSuccess != current.HadSuccess))
                {
                    Debug.WriteLine("Server '" + Parent.Address + ":" + Parent.Port + "' status change: " + current.HadSuccess);
                    var errMsg = current.LastError != null ? "Connection Failed: " + current.LastError.GetType().Name : "";
                    events.Add(new OnlineStatusEvent( 
                        current.HadSuccess, current.HadSuccess ? current.ServerMotd : errMsg, 
                        current.HadSuccess ? current.MinecraftVersion : "0.0.0",
                        current.CurrentPlayerCount, current.MaxPlayerCount));
                }

                // if first info, or Last player count was different (player went online or offline) => invoke
                if (NotifyCount)
                {
                    var diff = isFirst
                        ? current.CurrentPlayerCount
                        : current.CurrentPlayerCount - Last.CurrentPlayerCount;
                    if (diff != 0)
                    {
                        Debug.WriteLine("Server '" + Parent.Address + ":" + Parent.Port + "' count change: " + diff);
                        events.Add(new PlayerChangeEvent(diff));
                    }
                }

                // check current list for new players 
                var onlineIds = new List<string>();
                if (current.OnlinePlayers != null)
                    foreach (var p in current.OnlinePlayers)
                    {
                        // save online user id temporarily
                        if (!onlineIds.Contains(p.Id))
                            onlineIds.Add(p.Id);
                        // register name
                        userNames[p.Id] = p.Name;
                        // if notify and user has state and Last state was offline and user is watched, notify change
                        if (NotifyNames && (!userStates.ContainsKey(p.Id) || !userStates[p.Id]))
                            events.Add(new PlayerStateEvent(p, true));
                        // register state or set to true
                        userStates[p.Id] = true;
                    }

                // this needs to be done to avoid ElementChangedException
                var keys = userStates.Keys.ToArray();
                // check all states for players who went offline
                foreach (var k in keys)
                {
                    if (!userStates[k] || onlineIds.Contains(k)) continue;
                    // if user state still true, but he is not in online list => went offline
                    userStates[k] = false;
                    // create payload
                    var p = new PlayerPayLoad { Id = k, RawName = userNames[k] };
                    // notify => invoke
                    if (NotifyNames)
                        events.Add(new PlayerStateEvent(p, false));

                }
            }
            // set new Last
            Last = current;
            return events.ToArray();
        }
    }
}
