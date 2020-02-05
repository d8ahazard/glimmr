using HueDream.Models.Util;
using Nanoleaf.Client;
using Nanoleaf.Client.Exceptions;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Accord.Math;
using Emgu.CV.Util;
using HueDream.Models.DreamGrab;
using MMALSharp.Ports.Outputs;
using Org.BouncyCastle.Utilities;

namespace HueDream.Models.Nanoleaf {
    public class Panel {

        private string IpAddress;
        private string Token;
        private string BasePath;
        private NanoLayout layout;
        private HttpClient HC;
        private string Nanotype;
        private int streamMode;
        private UdpClient uc;
        private Stopwatch clock;
        private long cycleTime;
        private List<List<Color>> Colors;
        private Dictionary<int, int> panelPositions;
        
        public Panel(string ipAddress, string token = "") {
            IpAddress = ipAddress;
            Token = token;
            BasePath = "http://" + IpAddress + ":16021/api/v1/" + Token;
            LogUtil.Write("Created");
            HC = new HttpClient();
        }

        public Panel(NanoData n) {
            LogUtil.Write("Creating nanopanel...");
            IpAddress = n.IpV4Address;
            Token = n.Token;
            layout = n.Layout;
            Nanotype = n.Type;
            streamMode = Nanotype == "NL29" ? 2 : 1;
            BasePath = "http://" + IpAddress + ":16021/api/v1/" + Token;
            LogUtil.Write("Created");
            HC = new HttpClient();
            clock = new Stopwatch();
            panelPositions = CalculatePoints(n);
        }

        private static Dictionary<int, int> CalculatePoints(NanoData nd) {
            var pl = nd.Layout;
            // calculate panel points in 2d space
            
            
            // calculate color points in 2d space?
            var sPoints = new Dictionary<int, Point>();
            var cMode = DreamData.GetItem<int>("captureMode");
            int vc;
            int hc;
            var lD = DreamData.GetItem<LedData>("ledData");
            if (cMode == 0) {
                vc = lD.VCountDs;
                hc = lD.HCountDs;
            } else {
                vc = lD.VCount;
                hc = lD.HCount;
            }

            // 1 LED = 25 tile units for nano
            var hd = hc * 25;
            var vd = vc * 25;
            // Get vertical spaces between tiles
            var vStep = vd / 3;
            // Get horizont spaces between tiles
            var hStep = hd / 5;
            
            // Center of screen
            hd /= 2;
            vd /= 2;
            LogUtil.Write($@"Center is {hd}, {vd}; steps are {hStep}, {vStep}");
            
            var pPoints = new List<Point>();
            // We need to calculate the min/max of the coords and adjust to center
            var minX = 0;
            var minY = 0;
            var maxX = 0;
            var maxY = 0;
            int adjX;
            int adjY;
            foreach (var layout in pl.PositionData) {
                int pX = layout.X;
                int pY = layout.Y;
                if (pX < minX) minX = pX;
                if (pY < minY) minY = pY;
                if (pX > maxX) maxX = pX;
                if (pY > maxY) maxY = pY;
            }

            var xRange = maxX - minX;
            var yRange = maxY - minY;
            LogUtil.Write($@"Ranges: {xRange}, {yRange}");
            adjX = (maxX - minX) / 2;
            adjY = (maxY - minY) / 2;
            foreach (var layout in pl.PositionData) {
                int pX = layout.X - adjX;
                int pY = layout.Y - adjY;
                LogUtil.Write($@"We could adjust by {adjX}, {adjY} or {minX}, {minY}");
                LogUtil.Write($@"PX, PY: {pX}, {pY}");
                pX += (int) nd.X;
                pY += (int) nd.Y;
                LogUtil.Write($@"PXadj, PYadj: {pX}, {pY}");

                var p = new Point(pX, pY);
                pPoints.Add(p);
            }
            
            // Set our marker at center
            var xMarker = 0;
            var yMarker = 0;

            yMarker -= vStep; // Down one
            xMarker += hStep * 2; // Right x2
            var pointInt = 0;
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
            LogUtil.Write("Ppoints: " + JsonConvert.SerializeObject(pPoints));
            LogUtil.Write("Spoints: " + JsonConvert.SerializeObject(sPoints));
            var tList = new Dictionary<int, int>();
            var panelInt = 0;
            foreach (var pPoint in pPoints) {
                double maxDist = double.MaxValue;
                var pTarget = -1;
                foreach(KeyValuePair<int, Point> kp in sPoints) {
                    var dX = kp.Value.X - pPoint.X;
                    var dY = kp.Value.Y - pPoint.Y;
                    var dist = Math.Sqrt(Math.Pow(dX, 2) + Math.Pow(dY, 2));
                    if (dist < maxDist) {
                        LogUtil.Write("Setting min to " + dist);
                        maxDist = dist;
                        pTarget = kp.Key;
                    }
                }
                if (pTarget != -1) {
                    LogUtil.Write($@"Target for {panelInt}: {maxDist} {pTarget}");
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

        public void DisableStreaming() {
            StopPanel();
        }

        public void EnableStreaming(CancellationToken ct) {
            StartPanel(ct);
            var controlVersion = "v" + streamMode;
            var body = new { write = new{command = "display", animType = "extControl", extControlVersion = controlVersion}};

        var startResult = SendPutRequest(JsonConvert.SerializeObject(body), "effects").Result;
            if (controlVersion == "v2") {
                LogUtil.Write("We should have no response here: " + startResult);
            }
            else {
                LogUtil.Write("We should have a body with stuff: " + startResult);
            }
            uc = new UdpClient();
        }

        public async void StartPanel(CancellationToken ct) {
            await SendPutRequest(JsonConvert.SerializeObject(new {on = new {value = true}}), "state");
            clock.Start();
            cycleTime = clock.ElapsedMilliseconds;
            while (!ct.IsCancellationRequested) {
                
            }
            StopPanel();

        }
        
        public async void StopPanel() {
            await SendPutRequest(JsonConvert.SerializeObject(new {on = new {value = false}}), "state");
        }

        public async void SetBrightness(int newValue) {
            if (newValue > 100) newValue = 100;
            if (newValue < 0) newValue = 0;
            await SendPutRequest(JsonConvert.SerializeObject(new {brightness = new {value = newValue}}), "state");
        }

        public void UpdateLights(Color[] colors) {
            var clockInt = clock.ElapsedMilliseconds;
            if (clockInt - cycleTime < 100.0) return;
            cycleTime = clockInt;
            var byteString = new List<byte>();
            if (streamMode == 2) {
                byteString.AddRange(ByteUtils.PadInt(layout.NumPanels));
            } else {
                byteString.Add(ByteUtils.IntByte(layout.NumPanels));
            }

            foreach (var pd in layout.PositionData) {
                var id = pd.PanelId;
                if (streamMode == 2) {
                    byteString.AddRange(ByteUtils.PadInt(id));
                } else {
                    byteString.Add(ByteUtils.IntByte(id));
                }

                var color = PickColor(id, colors);
                // Pad ID, this is probably wrong
                // Add rgb
                byteString.Add(ByteUtils.IntByte(color.R));
                byteString.Add(ByteUtils.IntByte(color.G));
                byteString.Add(ByteUtils.IntByte(color.B));
                // White value
                byteString.AddRange(ByteUtils.PadInt(0,1));
                // Pad duration time
                if (streamMode == 2) {
                    byteString.AddRange(ByteUtils.PadInt(1));
                } else {
                    // Fade Duration time
                    byteString.AddRange(ByteUtils.PadInt(1, 1));
                }
            }
            SendUdpUnicast(byteString.ToArray());
        }

        public Color PickColor(int id, Color[] colors) {
            var pInt = panelPositions[id];
            pInt += 1;
            if (pInt > 11) pInt = 0;
            return colors[pInt];
        }

        public async Task<UserToken> CheckAuth() {
            try {
                var nanoleaf = new NanoleafClient(IpAddress);
                return await nanoleaf.CreateTokenAsync();
            } catch (Exception) {

            }
            return null;
        }
        
        private void SendUdpUnicast(byte[] data) {
            var ep = IpUtil.Parse(IpAddress, 60222);
            var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sender.EnableBroadcast = false;
            sender.SendTo(data, ep);
            sender.Dispose();
        }

        public async Task<NanoLayout> GetLayout() {
            if (string.IsNullOrEmpty(Token)) return null;
            LogUtil.Write("Getting layout.");
            var layout = await SendGetRequest("panelLayout/layout");
            var lObject = JsonConvert.DeserializeObject<NanoLayout>(layout);
            LogUtil.Write("We got a layout: " + JsonConvert.SerializeObject(lObject));
            return lObject;
        }

        public async Task<string> SendPutRequest(string json, string path = "") {
            var authorizedPath = BasePath + "/" + path;
            using (var content = new StringContent(json, Encoding.UTF8, "application/json")) 
                using (var responseMessage = await HC.PutAsync(authorizedPath, content)) {
                    LogUtil.Write($@"Sending put request to {authorizedPath}: {json}");
                    if (!responseMessage.IsSuccessStatusCode) {
                        HandleNanoleafErrorStatusCodes(responseMessage);
                    }
                    LogUtil.Write("Returning");
                    return await responseMessage.Content.ReadAsStringAsync();
                }
        }

        public async Task<string> SendGetRequest(string path = "") {
            var authorizedPath = BasePath + "/" + path;
            LogUtil.Write("Auth path is : " + authorizedPath);
            using (var responseMessage = await HC.GetAsync(authorizedPath)) {
                if (!responseMessage.IsSuccessStatusCode) {
                    LogUtil.Write("Error code?");
                    HandleNanoleafErrorStatusCodes(responseMessage);
                }
                LogUtil.Write("Returning");
                return await responseMessage.Content.ReadAsStringAsync();
            }
        }
        private void HandleNanoleafErrorStatusCodes(HttpResponseMessage responseMessage) {
            switch ((int)responseMessage.StatusCode) {
                case 400:
                    throw new NanoleafHttpException("Error 400: Bad request!");
                case 401:
                    throw new NanoleafUnauthorizedException($"Error 401: Not authorized! Provided an invalid token for this Aurora. Request path: {responseMessage.RequestMessage.RequestUri.AbsolutePath}");
                case 403:
                    throw new NanoleafHttpException("Error 403: Forbidden!");
                case 404:
                    throw new NanoleafResourceNotFoundException($"Error 404: Resource not found! Request Uri: {responseMessage.RequestMessage.RequestUri.AbsoluteUri}");
                case 422:
                    throw new NanoleafHttpException("Error 422: Unprocessable Entity");
                case 500:
                    throw new NanoleafHttpException("Error 500: Internal Server Error");
                default:
                    throw new NanoleafHttpException("ERROR! UNKNOWN ERROR " + (int)responseMessage.StatusCode);
            }
        }
    }

    
}
