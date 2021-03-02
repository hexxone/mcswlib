using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using mcswlib.ServerStatus.Event;
using Newtonsoft.Json;
using SkiaSharp;

namespace mcswlib.ServerStatus.ServerInfo
{

    internal class ServerInfo
    {
        private static Random rnd = new Random(69 * 1337 - 42);
        private readonly string _address;
        private readonly int _port;

        /// <summary>
        ///     Async TCP-Connect Wrapper for MineCraft Server Pings.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        internal ServerInfo(string address, int port = 25565)
        {
            _address = address;
            _port = port;
        }

        /// <summary>
        ///     Calls the Async connect method and afterwards the specific overwritten 
        ///     stream sending, reading & parsing method
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<ServerInfoBase> GetAsync(CancellationToken ct, DateTime dt)
        {
            try
            {
                using var client = new TcpClient();
                await using var stream = ConnectWrap(ct, client);
                return Get(ct, dt, client, stream);
            }
            catch (Exception e)
            {
                return new ServerInfoBase(dt, e);
            }
        }

        /// <summary>
        ///     Wrap the TCP Connecting in to another method
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private NetworkStream ConnectWrap(CancellationToken ct, TcpClient client)
        {
            var task = client.ConnectAsync(_address, _port, ct);
            while (!task.IsCompleted && !ct.IsCancellationRequested)
            {
                Debug.WriteLine("Connecting..");
                Task.Delay(20).Wait(ct);
            }
            if (!client.Connected)
                throw new EndOfStreamException();

            return client.GetStream();
        }


        // your "client" protocol version to tell the server 
        // doesn't really matter, server will return its own version independently
        // for detailed protocol version codes see here: https://wiki.vg/Protocol_version_numbers
        private const int Proto = 753;
        private const string Header = "data:image/png;base64,";


        private ServerInfoBase Get(CancellationToken ct, DateTime dt, TcpClient client, NetworkStream stream)
        {
            var offset = 0;
            var writeBuffer = new List<byte>();

            WriteVarInt(writeBuffer, Proto);
            WriteString(writeBuffer, _address);
            WriteShort(writeBuffer, Convert.ToInt16(_port));
            WriteVarInt(writeBuffer, 1); // we want to know status, not login

            Flush(writeBuffer, stream, 0, ct);
            Flush(writeBuffer, stream, 0, ct);

            var batch = new byte[1024];
            var readBuffer = new List<byte>();

            // Read the first data
            var readLen = stream.Read(batch, 0, batch.Length);
            readBuffer.AddRange(batch);

            // IF an IOException arises here, this server is probably not a MineCraft-one
            // TODO test if "+ 3" is always the case because of fixed ints?
            var allLength = ReadVarInt(ref offset, readBuffer) + 3;

            // Read the rest of the announced data
            while (readLen < allLength)
            {
                var read = stream.Read(batch, 0, batch.Length);
                Logger.WriteLine("Read bytes: " + read, Types.LogLevel.Debug);
                readBuffer.AddRange(batch.ToList().Where((a, x) => x < read));
                readLen += read;
                Task.Delay(10).Wait(ct);
                if (!stream.DataAvailable && readLen < allLength)
                    Logger.WriteLine("Missing bytes: " + (allLength - readLen), Types.LogLevel.Debug);
            }

            var packetNr = ReadVarInt(ref offset, readBuffer);
            var jsonLength = ReadVarInt(ref offset, readBuffer);
            var json = ReadString(ref offset, readBuffer, jsonLength);

            // now lets calculate the real ping

            writeBuffer.Clear();
            readBuffer.Clear();
            offset = 0;

            // random data
            var sentRnd = DateTime.Now.Ticks;
            writeBuffer.AddRange(BitConverter.GetBytes(sentRnd));
            // send off
            var sw = new Stopwatch();
            sw.Start();
            Flush(writeBuffer, stream, 1, ct);

            // wait for data
            readLen = stream.Read(batch, 0, batch.Length);
            readBuffer.AddRange(batch);

            allLength = ReadVarInt(ref offset, readBuffer) - 1;
            packetNr = ReadVarInt(ref offset, readBuffer);
            var reData = Read(ref offset, readBuffer, 8);
            var recvRnd = BitConverter.ToInt64(reData);

            // check for packet
            if (packetNr == 1 && sentRnd == recvRnd) sw.Stop();
            else Logger.WriteLine("Ping Data mismatch: " + sentRnd + " => " + recvRnd);

            // done
            stream.Close();
            client.Close();

            Logger.WriteLine("Json: " + json, Types.LogLevel.Debug);

            dynamic ping = JsonConvert.DeserializeObject(json);

            return new ServerInfoBase(dt,
                sw.ElapsedMilliseconds / 2,
                GetDescription(ping.description),
                (int)ping.players.max,
                (int)ping.players.online,
                (string)ping.version.name,
                GetImage(ping.favicon),
                GetSample(ping.players));
        }


        // =======================================================
        // Parser Utilities
        // =======================================================


        /// <summary>
        ///     Try to parse the description string from dynamic json
        /// </summary>
        /// <param name="desc"></param>
        /// <returns></returns>
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
                        build += pLoad.Text;
                    }
                    res = build.Replace("  ", " ");
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

        /// <summary>
        ///     Try to get image object for encoded base64 icon
        /// </summary>
        /// <param name="favicon"></param>
        /// <returns></returns>
        private static SKImage GetImage(dynamic favicon)
        {
            SKImage image = null;
            try
            {
                var imgStr = (string)favicon;
                if (!string.IsNullOrEmpty(imgStr))
                {
                    if (!imgStr.StartsWith(Header)) throw new Exception("Unknown Format");
                    var imgData = Convert.FromBase64String(imgStr[Header.Length..]);
                    image = SKImage.FromEncodedData(imgData);
                }
            }
            catch (Exception ie)
            {
                Logger.WriteLine("Error parsing favicon: " + ie, Types.LogLevel.Debug);
            }
            return image;
        }

        /// <summary>
        ///     Try to parse the the player samples from json
        /// </summary>
        /// <param name="players"></param>
        /// <returns></returns>
        private static List<PlayerPayLoad> GetSample(dynamic players)
        {
            var sample = new List<PlayerPayLoad>();
            try
            {
                if (players.sample != null)
                    foreach (var key in players.sample)
                    {
                        if (key.id == null || key.name == null) continue;
                        sample.Add(new PlayerPayLoad() { Id = key.id, RawName = key.name });
                    }
            }
            catch (Exception e)
            {
                Logger.WriteLine("Error when processing sample: " + e, Types.LogLevel.Debug);
            }
            return sample;
        }


        // =======================================================
        // Network Read Utilities
        // =======================================================


        private static byte ReadByte(ref int offset, List<byte> buffer)
        {
            var b = buffer[offset];
            offset += 1;
            return b;
        }

        private static byte[] Read(ref int offset, List<byte> buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer.ToArray(), offset, data, 0, length);
            offset += length;
            return data;
        }

        private static int ReadVarInt(ref int offset, List<byte> buffer)
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

        private static string ReadString(ref int offset, List<byte> buffer, int length)
        {
            var data = Read(ref offset, buffer, length);
            return Encoding.UTF8.GetString(data);
        }


        // =======================================================
        // Network Write Utilities
        // =======================================================


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
        private static void WriteLong(List<byte> buffer, long value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        private static void WriteString(List<byte> buffer, string data)
        {
            var buff = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer, buff.Length);
            buffer.AddRange(buff);
        }

        private static async void Flush(List<byte> buffer, Stream stream, int id, CancellationToken ct)
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

            await stream.WriteAsync(bufferLength.AsMemory(0, bufferLength.Length), ct);
            await stream.WriteAsync(packetData.AsMemory(0, packetData.Length), ct);
            await stream.WriteAsync(buff.AsMemory(0, buff.Length), ct);
        }

    }
}
