namespace mcswlib
{
    public static class Types
    {
        /// <summary>
        ///     Determines what to log and what not.
        /// </summary>
        public enum LogLevel
        {
            None = 0,
            Error = 10,
            Normal = 20,
            Debug = 30
        }

        /// <summary>
        ///     removes Minecraft Chat Syle informations
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        internal static string FixMcChat(string s)
        {
            var l = new[]
            {
                "§4", "§c", "§6", "§e",
                "§2", "§a", "§b", "§3",
                "§1", "§9", "§d", "§5",
                "§f", "§7", "§8", "§0",
                "§l", "§m", "§n", "§o", "§r"
            };
            foreach (var t in l) s = s.Replace(t, "");
            return s;
        }
    }
}