using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace mcswlib.ServerStatus.ServerInfo
{
    internal abstract class GetServerInfo
    {
        protected string address;
        protected int port;

        /// <summary>
        ///     Async TCP-Connect Wrapper for Minecraft Server Pings.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="por"></param>
        internal GetServerInfo(string addr, int por = 25565)
        {
            address = addr;
            port = por;
        }

        /// <summary>
        ///     Calls the Async connect method and afterwards the specific overwritten 
        ///     stream sending, reading & parsing method
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<ServerInfoBase> DoAsync(CancellationToken ct)
        {
            var dt = DateTime.Now;
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                using var client = new TcpClient();
                using var stream = ConnectWrap(ct, client);
                return await Get(ct, dt, sw, client, stream);
            }
            catch(Exception e)
            {
                return new ServerInfoBase(dt, sw.ElapsedMilliseconds, e);
            }
        }

        /// <summary>
        ///     Wrap the TCP Connecting in to another method
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        protected NetworkStream ConnectWrap(CancellationToken ct, TcpClient client)
        {
            var task = client.ConnectAsync(address, port);
            while (!task.IsCompleted && !ct.IsCancellationRequested)
            {
                Debug.WriteLine("Connecting..");
                Task.Delay(10).Wait();
            }
            if (!client.Connected)
                throw new EndOfStreamException();

            return client.GetStream();
        }

        /// <summary>
        ///     Needs to be implemented for each greatly different server version
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="startPing"></param>
        /// <param name="pingTime"></param>
        /// <param name="client"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        protected abstract Task<ServerInfoBase> Get(CancellationToken ct, DateTime startPing, Stopwatch pingTime, TcpClient client, NetworkStream stream);
    }
}
