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
        public int MaxBrightness { get; set; }
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
                CheckPositions(n);
            }

            disposed = false;
        }


        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<NanoData>("leaves", Id);
            SetData(newData);
            CheckPositions(newData, true);
        }

        private void SetData(NanoData n) {
            ipAddress = n.IpV4Address;
            token = n.Token;
            layout = n.Layout;
            MaxBrightness = n.MaxBrightness;
            var nanoType = n.Type;
            streamMode = nanoType == "NL29" ? 2 : 1;
            basePath = "http://" + ipAddress + ":16021/api/v1/" + token;
            Id = n.Id;
        }

        private void CheckPositions(NanoData n, bool force = false) {
            var pd = layout.PositionData;
            var autoCalc = pd.Any(pl => pl.Sector == -1);

            if (autoCalc || force) {
                LogUtil.Write("Automagically calculating panel positions.");
                var panelPositions = CalculatePoints(n);
                var newPd = new List<PanelLayout>();
                foreach (var pl in pd) {
                    if (pl.Sector == -1 || force) {
                        pl.Sector = panelPositions[pl.PanelId];
                    }

                    newPd.Add(pl);
                }

                layout.PositionData = newPd;
                n.Layout = layout;
                DataUtil.InsertCollection<NanoData>("leaves", n);
            }
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
            if (!Streaming) return;
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
                if (MaxBrightness < 100) {
                    color = ColorTransformUtil.ClampBrightness(color, MaxBrightness);
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


        private static Dictionary<int, int> CalculatePoints(NanoData nd) {
            if (nd == null) throw new ArgumentException("Invalid panel data.");
            var pl = nd.Layout;

            var pPoints = CalculatePanelPoints(nd);
            var sPoints = CalculateSectorPoints();
            // Set our marker at center

            LogUtil.Write("S points: " + JsonConvert.SerializeObject(sPoints));
            var tList = new Dictionary<int, int>();
            var panelInt = 0;
            foreach (var pPoint in pPoints) {
                double maxDist = double.MaxValue;
                var pTarget = -1;
                foreach (KeyValuePair<int, Point> kp in sPoints) {
                    var dX = kp.Value.X - pPoint.X;
                    var dY = kp.Value.Y - pPoint.Y;
                    var dist = Math.Sqrt(Math.Pow(dX, 2) + Math.Pow(dY, 2));
                    if (dist < maxDist) {
                        maxDist = dist;
                        pTarget = kp.Key;
                    }
                }

                if (pTarget != -1) {
                    var pId = pl.PositionData[panelInt].PanelId;
                    tList[pId] = pTarget;
                } else {
                    LogUtil.Write($@"Can't get target: {maxDist} {pTarget}");
                }

                panelInt++;
            }

            LogUtil.Write("Final List: " + JsonConvert.SerializeObject(tList));
            return tList;
        }

        private static List<PointD> CalculatePanelPoints(NanoData nd) {
            var pl = nd.Layout;
            var pPoints = new List<PointD>();
            var sideLength = pl.SideLength;
            var nX = nd.X;
            var nY = nd.Y;

            var minX = 1000;
            var minY = 1000;
            var maxX = 0;
            var maxY = 0;

            // Calculate the min/max range for each tile
            foreach (var data in pl.PositionData) {
                if (data.X < minX) minX = data.X;
                if (data.Y * -1 < minY) minY = data.Y * -1;
                if (data.X > maxX) maxX = data.X;
                if (data.Y * -1 > maxY) maxY = data.Y * -1;
            }

            var xRange = maxX - minX;
            var yRange = maxY - minY;

            foreach (var layout in pl.PositionData) {
                double pX = layout.X;
                double pY = layout.Y;
                if (nd.MirrorX) pX *= -1;
                if (!nd.MirrorY) pY *= -1;
                pX -= xRange / 2f;
                pY += yRange / 2f;
                pX -= sideLength / 2f;
                pY += sideLength / 2f;
                pX += nX;
                pY += nY;
                var p = new PointD(pX, pY);
                pPoints.Add(p);
            }

            LogUtil.Write("P points: " + JsonConvert.SerializeObject(pPoints));
            return pPoints;
        }

        private static Dictionary<int, Point> CalculateSectorPoints() {
            int verticalCount;
            int horizontalCount;
            var cMode = DataUtil.GetItem<int>("captureMode");
            var lD = DataUtil.GetItem<LedData>("ledData");
            if (cMode == 0) {
                verticalCount = lD.VCountDs;
                horizontalCount = lD.HCountDs;
            } else {
                verticalCount = lD.VCount;
                horizontalCount = lD.HCount;
            }

            // 1 LED = 25 tile units for nano

            // Set our TV image width
            var tvWidth = horizontalCount * 25;
            var tvHeight = verticalCount * 25;

            // Get vertical spaces between tiles
            var vStep = tvHeight / 3;
            // Get horizontal spaces between tiles
            var hStep = tvWidth / 5;

            // If window is less than 500px, divide our scale by half
            tvWidth /= 2;
            tvHeight /= 2;

            LogUtil.Write($"TvWidth, height {tvWidth}{tvHeight}");

            var sPoints = new Dictionary<int, Point>();
            var yMarker = vStep * -1; // Down one
            var xMarker = hStep * 2; // Right x2

            var pointInt = 1;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            yMarker += vStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            yMarker += vStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker -= hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker -= hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker -= hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker -= hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            yMarker -= vStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            yMarker -= vStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker += hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker += hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);
            pointInt++;
            xMarker += hStep;
            sPoints[pointInt] = new Point(xMarker, yMarker);

            LogUtil.Write("S points: " + JsonConvert.SerializeObject(sPoints));
            return sPoints;
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