using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using mcswlib.ServerStatus.Event;
using Newtonsoft.Json;
using SkiaSharp;

namespace mcswlib.ServerStatus.ServerInfo
{

    /// <summary>
    ///     New method for retrieving server information
    /// </summary>
    internal class GetServerInfoNew : GetServerInfo
    {
        // your "client" protocol version to tell the server 
        // doesn't really matter, server will return its own version independently
        // for detailed protocol version codes see here: https://wiki.vg/Protocol_version_numbers
        private const int Proto = 753;
        private const int BufferSize = short.MaxValue;
        private const string Header = "data:image/png;base64,";

        internal GetServerInfoNew(string addr, int por) : base(addr, por)
        {

        }

        protected override async Task<ServerInfoBase> Get(CancellationToken ct, DateTime startPing, Stopwatch pingTime, TcpClient client, NetworkStream stream)
        {
            var offset = 0;
            var writeBuffer = new List<byte>();

            WriteVarInt(writeBuffer, Proto);
            WriteString(writeBuffer, address);
            WriteShort(writeBuffer, Convert.ToInt16(port));
            WriteVarInt(writeBuffer, 1);
            Flush(ct, writeBuffer, stream, 0);
            // yep, twice.
            Flush(ct, writeBuffer, stream, 0);

            var readBuffer = new byte[BufferSize];
            await stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct);
            // done
            stream.Close();
            client.Close();

            // IF an IOException arises here, this server is probably not a minecraft-one
            _ = ReadVarInt(ref offset, readBuffer); // length
            _ = ReadVarInt(ref offset, readBuffer); // packet
            var jsonLength = ReadVarInt(ref offset, readBuffer);
            var json = ReadString(ref offset, readBuffer, jsonLength);

            dynamic ping = JsonConvert.DeserializeObject(json);

            return new ServerInfoBase(startPing,
                pingTime.ElapsedMilliseconds,
                GetDescription(ping.description),
                (int)ping.players.max,
                (int)ping.players.online,
                (string)ping.version.name,
                GetImage(ping.favicon),
                GetSample(ping.players));
        }

        private static string GetDescription(dynamic desc)
        {
            string res = null;

            // try .extra[]
            try
            {
                // todo convert back to mc stuff?
                if (desc.extra != null)
                {
                    
                    DescPayLoad[] extra = JsonConvert.DeserializeObject<DescPayLoad[]>(desc.extra.ToString());
                    var build = "";
                    foreach (var pLoad in extra)
                    {
                        if (!string.IsNullOrWhiteSpace(build)) build += " ";
                        build += pLoad.text;
                    }
                    res = build;
                }
            }
            catch (Exception e) { Logger.WriteLine("Error description.extra.text: " + e, Types.LogLevel.Debug); }

            // try .text
            if (string.IsNullOrEmpty(res))
                try { res = (string)desc.text; }
                catch (Exception e) { Logger.WriteLine("Error description.text: " + e, Types.LogLevel.Debug); }

            // another fallback
            if (string.IsNullOrEmpty(res))
                try { res = (string)desc; }
                catch (Exception ex) { Logger.WriteLine("Error description: " + ex, Types.LogLevel.Debug); }

            if (res == null) throw new FormatException("Empty description!");
            return res;
        }

        private static SKImage GetImage(dynamic favicon)
        {
            SKImage retour = null;
            try
            {
                var imgStr = (string)favicon;
                if (!string.IsNullOrEmpty(imgStr))
                {
                    if (!imgStr.StartsWith(Header)) throw new Exception("Unknown Format");
                    var imgData = Convert.FromBase64String(imgStr.Substring(Header.Length));
                    retour = SKImage.FromEncodedData(imgData);
                }
            }
            catch (Exception ie)
            {
                Logger.WriteLine("Error parsing favicon: " + ie, Types.LogLevel.Debug);
            }
            return retour;
        }

        private static List<PlayerPayLoad> GetSample(dynamic players)
        {
            var reour = new List<PlayerPayLoad>();
            try
            {
                if (players.sample != null)
                    foreach (var key in players.sample)
                    {
                        if (key.id == null || key.name == null) continue;
                        reour.Add(new PlayerPayLoad() { Id = key.id, RawName = key.name });
                    }
            }
            catch (Exception e)
            {
                Logger.WriteLine("Error when processing sample: " + e, Types.LogLevel.Debug);
            }
            return reour;
        }

        private static byte ReadByte(ref int offset, byte[] buffer)
        {
            var b = buffer[offset];
            offset += 1;
            return b;
        }

        private static byte[] Read(ref int offset, byte[] buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer, offset, data, 0, length);
            offset += length;
            return data;
        }

        private static int ReadVarInt(ref int offset, byte[] buffer)
        {
            var value = 0;
            var size = 0;
            int b;
            while (((b = ReadByte(ref offset, buffer)) & 0x80) == 0x80)
            {
                value |= (b & 0x7F) << (size++ * 7);
                if (size > 5)
                {
                    throw new IOException("This VarInt is an imposter!");
                }
            }
            return value | ((b & 0x7F) << (size * 7));
        }

        private static string ReadString(ref int offset, byte[] buffer, int length)
        {
            var data = Read(ref offset, buffer, length);
            return Encoding.UTF8.GetString(data);
        }

        private static void WriteVarInt(List<byte> buffer, int value)
        {
            while ((value & 128) != 0)
            {
                buffer.Add((byte)(value & 127 | 128));
                value = (int)((uint)value) >> 7;
            }
            buffer.Add((byte)value);
        }

        private static void WriteShort(List<byte> buffer, short value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        private static void WriteString(List<byte> buffer, string data)
        {
            var buff = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer, buff.Length);
            buffer.AddRange(buff);
        }

        private static async void Flush(CancellationToken ct, List<byte> buffer, NetworkStream stream, int id = -1)
        {
            var buff = buffer.ToArray();
            buffer.Clear();

            var add = 0;
            var packetData = new[] { (byte)0x00 };
            if (id >= 0)
            {
                WriteVarInt(buffer, id);
                packetData = buffer.ToArray();
                add = packetData.Length;
                buffer.Clear();
            }

            WriteVarInt(buffer, buff.Length + add);
            var bufferLength = buffer.ToArray();
            buffer.Clear();

            await stream.WriteAsync(bufferLength, 0, bufferLength.Length, ct);
            await stream.WriteAsync(packetData, 0, packetData.Length, ct);
            await stream.WriteAsync(buff, 0, buff.Length, ct);
        }
    }
}