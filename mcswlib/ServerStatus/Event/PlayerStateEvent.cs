namespace mcswlib.ServerStatus.Event
{
    public class PlayerStateEvent : EventBase
    {
        internal PlayerStateEvent(EventMessages msg, PlayerPayLoad ppl, bool on) : base(msg)
        {
            Player = ppl;
            Online = on;
        }

        public PlayerPayLoad Player { get; }
        public bool Online { get; }

        public override string ToString()
        {
            return (Online ? messages.NameJoin : messages.NameLeave).Replace("<name>", Player.Name);
        }
    }
}