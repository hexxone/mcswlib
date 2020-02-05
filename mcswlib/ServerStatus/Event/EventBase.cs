namespace mcswlib.ServerStatus.Event
{
    public abstract class EventBase
    {
        protected EventMessages messages;

        internal EventBase(EventMessages msg)
        {
            messages = msg;
        }

        /// <summary>
        ///     This function needs to be overwritten to return the event-specific message
        /// </summary>
        /// <returns></returns>
        public override string ToString() { return GetType().FullName; }
    }
}