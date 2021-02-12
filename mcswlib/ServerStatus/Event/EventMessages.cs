namespace mcswlib.ServerStatus.Event
{
    /// <summary>
    ///     Public class representing Event messages.
    /// </summary>
    public class EventMessages
    {
        public string ServerOnline = "Server status: online\r\nVersion: <code><version></code>\r\nMOTD:\r\n<code><text></code>";
        public string ServerOffline = "Server status: offline\r\nReason:\r\n<code><text></code>";

        public string CountJoin = "<count> <player> joined.";
        public string CountLeave = "<count> <player> left.";

        public string NameJoin = "´+ <name>";
        public string NameLeave = "- <name>";
    }
}
