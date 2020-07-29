using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.LED;
using HueDream.Models.Util;
using Nanoleaf.Client;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;
using ZedGraph;

namespace HueDream.Models.StreamingDevice.Nanoleaf {
    public sealed class NanoGroup : IStreamingDevice, IDisposable {
        private string _ipAddress;
        private string _token;
        private string _basePath;
        private NanoLayout _layout;
        private int _streamMode;
        private bool _disposed;
        private bool _sending;
        public int Brightness { get; set; }
        public string Id { get; set; }
        private HttpClient _hc;
        private readonly Socket _sender;


        public NanoGroup(string ipAddress, string token = "") {
            _ipAddress = ipAddress;
            _token = token;
            _hc = new HttpClient();
            _basePath = "http://" + _ipAddress + ":16021/api/v1/" + _token;
            _disposed = false;
        }

        public NanoGroup(NanoData n, HttpClient hc, Socket hs) {
            if (n != null) {
                SetData(n);
                _hc = hc;
                _sender = hs;
            }

            _disposed = false;
        }


        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<NanoData>("leaves", Id);
            SetData(newData);
        }

        private void SetData(NanoData n) {
            _ipAddress = n.IpV4Address;
            _token = n.Token;
            _layout = n.Layout;
            Brightness = n.Brightness;
            var nanoType = n.Type;
            _streamMode = nanoType == "NL29" ? 2 : 1;
            _basePath = "http://" + _ipAddress + ":16021/api/v1/" + _token;
            Id = n.Id;
        }

        
        public bool Streaming { get; set; }

        public async void StartStream(CancellationToken ct) {
            LogUtil.WriteInc($@"Nanoleaf: Starting panel: {_ipAddress}");
            // Turn it on first.
            var currentState = NanoSender.SendGetRequest(_basePath).Result;
            LogUtil.Write("Current state: " + currentState);
            //await NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = true}}),
                //"state");
            var controlVersion = "v" + _streamMode;
            var body = new
                {write = new {command = "display", animType = "extControl", extControlVersion = controlVersion}};

            await NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = true}}),
                "state");
            await NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(body), "effects");
            LogUtil.Write("Nanoleaf: Streaming is active.");
            _sending = true;
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }
            _sending = false;
            LogUtil.WriteDec($@"Nanoleaf: Stopped panel: {_ipAddress}");
            StopStream();
        }

        public void StopStream() {
            Streaming = false;
            NanoSender.SendPutRequest(_basePath, JsonConvert.SerializeObject(new {on = new {value = false}}), "state")
                .ConfigureAwait(false);
        }


        public void SetColor(List<Color> colors, double fadeTime = 0) {
            if (!Streaming) {
                LogUtil.Write("Streaming is  not active?");
                return;
            }
            if (colors == null || colors.Count < 12) {
                throw new ArgumentException("Invalid color list.");
            }

            var byteString = new List<byte>();
            if (_streamMode == 2) {
                byteString.AddRange(ByteUtils.PadInt(_layout.NumPanels));
            } else {
                byteString.Add(ByteUtils.IntByte(_layout.NumPanels));
            }
            foreach (var pd in _layout.PositionData) {
                var id = pd.PanelId;
                var colorInt = pd.Sector - 1;
                if (_streamMode == 2) {
                    byteString.AddRange(ByteUtils.PadInt(id));
                } else {
                    byteString.Add(ByteUtils.IntByte(id));
                }

                if (pd.Sector == -1) continue;
                //LogUtil.Write("Sector for light " + id + " is " + pd.Sector);
                var color = colors[colorInt];
                if (Brightness < 100) {
                    color = ColorTransformUtil.ClampBrightness(color, Brightness);
                }

                // Add rgb values
                byteString.Add(ByteUtils.IntByte(color.R));
                byteString.Add(ByteUtils.IntByte(color.G));
                byteString.Add(ByteUtils.IntByte(color.B));
                // White value
                byteString.AddRange(ByteUtils.PadInt(0, 1));
                // Pad duration time
                byteString.AddRange(_streamMode == 2 ? ByteUtils.PadInt(1) : ByteUtils.PadInt(1, 1));
            }
            SendUdpUnicast(byteString.ToArray());
        }


     
        public async Task<UserToken> CheckAuth() {
            var nanoleaf = new NanoleafClient(_ipAddress);
            UserToken result = null;
            try {
                result = await nanoleaf.CreateTokenAsync().ConfigureAwait(false);
                LogUtil.Write("Authorized.");
            } catch (AggregateException e) {
                LogUtil.Write("Unauthorized Exception: " + e.Message);
            }

            nanoleaf.Dispose();
            return result;
        }

        private void SendUdpUnicast(byte[] data) {
            if (!_sending) return;
            var ep = IpUtil.Parse(_ipAddress, 60222);
            _sender.SendTo(data, ep);
        }

        public async Task<NanoLayout> GetLayout() {
            if (string.IsNullOrEmpty(_token)) return null;
            var fLayout = await NanoSender.SendGetRequest(_basePath, "panelLayout/layout").ConfigureAwait(false);
            var lObject = JsonConvert.DeserializeObject<NanoLayout>(fLayout);
            return lObject;
        }


        public void Dispose() {
            Dispose(true);
        }

        private void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (!disposing) return;
            LogUtil.Write("Panel Disposed.");
            _disposed = true;
        }
    }
}