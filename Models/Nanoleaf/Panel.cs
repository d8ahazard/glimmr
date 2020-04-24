﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.LED;
using HueDream.Models.Util;
using Nanoleaf.Client;
using Nanoleaf.Client.Exceptions;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;
using ZedGraph;

namespace HueDream.Models.Nanoleaf {
    public sealed class Panel : IDisposable {
        private readonly string ipAddress;
        private readonly string token;
        private readonly string basePath;
        private readonly NanoLayout layout;
        private readonly HttpClient hc;
        private readonly int streamMode;
        private readonly Stopwatch clock;
        private long cycleTime;
        private readonly Dictionary<int, int> panelPositions;
        private bool disposed;

        public Panel(string ipAddress, string token = "") {
            this.ipAddress = ipAddress;
            this.token = token;
            basePath = "http://" + this.ipAddress + ":16021/api/v1/" + this.token;
            hc = new HttpClient();
            disposed = false;
        }

        public Panel(NanoData n) {
            if (n != null) {
                ipAddress = n.IpV4Address;
                token = n.Token;
                layout = n.Layout;
                var nanoType = n.Type;
                streamMode = nanoType == "NL29" ? 2 : 1;
                basePath = "http://" + ipAddress + ":16021/api/v1/" + token;
                hc = new HttpClient();
                clock = new Stopwatch();
                var pd = layout.PositionData;
                bool autoCalc = false;
                foreach (var pl in pd) {
                    if (pl.Sector == -1) {
                        autoCalc = true;
                        break;
                    }
                }

                if (autoCalc) {
                    LogUtil.Write("Automagically calculating panel positions.");
                    panelPositions = CalculatePoints(n);
                    var newPd = new List<PanelLayout>();
                    foreach (var pl in pd) {
                        if (pl.Sector == -1) {
                            pl.Sector = panelPositions[pl.PanelId];
                        }

                        newPd.Add(pl);
                    }

                    layout.PositionData = newPd;
                    var leaves = DataUtil.GetItem<List<NanoData>>("leaves");
                    var newLeaves = new List<NanoData>();
                    foreach (var leaf in leaves) {
                        if (leaf.Id == n.Id) {
                            leaf.Layout = layout;
                        }

                        newLeaves.Add(leaf);
                    }

                    DataUtil.SetItem<List<NanoData>>("leaves", newLeaves);
                }
            }

            disposed = false;
        }

        public static Dictionary<int, int> CalculatePoints(NanoData nd) {
            if (nd == null) throw new ArgumentException("Invalid panel data.");
            var pl = nd.Layout;
            
            var pPoints = CalculatePanelPoints(nd);
            var sPoints = CalculateSectorPoints(nd);
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
            foreach(var data in pl.PositionData) {
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
                pX -= xRange / 2;
                pY += yRange / 2;
                pX -= sideLength / 2;
                pY += sideLength / 2;
                pX += nX;
                pY += nY;
                var p = new PointD(pX, pY);
                pPoints.Add(p);
            }
            LogUtil.Write("P points: " + JsonConvert.SerializeObject(pPoints));
            return pPoints;
        }

        private static Dictionary<int, Point> CalculateSectorPoints(NanoData nd) {
            
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
            
            // Get window width
            var width = 1024;
            var height = 768;
    
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
        public void DisableStreaming() {
            StopPanel();
        }

        public void EnableStreaming(CancellationToken ct) {
            StartPanel(ct);
            var controlVersion = "v" + streamMode;
            var body = new
                {write = new {command = "display", animType = "extControl", extControlVersion = controlVersion}};

            var startResult = SendPutRequest(JsonConvert.SerializeObject(body), "effects").Result;
            if (controlVersion == "v2") {
                LogUtil.Write("We should have no response here: " + startResult);
            } else {
                LogUtil.Write("We should have a body with stuff: " + startResult);
            }
        }

        private async void StartPanel(CancellationToken ct) {
            await SendPutRequest(JsonConvert.SerializeObject(new {on = new {value = true}}), "state").ConfigureAwait(false);
            clock.Start();
            cycleTime = clock.ElapsedMilliseconds;
            LogUtil.WriteInc($@"Nanoleaf: Starting panel: {ipAddress}");
            while (!ct.IsCancellationRequested) {
            }

            LogUtil.WriteDec($@"Nanoleaf: Stopped panel: {ipAddress}");
            StopPanel();
        }

        private async void StopPanel() {
            await SendPutRequest(JsonConvert.SerializeObject(new {on = new {value = false}}), "state").ConfigureAwait(false);
        }

        public async void SetBrightness(int newValue) {
            if (newValue > 100) newValue = 100;
            if (newValue < 0) newValue = 0;
            await SendPutRequest(JsonConvert.SerializeObject(new {brightness = new {value = newValue}}), "state").ConfigureAwait(false);
        }

        public void UpdateLights(List<Color> colors) {
            if (colors == null || colors.Count < 12) {
                throw new ArgumentException("Invalid color list.");
            }
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
                var colorInt = pd.Sector - 1;
                if (streamMode == 2) {
                    byteString.AddRange(ByteUtils.PadInt(id));
                } else {
                    byteString.Add(ByteUtils.IntByte(id));
                }

                if (pd.Sector == -1) continue;
                var color = colors[colorInt];
                //LogUtil.Write("Sending sector " + (sector + 1) + " out of " + colors.Length);
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
                LogUtil.Write("Unauthorized...");
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
            LogUtil.Write("Getting layout.");
            var fLayout = await SendGetRequest("panelLayout/layout").ConfigureAwait(false);
            var lObject = JsonConvert.DeserializeObject<NanoLayout>(fLayout);
            LogUtil.Write("We got a layout: " + JsonConvert.SerializeObject(lObject));
            return lObject;
        }

        private async Task<string> SendPutRequest(string json, string path = "") {
            var authorizedPath = new Uri(basePath + "/" + path);
            try {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var responseMessage = await hc.PutAsync(authorizedPath, content).ConfigureAwait(false);
                LogUtil.Write($@"Sending put request to {authorizedPath}: {json}");
                if (!responseMessage.IsSuccessStatusCode) {
                    HandleNanoleafErrorStatusCodes(responseMessage);
                }

                return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (HttpRequestException) {
                return null;
            }
        }

        private async Task<string> SendGetRequest(string path = "") {
            var authorizedPath = basePath + "/" + path;
            var uri = new Uri(authorizedPath);
            try {
                using var responseMessage = await hc.GetAsync(uri).ConfigureAwait(false);
                if (responseMessage.IsSuccessStatusCode)
                    return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogUtil.Write("Error contacting nanoleaf: " + responseMessage.Content);
                HandleNanoleafErrorStatusCodes(responseMessage);

                return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (HttpRequestException) {
                return null;
            }
        }

        private static void HandleNanoleafErrorStatusCodes(HttpResponseMessage responseMessage) {
            throw (int) responseMessage.StatusCode switch {
                400 => new NanoleafHttpException("Error 400: Bad request!"),
                401 => new NanoleafUnauthorizedException(
                    $"Error 401: Not authorized! Provided an invalid token for this Aurora. Request path: {responseMessage.RequestMessage.RequestUri.AbsolutePath}"),
                403 => new NanoleafHttpException("Error 403: Forbidden!"),
                404 => new NanoleafResourceNotFoundException(
                    $"Error 404: Resource not found! Request Uri: {responseMessage.RequestMessage.RequestUri.AbsoluteUri}"),
                422 => new NanoleafHttpException("Error 422: Unprocessable Entity"),
                500 => new NanoleafHttpException("Error 500: Internal Server Error"),
                _ => new NanoleafHttpException("ERROR! UNKNOWN ERROR " + (int) responseMessage.StatusCode)
            };
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
            hc.Dispose();
        }
    }
}