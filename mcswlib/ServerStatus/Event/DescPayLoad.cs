namespace mcswlib.ServerStatus.Event
{
    public class DescPayLoad
    {
        public DescPayLoad(
            string text,
            string color = "white",
            bool italic = false,
            bool bold = false,
            bool underlined = false,
            bool strikethrough = false,
            bool obfuscated = false)
        {
            this.text = text;
            this.color = color;
            this.italic = italic;
            this.bold = bold;
            this.underlined = underlined;
            this.strikethrough = strikethrough;
            this.obfuscated = obfuscated;
        }

        // should always be set
        public string text { get; }

        // most used params?
        public string color { get; }
        public bool italic { get; }

        // rest of them
        public bool bold { get; }
        public bool underlined { get; }
        public bool strikethrough { get; }
        public bool obfuscated { get; }
    }
}