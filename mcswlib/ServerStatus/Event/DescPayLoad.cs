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
            this.Text = text;
            this.Color = color;
            this.Italic = italic;
            this.Bold = bold;
            this.Underlined = underlined;
            this.Strikethrough = strikethrough;
            this.Obfuscated = obfuscated;
        }

        // should always be set
        public string Text { get; }

        // most used params?
        public string Color { get; }
        public bool Italic { get; }

        // rest of them
        public bool Bold { get; }
        public bool Underlined { get; }
        public bool Strikethrough { get; }
        public bool Obfuscated { get; }
    }
}