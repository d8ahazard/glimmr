using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HueDream.Models.CaptureSource.Camera;
using HueDream.Models.Util;
using Newtonsoft.Json.Linq;

namespace HueDream.Models.StreamingDevice.WLed {
    public class WLedStrip : IStreamingDevice, IDisposable
    {
        public WLedData Data { get; set; }
        
        public WLedStrip(WLedData wd) {
            _client = new HttpClient();
            _updateColors = new List<Color>();
            Data = wd ?? throw new ArgumentException("Invalid WLED data.");
            Id = Data.Id;
            IpAddress = Data.IpAddress;
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
        private List<Color> _updateColors;
        
        public void StartStream(CancellationToken ct) {
            if (Streaming) return;
            LogUtil.Write("WLED: Initializing stream.");
            var onObj = new JObject(
                new JProperty("on", true),
                new JProperty("bri", Brightness)
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

        public void StartStream() {
            if (Streaming) return;
            LogUtil.Write("WLED: Initializing stream.");
            var onObj = new JObject(
                new JProperty("on", true),
                new JProperty("bri", Brightness)
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
            for (var i = 0; i < Data.LedCount; i++) {
                packet.AddRange(new byte[] {0, 0, 0});
            }
            _stripSender.SendTo(packet.ToArray(), ep);
            var offObj = new JObject(
                new JProperty("on", false)
            );
            SendPost(offObj);
        }

        public void SetColor(List<Color> colors, double fadeTime, bool ambient = false) {
            if (colors == null) throw new InvalidEnumArgumentException("Colors cannot be null.");
            if (!Streaming) return;
            var packet = new List<Byte>();
            // Set mode to DRGB, dude.
            var timeByte = ambient ? 255 : 2;
            packet.Add(ByteUtils.IntByte(2));
            packet.Add(ByteUtils.IntByte(timeByte));
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

        public void UpdatePixel(int pixelIndex, Color color) {
            if (_updateColors.Count == 0) {
                for (var i = 0; i < Data.LedCount; i++) {
                    _updateColors.Add(Color.FromArgb(0,0,0,0));
                }
            }

            if (pixelIndex >= Data.LedCount) return;
            _updateColors[pixelIndex] = color;
            SetColor(_updateColors, 0);
        }
       
     
        public void ReloadData() {
            var id = Data.Id;
            Data = DataUtil.GetCollectionItem<WLedData>("wled", id);
            appSplitter.AddWled(Data);
        }

        public void UpdateCount(int count) {
            var setting = new Dictionary<string, dynamic>();
            setting["LC"] = count;
            SendForm(setting);
        }

        public void UpdateBrightness(int brightness) {
            Brightness = brightness;
            var onObj = new JObject(
                new JProperty("bri", brightness)
            );
            SendPost(onObj);
        }

        public void UpdateType(bool isRgbw) {
            var setting = new Dictionary<string, dynamic>();
            setting["EW"] = isRgbw;
            SendForm(setting);
        }

        private async void SendPost(JObject values, string target="/json/state") {
            var uri = new Uri("http://" + IpAddress + target);
            var httpContent = new StringContent(values.ToString());
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            await _client.PostAsync(uri, httpContent);
            httpContent.Dispose();
        }

        private async void SendForm(Dictionary<string, dynamic> values) {
            var uri = new Uri("http://" + IpAddress + "settings/leds");
            var request = (HttpWebRequest)WebRequest.Create(uri);
            string postData = string.Empty;
            foreach (string k in values.Keys) {
                if (string.IsNullOrEmpty(postData)) {
                    postData = $"?{k}={values[k]}";
                } else {
                    postData += $"@{k}={values[k]}";
                }
            }
            var data = Encoding.ASCII.GetBytes(postData);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            await using (var stream = request.GetRequestStream()) {
                await stream.WriteAsync(data, 0, data.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            await using var rs = response.GetResponseStream();
            if (rs != null) {
                var responseString = await new StreamReader(rs).ReadToEndAsync();
                LogUtil.Write("We got a response: " + responseString);    
            }
            response.Dispose();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing) {
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