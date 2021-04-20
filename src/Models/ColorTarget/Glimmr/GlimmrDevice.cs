using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Glimmr.Models.ColorTarget.Glimmr {
    public class GlimmrDevice : ColorTarget, IColorTarget, IDisposable
    {
        public bool Enable { get; set; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }
        private bool _disposed;
        private static int port = 8889;
        private IPEndPoint _ep;
        private readonly List<Color> _updateColors;
        private readonly HttpClient _httpClient;
        private readonly UdpClient _udpClient;
        private int _offset;
        private int _len;
        private int _sectorCount;
        
        IColorTargetData IColorTarget.Data {
            get => Data;
            set => Data = (GlimmrData) value;
        }

        public GlimmrData Data { get; set; }
        
        public GlimmrDevice(GlimmrData wd, ColorService colorService) : base(colorService) {
            ColorService.ColorSendEvent += SetColor;
            _udpClient = ColorService.ControlService.UdpClient;
            _httpClient = ColorService.ControlService.HttpSender;
            _updateColors = new List<Color>();
            Data = wd ?? throw new ArgumentException("Invalid Glimmr data.");
            Id = Data.Id;
            Enable = Data.Enable;
            IpAddress = Data.IpAddress;
            _len = Data.LedCount;
            _sectorCount = Data.BottomSectorCount + Data.LeftSectorCount + Data.RightSectorCount + Data.TopSectorCount;
        }

        
        public async Task StartStream(CancellationToken ct) {
            if (Streaming) return;
            if (!Data.Enable) return;
            Log.Debug($"Glimmr: Starting stream at {IpAddress}...");
            await SendPost("mode", 5);
            _ep = IpUtil.Parse(IpAddress, port);
            Streaming = true;
            Log.Debug("Glimmr: Stream started.");
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
            Log.Debug("Glimmr: Stream stopped.");
            await SendPost("mode", 0);
        }


        public void SetColor(List<Color> leds, List<Color> sectors, int arg3, bool force=false) {
            
            if (!Streaming || !Data.Enable || Testing && !force) {
                return;
            }
            
            if (_ep == null) {
                Log.Debug("No endpoint.");
                return;
            }

            var packet = new List<byte>();
            packet.Add(ByteUtils.IntByte(2));
            packet.Add(ByteUtils.IntByte(255));
            foreach (var color in leds) {
                packet.Add(ByteUtils.IntByte(color.R));
                packet.Add(ByteUtils.IntByte(color.G));
                packet.Add(ByteUtils.IntByte(color.B));
            }
            foreach (var color in sectors) {
                packet.Add(ByteUtils.IntByte(color.R));
                packet.Add(ByteUtils.IntByte(color.G));
                packet.Add(ByteUtils.IntByte(color.B));
            }

            try {
                _udpClient.SendAsync(packet.ToArray(), packet.Count, _ep);
                ColorService.Counter.Tick(Id);
            } catch (Exception e) {
                Log.Debug("Exception: " + e.Message);        
            }
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
            Data = DataUtil.GetDevice<GlimmrData>(id);
            _len = Data.LedCount;
            Log.Debug($"Reloaded LED Data for {id}: " + JsonConvert.SerializeObject(Data));
            return Task.CompletedTask;
        }

        
        private async Task SendPost(string target, int value) {
            Uri uri;
            if (string.IsNullOrEmpty(IpAddress) && !string.IsNullOrEmpty(Id)) {
                IpAddress = Id;
                Data.IpAddress = Id;
            } 
            try {
                uri = new Uri("http://" + IpAddress + "/api/DreamData/" + target);
                Log.Debug($"Posting to {uri}");
            } catch (UriFormatException e) {
                Log.Warning("URI Format exception: " + e.Message);
                return;
            }

            var httpContent = new StringContent(value.ToString());
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            try {
                await _httpClient.PostAsync(uri, httpContent);
            } catch (Exception e) {
                Log.Warning("HTTP Request Exception: " + e.Message);
            }

            httpContent.Dispose();
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