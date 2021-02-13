namespace mcswlib.ServerStatus.Event
{
    public abstract class EventBase
    {
        public override string ToString() { return GetType().FullName; }
    }
}