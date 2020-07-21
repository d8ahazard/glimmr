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
        private string ipAddress;
        private string token;
        private string basePath;
        private NanoLayout layout;
        private readonly HttpClient hc;
        private int streamMode;
        private bool disposed;
        public int Brightness { get; set; }
        public string Id { get; set; }

        public NanoGroup(string ipAddress, string token = "") {
            this.ipAddress = ipAddress;
            this.token = token;
            basePath = "http://" + this.ipAddress + ":16021/api/v1/" + this.token;
            disposed = false;
        }

        public NanoGroup(NanoData n) {
            if (n != null) {
                SetData(n);
                hc = new HttpClient();
            }

            disposed = false;
        }


        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<NanoData>("leaves", Id);
            SetData(newData);
        }

        private void SetData(NanoData n) {
            ipAddress = n.IpV4Address;
            token = n.Token;
            layout = n.Layout;
            Brightness = n.Brightness;
            var nanoType = n.Type;
            streamMode = nanoType == "NL29" ? 2 : 1;
            basePath = "http://" + ipAddress + ":16021/api/v1/" + token;
            Id = n.Id;
        }

        private void CheckPositions(NanoData n, bool force = false) {
            var pd = layout.PositionData;
            
        }

        public bool Streaming { get; set; }

        public async void StartStream(CancellationToken ct) {
            LogUtil.WriteInc($@"Nanoleaf: Starting panel: {ipAddress}");
            var controlVersion = "v" + streamMode;
            var body = new
                {write = new {command = "display", animType = "extControl", extControlVersion = controlVersion}};

            await NanoSender.SendPutRequest(basePath, JsonConvert.SerializeObject(new {on = new {value = true}}),
                "state");
            await NanoSender.SendPutRequest(basePath, JsonConvert.SerializeObject(body), "effects");
            LogUtil.Write("Nanoleaf: Streaming is active.");
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }

            LogUtil.WriteDec($@"Nanoleaf: Stopped panel: {ipAddress}");
            StopStream();
        }

        public void StopStream() {
            Streaming = false;
            NanoSender.SendPutRequest(basePath, JsonConvert.SerializeObject(new {on = new {value = false}}), "state")
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
            if (streamMode == 2) {
                byteString.AddRange(ByteUtils.PadInt(layout.NumPanels));
            } else {
                byteString.Add(ByteUtils.IntByte(layout.NumPanels));
            }
            foreach (var pd in layout.PositionData) {
                var id = pd.PanelId;
                var colorInt = pd.Sector - 1;
                if (streamMode == 2) {
                    byteString.AddRange(ByteUtils.PadInt(id));
                } else {
                    byteString.Add(ByteUtils.IntByte(id));
                }
                
                if (pd.Sector == -1) continue;
                var color = colors[colorInt];
                if (Brightness < 100) {
                    color = ColorTransformUtil.ClampBrightness(color, Brightness);
                }

                // Pad ID, this is probably wrong
                // Add rgb
                byteString.Add(ByteUtils.IntByte(color.R));
                byteString.Add(ByteUtils.IntByte(color.G));
                byteString.Add(ByteUtils.IntByte(color.B));
                // White value
                byteString.AddRange(ByteUtils.PadInt(0, 1));
                // Pad duration time
                byteString.AddRange(streamMode == 2 ? ByteUtils.PadInt(1) : ByteUtils.PadInt(1, 1));
            }
            SendUdpUnicast(byteString.ToArray());
        }


     
        public async Task<UserToken> CheckAuth() {
            var nanoleaf = new NanoleafClient(ipAddress);
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
            var ep = IpUtil.Parse(ipAddress, 60222);
            var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sender.EnableBroadcast = false;
            sender.SendTo(data, ep);
            sender.Dispose();
        }

        public async Task<NanoLayout> GetLayout() {
            if (string.IsNullOrEmpty(token)) return null;
            var fLayout = await NanoSender.SendGetRequest(basePath, "panelLayout/layout").ConfigureAwait(false);
            var lObject = JsonConvert.DeserializeObject<NanoLayout>(fLayout);
            return lObject;
        }


        public void Dispose() {
            Dispose(true);
        }

        private void Dispose(bool disposing) {
            if (disposed) {
                return;
            }

            if (!disposing) return;
            LogUtil.Write("Panel Disposed.");
            disposed = true;
            hc?.Dispose();
        }
    }
}