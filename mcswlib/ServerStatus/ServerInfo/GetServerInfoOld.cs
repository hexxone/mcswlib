using mcswlib.ServerStatus.Event;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mcswlib.ServerStatus.ServerInfo
{
    /// <summary>
    ///     Old method for retrieving Server information
    /// </summary>
    internal class GetServerInfoOld : GetServerInfo
    {
        internal GetServerInfoOld(string addr, int por=25565) : base(addr, por)
        {

        }

        protected override async Task<ServerInfoBase> Get(CancellationToken ct, DateTime startPing, Stopwatch pingTime, TcpClient client, NetworkStream stream)
        {
            await stream.WriteAsync(new byte[] { 0xFE, 0x01 }, 0, 2);
            var buffer = new byte[2048];
            var br = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (buffer[0] != 0xFF) throw new InvalidDataException("Received invalid packet");
            var packet = Encoding.BigEndianUnicode.GetString(buffer, 3, br - 3);
            if (!packet.StartsWith("§")) throw new InvalidDataException("Received invalid data");
            var packetData = packet.Split('\u0000');
            stream.Close();
            client.Close();

            return new ServerInfoBase(startPing, pingTime.ElapsedMilliseconds, packetData[3], int.Parse(packetData[5]),
                int.Parse(packetData[4]), packetData[2], null, new List<PlayerPayLoad>());
        }
    }
}