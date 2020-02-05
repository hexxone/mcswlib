using System;

namespace mcswlib.ServerStatus.Event
{
    public class PlayerChangeEvent : EventBase
    {

        internal PlayerChangeEvent(EventMessages msg, int diff) : base(msg)
        {
            PlayerDiff = diff;
        }

        public int PlayerDiff { get; }

        public override string ToString()
        {
            var abs = Math.Abs(PlayerDiff);
            var msg = PlayerDiff > 0 ? messages.CountJoin : messages.CountLeave;
            msg = msg.Replace("<count>", abs.ToString());
            msg = msg.Replace("<player>", "Player" + (abs > 1 ? "s" : ""));
            return msg;
        }
    }
}