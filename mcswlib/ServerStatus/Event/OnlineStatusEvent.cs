namespace mcswlib.ServerStatus.Event
{
    public class OnlineStatusEvent : EventBase
    {
        /// <summary>
        ///     Online Status of a  Server, given parameters are online bool & statusText msg if offline
        /// </summary>
        /// <param name="stat"></param>
        /// <param name="statusText"></param>
        internal OnlineStatusEvent(bool stat, string statusText = "", string ver = "-", double cur = 0, double max = 0)
        {
            ServerStatus = stat;
            StatusText = statusText;
            Version = ver;
            CurrentPlayers = cur;
            MaxPlayers = max;
        }

        public bool ServerStatus { get; }

        public string StatusText { get; }

        public string Version { get; }

        public double CurrentPlayers { get; }

        public double MaxPlayers { get; }
    }
}