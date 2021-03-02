using System;
using Newtonsoft.Json;

namespace mcswbot2.Bot.Objects
{
    [Serializable]
    public class ServerInfoBasic
    {
        /// <summary>
        ///     Determines if the request was successfull
        /// </summary>
        public bool HadSuccess { get; set; }

        /// <summary>
        ///     TimeStamp when the request was done
        /// </summary>
        public DateTime RequestDate { get; set; }

        /// <summary>
        ///     How long did the request take to complete in MS?
        /// </summary>
        public double RequestTime { get; set; }

        /// <summary>
        ///     Gets the server's current player count
        /// </summary>
        public double CurrentPlayerCount { get; set; }

        public ServerInfoBasic() { }

        [JsonConstructor]
        public ServerInfoBasic(bool hadSuccess, DateTime requestDate, double requestTime, double currentPlayerCount)
        {
            HadSuccess = hadSuccess;
            RequestDate = requestDate;
            RequestTime = requestTime;
            CurrentPlayerCount = currentPlayerCount;
        }
    }
}
