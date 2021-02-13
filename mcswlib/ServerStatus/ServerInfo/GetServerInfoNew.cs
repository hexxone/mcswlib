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
        private const int Proto = 47;
        private const int BufferSize = short.MaxValue;

        internal GetServerInfoNew(string addr, int por) : base(addr, por)
        {

        }

        protected override async Task<ServerInfoBase> Get(CancellationToken ct, DateTime startPing, Stopwatch pingTime, TcpClient client, NetworkStream stream)
        {
            var _offset = 0;
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
            // IF an IOException arises here, thie server is probably not a minecraft-one
            var length = ReadVarInt(ref _offset, readBuffer);
            var packet = ReadVarInt(ref _offset, readBuffer);
            var jsonLength = ReadVarInt(ref _offset, readBuffer);
            var json = ReadString(ref _offset, readBuffer, jsonLength);

            dynamic ping = JsonConvert.DeserializeObject(json);

            // parse player sample
            var sample = new List<PlayerPayLoad>();
            if (json.Contains("\"sample\":["))
            {
                try
                {
                    foreach (var key in ping.players.sample)
                    {
                        if (key.id == null || key.name == null) continue;
                        var plr = new PlayerPayLoad() { Id = key.id, RawName = key.name };
                        sample.Add(plr);
                    }
                }
                catch (Exception e)
                {
                    Logger.WriteLine("Error when sample processing: " + e, Types.LogLevel.Debug);
                }
            }
            // parse favicon
            SKImage image = null;
            if (json.Contains("\"favicon\":\""))
            {
                try
                {
                    var hdr = "data:image/png;base64,";
                    var imgStr = (string)ping.favicon;
                    if (!imgStr.StartsWith(hdr)) throw new Exception("Unkown Format");
                    var imgData = Convert.FromBase64String(imgStr.Substring(hdr.Length));
                    image = SKImage.FromEncodedData(imgData);
                }
                catch (Exception ie)
                {
                    Logger.WriteLine("Error parsing favicon: " + ie, Types.LogLevel.Debug);
                }
            }
            // parse MOTD/description
            var desc = "";
            if (json.Contains("\"description\":{\""))
            {
                try
                {
                    desc = (string)ping.description.text;
                }
                catch (Exception e)
                {
                    Logger.WriteLine("Error description.text: " + e, Types.LogLevel.Debug);
                }
            }
            if (string.IsNullOrEmpty(desc))
            {
                try
                {
                    desc = (string)ping.description;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Error description: " + ex, Types.LogLevel.Debug);
                }
            }

            if (string.IsNullOrEmpty(desc))
                throw new FormatException("Empty description!");

            return new ServerInfoBase(startPing, pingTime.ElapsedMilliseconds, desc, (int)ping.players.max, (int)ping.players.online, (string)ping.version.name, image, sample);
        }


        #region request helper methods

        internal static byte ReadByte(ref int _offset, byte[] buffer)
        {
            var b = buffer[_offset];
            _offset += 1;
            return b;
        }

        internal static byte[] Read(ref int _offset, byte[] buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer, _offset, data, 0, length);
            _offset += length;
            return data;
        }

        internal static int ReadVarInt(ref int _offset, byte[] buffer)
        {
            var value = 0;
            var size = 0;
            int b;
            while (((b = ReadByte(ref _offset, buffer)) & 0x80) == 0x80)
            {
                value |= (b & 0x7F) << (size++ * 7);
                if (size > 5)
                {
                    throw new IOException("This VarInt is an imposter!");
                }
            }
            return value | ((b & 0x7F) << (size * 7));
        }

        internal static string ReadString(ref int _offset, byte[] buffer, int length)
        {
            var data = Read(ref _offset, buffer, length);
            return Encoding.UTF8.GetString(data);
        }

        internal static void WriteVarInt(List<byte> buffer, int value)
        {
            while ((value & 128) != 0)
            {
                buffer.Add((byte)(value & 127 | 128));
                value = (int)((uint)value) >> 7;
            }
            buffer.Add((byte)value);
        }

        internal static void WriteShort(List<byte> buffer, short value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        internal static void WriteString(List<byte> buffer, string data)
        {
            var buff = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer, buff.Length);
            buffer.AddRange(buff);
        }

        internal static void Write(NetworkStream stream, byte b)
        {
            stream.WriteByte(b);
        }

        internal static async void Flush(CancellationToken ct, List<byte> buffer, NetworkStream stream, int id = -1)
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

        #endregion
    }
}