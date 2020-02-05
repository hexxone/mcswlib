namespace mcswlib.ServerStatus.Event
{
    public class PlayerPayLoad
    {
        internal PlayerPayLoad() { }

        public string Name => Types.FixMcChat(RawName);
        public string RawName { get; set; }
        public string Id { get; set; }
    }
}