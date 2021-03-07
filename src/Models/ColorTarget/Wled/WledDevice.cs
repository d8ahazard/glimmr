using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Glimmr.Models.ColorTarget.Wled {
    public class WledDevice : ColorTarget, IColorTarget, IDisposable
    {
        public bool Enable { get; set; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }
        private bool _disposed;
        private static int port = 21324;
        private IPEndPoint _ep;
        private readonly List<Color> _updateColors;
        private readonly HttpClient _httpClient;
        private readonly UdpClient _udpClient;
        private int _offset;
        private int _len;
        
        IColorTargetData IColorTarget.Data {
            get => Data;
            set => Data = (WledData) value;
        }

        public WledData Data { get; set; }
        
        public WledDevice(WledData wd, ColorService colorService) : base(colorService) {
            ColorService.ColorSendEvent += SetColor;
            _udpClient = ColorService.ControlService.UdpClient;
            _httpClient = ColorService.ControlService.HttpSender;
            _updateColors = new List<Color>();
            Data = wd ?? throw new ArgumentException("Invalid WLED data.");
            Id = Data.Id;
            Enable = Data.Enable;
            IpAddress = Data.IpAddress;
            _offset = Data.Offset;
            _len = Data.LedCount;
        }

        
        public async Task StartStream(CancellationToken ct) {
            if (Streaming) return;
            if (!Data.Enable) return;
            var onObj = new JObject(
                new JProperty("on", true),
                new JProperty("bri", Brightness)
                );
            await SendPost(onObj);
            _ep = IpUtil.Parse(IpAddress, port);
            Streaming = true;
        }


        public async Task FlashColor(Color color) {
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
                if (_udpClient != null) {
                    await _udpClient.SendAsync(packet.ToArray(), packet.Count, _ep);    
                }
                
            } catch (Exception e) {
                Log.Debug("Exception, look at that: " + e.Message);        
            }
        }


        public bool IsEnabled() {
            return Data.Enable;
        }

        
        public async Task StopStream() {
            if (!Enable) return;
            await FlashColor(Color.FromArgb(0, 0, 0));
            Streaming = false;
            Log.Debug("WLED: Stream stopped.");
            var offObj = new JObject(
                new JProperty("on", false)
            );
            await SendPost(offObj);
        }


        public void SetColor(List<Color> list, List<Color> colors1, int arg3, bool force=false) {
            
            if (!Streaming || !Data.Enable || Testing && !force) {
                return;
            }

            var colors = list;
            colors = ColorUtil.TruncateColors(colors,_offset, _len);
            if (Data.StripMode == 2) {
                colors = ShiftColors(colors);
            } else {
                if (Data.ReverseStrip) {
                    colors.Reverse();
                }
            }

            var packet = new Byte[2 + colors.Count * 3];
            // Set mode to DRGB, dude.
            var timeByte = 255;
            packet[0] = ByteUtils.IntByte(2);
            packet[1] = ByteUtils.IntByte(timeByte);
            var pInt = 2;
            for (var i = 0; i < colors.Count; i++) {
                packet[pInt] = ByteUtils.IntByte(colors[i].R);
                packet[pInt + 1] = ByteUtils.IntByte(colors[i].G);
                packet[pInt + 2] = ByteUtils.IntByte(colors[i].B);
                pInt += 3;
            }

            if (_ep == null) {
                Log.Debug("No endpoint.");
                return;
            }

            try {
                _udpClient.SendAsync(packet.ToArray(), packet.Length, _ep);
            } catch (Exception e) {
                Log.Debug("Exception: " + e.Message);        
            }
        }

        

        private List<Color> ShiftColors(IReadOnlyList<Color> input) {
            var output = new Color[input.Count];
            var il = output.Length - 1;
            if (!Data.ReverseStrip) {
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

        public async Task UpdatePixel(int pixelIndex, Color color) {
            if (_updateColors.Count == 0) {
                for (var i = 0; i < Data.LedCount; i++) {
                    _updateColors.Add(Color.FromArgb(0,0,0,0));
                }
            }

            if (pixelIndex >= Data.LedCount) return;
            _updateColors[pixelIndex] = color;
            SetColor(_updateColors, null, 0);
            await Task.FromResult(true);
        }
       
     
        public Task ReloadData() {
            var id = Data.Id;
            Data = DataUtil.GetDevice<WledData>(id);
            _offset = Data.Offset;
            _len = Data.LedCount;
            Log.Debug($"Reloaded LED Data for {id}: " + JsonConvert.SerializeObject(Data));
            return Task.CompletedTask;
        }

        public async Task UpdateCount(int count) {
            var setting = new Dictionary<string, dynamic>();
            setting["LC"] = count;
            await SendForm(setting);
        }

        public async Task UpdateBrightness(int brightness) {
            Brightness = brightness;
            var onObj = new JObject(
                new JProperty("bri", brightness)
            );
            await SendPost(onObj);
        }

        public async Task UpdateType(bool isRgbw) {
            var setting = new Dictionary<string, dynamic>();
            setting["EW"] = isRgbw;
            await SendForm(setting);
        }

        private async Task SendPost(JObject values, string target="/json/state") {
            Uri uri;
            if (string.IsNullOrEmpty(IpAddress) && !string.IsNullOrEmpty(Id)) {
                IpAddress = Id;
                Data.IpAddress = Id;
                await DataUtil.InsertCollection<WledData>("Dev_Wled", Data);
            } 
            try {
                uri = new Uri("http://" + IpAddress + target);
            } catch (UriFormatException e) {
                Log.Warning("URI Format exception: " + e.Message);
                return;
            }

            var httpContent = new StringContent(values.ToString());
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            try {
                await _httpClient.PostAsync(uri, httpContent);
            } catch (Exception e) {
                Log.Warning("HTTP Request Exception: " + e.Message);
            }

            httpContent.Dispose();
        }

        private async Task SendForm(Dictionary<string, dynamic> values) {
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
                await stream.WriteAsync(data.AsMemory(0, data.Length));
            }

            var response = (HttpWebResponse)request.GetResponse();

            await using var rs = response.GetResponseStream();
            var responseString = new StreamReader(rs).ReadToEndAsync();
            Log.Debug("We got a response: " + responseString);    
            response.Dispose();
        }

        public void Dispose() {
            Dispose(true).ConfigureAwait(true);
            GC.SuppressFinalize(this);
        }


        protected virtual async Task Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (disposing) {
                if (Streaming) {
                    await StopStream();
                }
            }

            _disposed = true;
        }
    }
}