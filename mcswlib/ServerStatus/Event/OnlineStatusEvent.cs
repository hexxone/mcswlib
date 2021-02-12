namespace mcswlib.ServerStatus.Event
{
    public class OnlineStatusEvent : EventBase
    {
        /// <summary>
        ///     Online Status of a  Server, given parameters are online bool & statusText msg if offline
        /// </summary>
        /// <param name="stat"></param>
        /// <param name="statusText"></param>
        internal OnlineStatusEvent(EventMessages msg, bool stat, string statusText = "", string ver = "-") : base(msg)
        {
            ServerStatus = stat;
            StatusText = statusText;
            Version = ver;
        }

        public bool ServerStatus { get; }

        public string StatusText { get; }

        public string Version { get; }

        public override string ToString()
        {
            return (ServerStatus ? messages.ServerOnline : messages.ServerOffline)
                .Replace("<text>", StatusText)
                .Replace("<version>", Version);
        }
    }
}