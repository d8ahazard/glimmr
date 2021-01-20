using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.StreamingDevice.Yeelight;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Glimmr.Models.StreamingDevice.WLED {
    public class WledDevice : IStreamingDevice, IDisposable
    {
        public bool Enable { get; set; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }
        private bool _disposed;
        private bool _sending;
        private bool colorsSet;
        private static int port = 21324;
        private IPEndPoint ep;
        private Splitter appSplitter;
        private List<Color> _updateColors;
        private HttpClient _httpClient;
        private UdpClient _udpClient;
        
        StreamingData IStreamingDevice.Data {
            get => Data;
            set => Data = (WledData) value;
        }

        public WledData Data { get; set; }
        
        public WledDevice(WledData wd, UdpClient uc, HttpClient hc, ColorService colorService) {
            colorService.ColorSendEvent += SetColor;
            _udpClient = uc;
            _httpClient = hc;
            _updateColors = new List<Color>();
            Data = wd ?? throw new ArgumentException("Invalid WLED data.");
            Log.Debug("Enabled: " + IsEnabled());
            Id = Data.Id;
            Enable = Data.Enable;
            IpAddress = Data.IpAddress;
        }

        
        public void StartStream(CancellationToken ct) {
            if (Streaming) return;
            if (!Data.Enable) return;
            Log.Debug("WLED: Initializing stream.");
           
            var onObj = new JObject(
                new JProperty("on", true),
                new JProperty("bri", Brightness)
                );
            SendPost(onObj);
            ep = IpUtil.Parse(IpAddress, port);
            Streaming = true;
            Log.Debug("WLED: Streaming started...");
        }


        public void FlashColor(Color color) {
            var packet = new List<Byte>();
            // Set mode to DRGB, dude.
            var timeByte = 255;
            packet.Add(ByteUtils.IntByte(2));
            packet.Add(ByteUtils.IntByte(timeByte));
            for (var i = 0; i < Data.LedCount; i++) {
                packet.Add(color.R);
                packet.Add(color.G);
                packet.Add(color.B);
            }
            
            try {
                _udpClient?.SendAsync(packet.ToArray(), packet.Count, ep);
            } catch (Exception e) {
                Log.Debug("Fucking exception, look at that: " + e.Message);        
            }
        }

        public bool IsEnabled() {
            return Data.Enable;
        }

        
        public void StopStream() {
            StopStrip();
            Streaming = false;
            Log.Debug("WLED: Stream stopped.");
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
            if (ep != null) _udpClient?.SendAsync(packet.ToArray(), packet.Count, ep);
            var offObj = new JObject(
                new JProperty("on", false)
            );
            SendPost(offObj);
        }

        public void SetColor(List<Color> colors, List<Color> _, double fadeTime) {
            if (colors == null) {
                return;
            }
            if (!Streaming || !Data.Enable || Testing) {
                return;
            }

            colors = TruncateColors(colors);
            if (Data.StripMode == 2) {
                colors = ShiftColors(colors);
            } else {
                if (Data.StripDirection == 1) {
                    colors.Reverse();
                }
            }

            
            var packet = new List<Byte>();
            // Set mode to DRGB, dude.
            var timeByte = 255;
            packet.Add(ByteUtils.IntByte(2));
            packet.Add(ByteUtils.IntByte(timeByte));
            foreach (var color in colors) {
                packet.Add(ByteUtils.IntByte(color.R));
                packet.Add(ByteUtils.IntByte(color.G));
                packet.Add(ByteUtils.IntByte(color.B));
            }
            if (!colorsSet) {
                colorsSet = true;
            }

            if (ep == null) {
                Log.Debug("No endpoint.");
                return;
            }

            if (_sending) {
                Log.Debug("Already sending...");
                return;
            }

            _sending = true;
            try {
                _udpClient?.SendAsync(packet.ToArray(), packet.Count, ep);
            } catch (Exception e) {
                Log.Debug("Fucking exception, look at that: " + e.Message);        
            }

            _sending = false;
        }

        private List<Color> TruncateColors(List<Color> input) {
            var truncated = new List<Color>();
            var offset = Data.Offset;
            // Subtract one from our offset because arrays
            var len = Data.LedCount;
            if (Data.StripMode == 2) len /= 2;
            // Start at the beginning
            if (offset + len > input.Count) {
                // Set the point where we need to end the loop
                var offsetLen = offset + len - input.Count;
                // Where do we start midway?
                var loopLen = input.Count - offsetLen;
                if (loopLen > 0) {
                    for (var i = loopLen - 1; i < input.Count; i++) {
                        truncated.Add(input[i]);
                    }
                }

                // Now calculate how many are needed from the front
                for (var i = 0; i < len - offsetLen; i++) {
                    truncated.Add(input[i]);
                }
            } else {
                for (var i = offset; i < offset + len; i++) {
                    truncated.Add(input[i]);
                }    
            }

            return truncated;
        }

        private List<Color> ShiftColors(IReadOnlyList<Color> input) {
            var output = new Color[input.Count * 2];
            var il = output.Length - 1;
            if (Data.StripDirection == 0) {
                for (var i = 0; i < input.Count; i++) {
                    output[i] = input[i];
                    output[il - i] = input[i];
                }
            } else {
                var l = 0;
                for (var i = input.Count - 1; i >= 0; i--) {
                    output[i] = input[l];
                    output[il - i] = input[l];
                    l++;
                }
            }


            return output.ToList();
        }

        public void UpdatePixel(int pixelIndex, Color color) {
            if (_updateColors.Count == 0) {
                for (var i = 0; i < Data.LedCount; i++) {
                    _updateColors.Add(Color.FromArgb(0,0,0,0));
                }
            }

            if (pixelIndex >= Data.LedCount) return;
            _updateColors[pixelIndex] = color;
            SetColor(_updateColors, null, 0);
        }
       
     
        public void ReloadData() {
            var id = Data.Id;
            Data = DataUtil.GetCollectionItem<WledData>("Dev_Wled", id);
            Log.Debug($"Reloaded LED Data for {id}: " + JsonConvert.SerializeObject(Data));
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
            Uri uri;
            if (string.IsNullOrEmpty(IpAddress) && !string.IsNullOrEmpty(Id)) {
                IpAddress = Id;
                Data.IpAddress = Id;
                DataUtil.InsertCollection<WledData>("Dev_Wled", Data);
            } 
            try {
                uri = new Uri("http://" + IpAddress + target);
            } catch (UriFormatException e) {
                Log.Warning("URI Format exception: ", e);
                return;
            }

            var httpContent = new StringContent(values.ToString());
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            try {
                await _httpClient.PostAsync(uri, httpContent);
            } catch (Exception e) {
                Log.Warning("HTTP Request Exception...");
            }

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
            var responseString = await new StreamReader(rs).ReadToEndAsync();
            Log.Debug("We got a response: " + responseString);    
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
                }
            }

            _disposed = true;
        }
    }
}