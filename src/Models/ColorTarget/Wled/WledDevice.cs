﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
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
        
        private List<List<Color>> _frameBuffer;
        private int _frameDelay;
        
        IColorTargetData IColorTarget.Data {
            get => Data;
            set => Data = (WledData) value;
        }

        public WledData Data { get; set; }
        
        public WledDevice(WledData wd, ColorService colorService) : base(colorService) {
            colorService.ColorSendEvent += SetColor;
            _udpClient = ColorService.ControlService.UdpClient;
            _httpClient = ColorService.ControlService.HttpSender;
            _updateColors = new List<Color>();
            Data = wd ?? throw new ArgumentException("Invalid WLED data.");
            Id = Data.Id;
            Brightness = Data.Brightness;
            ReloadData();
        }

        

        public async Task StartStream(CancellationToken ct) {
            if (Streaming) return;
            if (!Data.Enable) return;
            _frameBuffer = new List<List<Color>>();
            Log.Debug($"WLED: Starting stream at {IpAddress}...");
            _ep = IpUtil.Parse(IpAddress, port);
            Streaming = true;
            UpdateLightState(Streaming).ConfigureAwait(false);
            Log.Debug("WLED: Stream started.");
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
            await UpdateLightState(Streaming).ConfigureAwait(false);
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

            if (_frameDelay > 0) {
                _frameBuffer.Add(colors);
                if (_frameBuffer.Count < _frameDelay) return; // Just buffer till we reach our count
                colors = _frameBuffer[0];
                _frameBuffer.RemoveAt(0);	
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
                ColorService.Counter.Tick(Id);
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
            var oldBrightness = Brightness;
            Data = DataUtil.GetDevice<WledData>(Id);
            _offset = Data.Offset;
            Brightness = Data.Brightness;
            IpAddress = Data.IpAddress;
            if (oldBrightness != Brightness) {
                Log.Debug($"Brightness has changed!! {oldBrightness} {Brightness}");
                UpdateLightState(Streaming).ConfigureAwait(false);
            } else {
                Log.Debug($"Nothing to update for brightness {oldBrightness} {Brightness}");
            }
            _len = Data.LedCount;
            _frameDelay = Data.FrameDelay;
            _frameBuffer = new List<List<Color>>();
            return Task.CompletedTask;
        }

        public async Task UpdateLightState(bool on) {
            var url = "http://" + IpAddress + "/win";
            url += "&T=" + (on ? "1" : "0");
            url += "&A=" + (int)(Brightness / 100f * 255);
            Log.Debug("GETTIN: " + url);
            await _httpClient.GetAsync(url).ConfigureAwait(false);
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