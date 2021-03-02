using System;
using System.Collections.Generic;
using mcswbot2.Bot.Objects;
using mcswlib.ServerStatus.Event;
using Newtonsoft.Json;
using SkiaSharp;

namespace mcswlib.ServerStatus.ServerInfo
{
    public class ServerInfoBase : ServerInfoBasic, IDisposable
    {
        /// <summary>
        ///     Creates a new instance of <see cref="ServerInfoBase" /> with specified values
        ///     => successful request
        /// </summary>
        /// <param name="dt">When did the request start?</param>
        /// <param name="sp">How long did the request take?</param>
        /// <param name="motd">Server's MOTD</param>
        /// <param name="maxPlayers">Server's max player count</param>
        /// <param name="playerCount">Server's current player count</param>
        /// <param name="version">Server's Minecraft version</param>
        /// <param name="favIco">Server's favicon object if given</param>
        /// <param name="players">Server's online players</param>
        internal ServerInfoBase(DateTime dt, long sp, string motd, int maxPlayers, int playerCount, string version,
            SKImage favIco, List<PlayerPayLoad> players)
        {
            HadSuccess = true;
            RequestDate = dt;
            RequestTime = sp;
            RawMotd = motd;
            MaxPlayerCount = maxPlayers;
            CurrentPlayerCount = playerCount;
            MinecraftVersion = version;
            FavIcon = favIco;
            OnlinePlayers = players;
        }

        /// <summary>
        ///     Creates a new instance of <see cref="ServerInfoBase" /> with specified values
        ///     => failed request
        /// </summary>
        /// <param name="ex">the Last occured Exception when determining Server status</param>
        internal ServerInfoBase(DateTime dt, Exception ex)
        {
            RequestDate = dt;
            LastError = ex;

            HadSuccess = false;
            RequestTime = 1;
            MaxPlayerCount = 0;
            CurrentPlayerCount = 0;
            MinecraftVersion = "0.0.0";
        }
        
        /// <summary>
        ///     Returns the Last occured runtime error
        /// </summary>
        public Exception LastError { get; }

        /// <summary>
        ///     Get the raw Message of the day including formatting's and color codes.
        /// </summary>
        public string RawMotd { get; }

        /// <summary>
        ///     Gets the server's MOTD as Text
        /// </summary>
        [JsonIgnore]
        public string ServerMotd => Types.FixMcChat(RawMotd);

        /// <summary>
        ///     Gets the server's max player count
        /// </summary>
        public double MaxPlayerCount { get; }

        /// <summary>
        ///     Gets the server's Minecraft version
        /// </summary>
        public string MinecraftVersion { get; }

        /// <summary>
        ///     Gets the server's Online Players as object List
        /// </summary>
        public List<PlayerPayLoad> OnlinePlayers { get; }

        /// <summary>
        ///     The Icon for the Server
        /// </summary>
        [JsonIgnore]
        public SKImage FavIcon { get; }
        
        /// <summary>
        ///     better Dispose of Graphics object explicitly
        /// </summary>
        public void Dispose() => FavIcon?.Dispose();
    }
}