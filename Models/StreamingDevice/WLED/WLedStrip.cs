using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using HueDream.Models.CaptureSource.Camera;
using HueDream.Models.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HueDream.Models.StreamingDevice.WLed {
    public class WLedStrip : IStreamingDevice, IDisposable
    {
        private WLedData _data;
        
        public WLedStrip(WLedData wd) {
            _client = new HttpClient();
            _data = wd;
            Id = _data.Id;
            IpAddress = _data.IpAddress;
        }

        public bool Streaming { get; set; }
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        private Socket _stripSender;
        private bool _disposed;
        private bool colorsSet;
        private static int port = 21324;
        private IPEndPoint ep;
        private Splitter appSplitter;
        private HttpClient _client;
        
        public async void StartStream(CancellationToken ct) {
            if (Streaming) return;
            LogUtil.Write("WLED: Initializing stream.");
            var onObj = new JObject(
                new JProperty("on", true),
                new JProperty("bri", 255)
                );
            SendPost(onObj);
            _stripSender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _stripSender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _stripSender.Blocking = false;
            _stripSender.EnableBroadcast = false;
            ep = IpUtil.Parse(IpAddress, port);
            Streaming = true;
            LogUtil.Write("WLED: Streaming started...");
        }

        public void StopStream() {
            StopStrip();
            Streaming = false;
            _stripSender.Dispose();
            LogUtil.Write("WLED: Stream stopped.");

        }

        private void StopStrip() {
            if (!Streaming) return;
            var packet = new List<byte>();
            // Set mode to DRGB, dude.
            packet.Add(ByteUtils.IntByte(2));
            packet.Add(ByteUtils.IntByte(2));
            for (var i = 0; i < _data.LedCount; i++) {
                packet.AddRange(new byte[] {0, 0, 0});
            }
            _stripSender.SendTo(packet.ToArray(), ep);
            var offObj = new JObject(
                new JProperty("on", false)
            );
            SendPost(offObj);
        }

        public void SetColor(List<Color> colors, double fadeTime) {
            if (colors == null) throw new InvalidEnumArgumentException("Colors cannot be null.");
            if (!Streaming) return;
            var packet = new List<Byte>();
            // Set mode to DRGB, dude.
            packet.Add(ByteUtils.IntByte(2));
            packet.Add(ByteUtils.IntByte(2));
            foreach (var color in colors) {
                packet.Add(ByteUtils.IntByte(color.R));
                packet.Add(ByteUtils.IntByte(color.G));
                packet.Add(ByteUtils.IntByte(color.B));
            }
            //LogUtil.Write("No, really, sending?");
            if (!colorsSet) {
                colorsSet = true;
                LogUtil.Write("Sending " + colors.Count + " colors to " + IpAddress);
                LogUtil.Write("First packet: " + ByteUtils.ByteString(packet.ToArray()));
            }
            _stripSender.SendTo(packet.ToArray(), ep);
            //LogUtil.Write("Sent.");
        }
       
     
        public void ReloadData() {
            var id = _data.Id;
            _data = DataUtil.GetCollectionItem<WLedData>("wled", id);
            appSplitter.AddWled(_data);
        }

        private async void SendPost(JObject values) {
            var uri = new Uri("http://" + IpAddress + "/json/state");
            var httpContent = new StringContent(values.ToString());
            LogUtil.Write("Posting content: " + values + " to " + uri);
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _client.PostAsync(uri, httpContent);
            var responseString = await response.Content.ReadAsStringAsync();
            LogUtil.Write("Response: " + responseString);
            httpContent.Dispose();
        }

        public void Dispose() {
            Dispose(true);
        }


        private void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (disposing) {
                if (Streaming) {
                    StopStream();
                    _stripSender?.Dispose();
                }
            }

            _disposed = true;
        }
    }
}