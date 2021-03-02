using System;
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
        public ServerStatusUpdater Updater { get; }


        internal ServerStatus(string label, ServerStatusUpdater updater)
        {
            Label = label;
            Updater = updater;
            Updater.UpdatedEvent += UpdatedEvent;
        }


        public EventHandler<EventBase[]> ChangedEvent;


        /// <summary>
        ///     Include a list of Names or UIDs of MineCraft-Users.
        ///     When they join or leave, the PlayerStateChangedEvent will be triggered.
        ///     NOTE; Only new Servers(1.11+) support this feature! Also, large Servers
        ///     don't usually return the actual/complete player list. Hence, this may
        ///     not work for some cases.
        /// </summary>
        private readonly Dictionary<string, string> _userNames = new();
        private readonly Dictionary<string, bool> _userStates = new();

        public ServerInfoBase Last { get; private set; }

        private void UpdatedEvent(object? sender, ServerInfoBase e)
        {
            var updater = (ServerStatusUpdater) sender;
            var events = Update(e);
            ChangedEvent.Invoke(this, events);
        }

        /// <summary>
        ///     Will compare the Last status with the current one and return event updates.
        /// </summary>
        /// <returns></returns>
        private EventBase[] Update(ServerInfoBase current)
        {
            // event-queue
            var events = new List<EventBase>();
            var queue = new List<EventBase>();

            var isFirst = Last == null;
            if (current != null && current != Last)
            {
                // if first info, or Last success was different from this (either went online or went offline) => invoke
                if (isFirst || Last.HadSuccess != current.HadSuccess)
                {
                    Debug.WriteLine("Server '" + Updater.Address + ":" + Updater.Port + "' status change: " + current.HadSuccess);
                    var errMsg = current.LastError != null ? "Connection Failed: " + current.LastError.GetType().Name : "";
                    events.Add(new OnlineStatusEvent(
                        current.HadSuccess, current.HadSuccess ? current.ServerMotd : errMsg,
                        current.HadSuccess ? current.MinecraftVersion : "0.0.0",
                        current.CurrentPlayerCount, current.MaxPlayerCount));
                }


                // JOIN PLAYER QUEUE

                var joins = 0;

                var onlineIds = new List<string>();
                if (current.OnlinePlayers != null)
                {
                    foreach (var p in current.OnlinePlayers)
                    {
                        // save online user id temporarily
                        if (!onlineIds.Contains(p.Id))
                            onlineIds.Add(p.Id);

                        // register name
                        if (!_userNames.ContainsKey(p.Id))
                            _userNames.Add(p.Id, p.Name);
                        else
                            _userNames[p.Id] = p.Name;

                        // if user has state and Last state was online => no change
                        if (_userStates.ContainsKey(p.Id))
                        {
                            if (_userStates[p.Id]) continue;
                            _userStates[p.Id] = true;
                        }
                        else _userStates.Add(p.Id, true);

                        // notify
                        joins++;
                        queue.Add(new PlayerStateEvent(p, true));
                    }
                }

                if (joins > 0)
                {
                    Debug.WriteLine("Server '" + Updater.Address + ":" + Updater.Port + "' join change: " + joins);
                    events.Add(new PlayerChangeEvent(joins));
                }
                events.AddRange(queue);
                queue.Clear();

                // LEAVE PLAYER QUEUE

                var leaves = 0;

                // this needs to be done to avoid ElementChangedException
                var keys = _userStates.Keys.ToArray();
                // check all states for players who went offline
                foreach (var k in keys)
                {
                    if (!_userStates[k] || onlineIds.Contains(k)) continue;
                    // if user state still true, but he is not in online list => went offline
                    _userStates[k] = false;

                    // notify
                    leaves--;
                    queue.Add(new PlayerStateEvent(new PlayerPayLoad { Id = k, RawName = _userNames[k] }, false));
                }

                if (leaves < 0)
                {
                    Debug.WriteLine("Server '" + Updater.Address + ":" + Updater.Port + "' leave change: " + leaves);
                    events.Add(new PlayerChangeEvent(leaves));
                }
                events.AddRange(queue);
                queue.Clear();


                // Fallback, when no PlayerList changes where detected from sample...
                // if first info, or Last player count was different => Notify
                if (joins == 0 && leaves == 0)
                {
                    var diff = isFirst
                        ? current.CurrentPlayerCount
                        : current.CurrentPlayerCount - Last.CurrentPlayerCount;
                    if (diff != 0)
                    {
                        Debug.WriteLine("Server '" + Updater.Address + ":" + Updater.Port + "' count change: " + diff);
                        events.Add(new PlayerChangeEvent(diff));
                    }
                }

            }
            // set new Last
            Last = current;
            return events.ToArray();
        }
    }
}
