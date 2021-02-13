using System;

namespace mcswlib.ServerStatus.Event
{
    public class PlayerChangeEvent : EventBase
    {

        internal PlayerChangeEvent(int diff)
        {
            PlayerDiff = diff;
        }

        public int PlayerDiff { get; }
    }
}