using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.DreamScreen.Devices;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.DreamScreen.Scenes;
using HueDream.Models.Hue;
using HueDream.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen {
    public class DreamClient : IHostedService, IDisposable
    {
        private static bool _searching;
        private readonly DreamScene dreamScene;
        private readonly byte group;
        private readonly string ambientColor;
        private readonly int ambientMode;
        private int ambientShow;
        private List<HueBridge> bridges;
        private int brightness;

        private BaseDevice dev;

        // Our functional values
        private int devMode;
        private IPEndPoint listenEndPoint;
        private UdpClient listener;

        private Task listenTask;

        // I don't know if we actually need all these
        private int prevAmbientMode;
        private int prevAmbientShow;

        // Used by our loops to know when to update?
        private int prevMode;
        
        // Token used within the show builder for the color refresh loop
        // Token passed to our hue bridges
        private CancellationTokenSource sendTokenSource;
        // Token used by our show builder for ambient scenes
        private CancellationTokenSource showBuilderSource;
        // Token used for the ds listen loop
        private CancellationTokenSource listenTokenSource;

        // Use this to check if we've initialized our bridges
        private bool streamStarted;
        
        // Use this to check if we've started our show builder
        private bool showBuilderStarted;
        private IPEndPoint targetEndpoint;

        public DreamClient() {
            var dd = DreamData.GetStore();
            dev = GetDeviceData();
            dreamScene = new DreamScene();
            devMode = dev.Mode;
            ambientMode = dev.AmbientModeType;
            ambientShow = dev.AmbientShowType;
            ambientColor = dev.AmbientColor;
            brightness = dev.Brightness;
            // Set these to "unset" states
            prevMode = -1;
            prevAmbientMode = -1;
            prevAmbientShow = -1;
            streamStarted = false;
            showBuilderStarted = false;
            string sourceIp = dd.GetItem("dsIp");
            group = (byte) dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            bridges = new List<HueBridge>();
        }


        private static List<BaseDevice> Devices { get; set; }
        
        public Task StartAsync(CancellationToken cancellationToken) {
            //LogUtil.WriteInc("DreamClient Started.");
            listenTokenSource = new CancellationTokenSource();
            listenTask = StartListening(listenTokenSource.Token);
            UpdateMode(dev.Mode);
            return listenTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken) {
            try {
                Console.WriteLine(@"StopAsync called.");
                CancelSource(listenTokenSource);
                StopStream();
                StopShowBuilder();
            }
            finally {
                LogUtil.WriteDec("DreamClient Stopped.");
                await Task.WhenAny(listenTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        private static BaseDevice GetDeviceData() {
            using var dd = DreamData.GetStore();
            BaseDevice myDev;
            var devType = dd.GetItem<string>("emuType");
            if (devType == "SideKick")
                myDev = dd.GetItem<SideKick>("myDevice");
            else
                myDev = dd.GetItem<Connect>("myDevice");
            return myDev;
        }

        private void UpdateMode(int newMode) {
            if (prevMode == newMode) return;
            dev = DreamData.GetDeviceData();
            dev.Mode = newMode;
            devMode = newMode;
            Console.WriteLine($@"DreamScreen: Updating mode to {newMode} from {prevMode}.");
            // If we are not in ambient mode and ambient scene is running, stop it
            switch (newMode) {
                case 0:
                    StopStream();
                    StopShowBuilder();
                    break;
                case 1:
                case 2:
                    Console.WriteLine($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                    if (!streamStarted) StartStream();
                    Subscribe();
                    break;
                case 3:
                    if (!streamStarted) StartStream();
                    UpdateAmbientMode(ambientMode);
                    break;
            }
        
            prevMode = newMode;
        }

        private void UpdateAmbientMode(int newMode) {
            if (prevAmbientMode == newMode) return;
            if (ambientMode == 0 && prevAmbientMode != -1) {
                // Cancel ambient task
                StopShowBuilder();
            }

            switch (ambientMode) {
                case 0:
                    UpdateAmbientColor(ambientColor);
                    break;
                case 1:
                    StartShowBuilder();
                    break;
            }

            prevAmbientMode = newMode;
        }

        private void UpdateAmbientShow(int newShow) {
            if (prevAmbientShow == newShow) return;
            StopShowBuilder();
            StartShowBuilder();
            dreamScene.LoadScene(newShow);
            prevAmbientShow = newShow;
        }

        private void UpdateBrightness(int newBrightness) {
            if (brightness == newBrightness) return;
            brightness = newBrightness;
            if (ambientMode == 0 && devMode == 3) {
                UpdateAmbientColor(ambientColor);
            }
        }

        private void StartStream() {
            if (!streamStarted) {
                var bridgeArray = DreamData.GetItem<List<BridgeData>>("bridges");
                bridges = new List<HueBridge>();
                sendTokenSource = new CancellationTokenSource();
                foreach (BridgeData bridge in bridgeArray) {
                    if (string.IsNullOrEmpty(bridge.Key) || string.IsNullOrEmpty(bridge.User) ||
                        bridge.SelectedGroup == "-1") continue;
                    var b = new HueBridge(bridge);
                    b.DisableStreaming();
                    b.EnableStreaming(sendTokenSource.Token);
                    bridges.Add(b);
                    LogUtil.Write("Starting stream on " + bridge.Ip);
                    streamStarted = true;
                }
            }

            if (streamStarted) LogUtil.WriteInc("Stream started.");
        }

        private void StopStream() {
            if (streamStarted) {
                foreach (var b in bridges) {
                    b.DisableStreaming();
                }
                CancelSource(sendTokenSource);
                LogUtil.WriteDec("Stream stopped.");
                streamStarted = false;
            }
        }

        public void SendColors(string[] colors, double fadeTime = 0) {
            //Console.WriteLine(@"Sending colors...");
            if (bridges.Count > 0) {
                Console.WriteLine(@"Updating colors: " + JsonConvert.SerializeObject(colors));
                foreach (var bridge in bridges)
                    bridge.UpdateLights(colors, brightness, sendTokenSource.Token, fadeTime);
            } else {
                Console.WriteLine(@"No bridges to update.");
            }
        }

       
        private void StartShowBuilder() {
            if (!showBuilderStarted) {
                dreamScene.LoadScene(ambientShow);
                showBuilderSource = new CancellationTokenSource();
                Task.Run(() => dreamScene.BuildColors(this, showBuilderSource.Token));
                prevAmbientShow = ambientShow;
                showBuilderStarted = true;
                LogUtil.WriteInc($"Started new ambient show {ambientShow}.");
            }
        }

        private void StopShowBuilder() {
            if (showBuilderStarted) {
                CancelSource(showBuilderSource);
                showBuilderStarted = false;
                LogUtil.WriteDec("Stopping show builder.");
            }
        }

        private void UpdateAmbientColor(string aColor) {
            // Re initialize, just in case
            StopShowBuilder();
            var colors = new string[12];
            for (var i = 0; i < colors.Length; i++) colors[i] = "FFFFFF";
            Console.WriteLine($@"DreamScreen: Setting ambient color to: {aColor}.");
            for (var i = 0; i < colors.Length; i++) colors[i] = aColor;
            SendColors(colors);
        }

        private void Subscribe() {
            DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, targetEndpoint);
        }


        private Task StartListening(CancellationToken ct) {
            try {
                listenEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                listener = new UdpClient();
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.EnableBroadcast = true;
                listener.Client.Bind(listenEndPoint);

                // Return listen task to kill later
                return Task.Run(() => {
                    LogUtil.WriteInc("Listener started.");
                    while (!ct.IsCancellationRequested) {
                        var sourceEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        var receivedResults = listener.Receive(ref sourceEndPoint);
                        ProcessData(receivedResults, sourceEndPoint);
                    }
                });
            }
            catch (SocketException e) {
                Console.WriteLine($@"Socket exception: {e.Message}.");
            }

            return null;
        }


        private void ProcessData(byte[] receivedBytes, IPEndPoint receivedIpEndPoint) {
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) return;
            //Console.WriteLine("DS: Brightness is " + Brightness);
            string command = null;
            string flag = null;
            var from = receivedIpEndPoint.Address.ToString();
            var replyPoint = new IPEndPoint(receivedIpEndPoint.Address, 8888);
            var payloadString = string.Empty;
            var payload = Array.Empty<byte>();
            BaseDevice msgDevice = null;
            var writeState = false;
            var msg = new DreamScreenMessage(receivedBytes, from);
            if (msg.IsValid) {
                payload = msg.GetPayload();
                payloadString = msg.PayloadString;
                command = msg.Command;
                msgDevice = msg.Device;
                string[] ignore = {"SUBSCRIBE", "READ_CONNECT_VERSION?", "COLOR_DATA", "DEVICE_DISCOVERY"};
                if (!ignore.Contains(command)) Console.WriteLine($@"{from} -> {JsonConvert.SerializeObject(msg)}.");
                flag = msg.Flags;
                var groupMatch = msg.Group == dev.GroupNumber || msg.Group == 255;
                if ((flag == "11" || flag == "21") && groupMatch) {
                    dev = GetDeviceData();
                    writeState = true;
                }
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (devMode == 1 || devMode == 2)
                        DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, replyPoint);
                    break;
                case "COLOR_DATA":
                    if (devMode == 1 || devMode == 2) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var lightCount = 0;
                        var colors = new string[12];
                        foreach (var colorValue in colorData) {
                            colors[lightCount] = colorValue;
                            if (lightCount > 11) break;
                            lightCount++;
                        }

                        SendColors(colors);
                    }

                    break;
                case "DEVICE_DISCOVERY":
                    if (flag == "30" && from != "0.0.0.0")
                        SendDeviceStatus(replyPoint);
                    else if (flag == "60")
                        if (msgDevice != null) {
                            string dsIpCheck = DreamData.GetItem("dsIp");
                            if (dsIpCheck == "0.0.0.0" &&
                                msgDevice.Tag.Contains("DreamScreen", StringComparison.CurrentCulture)) {
                                Console.WriteLine(@"No DS IP Set, setting.");
                                DreamData.SetItem("dsIp", from);
                                targetEndpoint = replyPoint;
                            }

                            if (_searching) Devices.Add(msgDevice);
                        }

                    break;
                case "GROUP_NAME":
                    var gName = Encoding.ASCII.GetString(payload);
                    if (writeState) dev.GroupName = gName;

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    if (writeState) dev.GroupNumber = gNum;

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (writeState) dev.Name = dName;

                    break;
                case "BRIGHTNESS":
                    brightness = payload[0];
                    if (writeState) {
                        Console.WriteLine($@"Setting brightness to {brightness}.");
                        dev.Brightness = payload[0];
                        UpdateBrightness(payload[0]);
                    }

                    break;
                case "SATURATION":
                    if (writeState) dev.Saturation = ByteUtils.ByteString(payload);

                    break;
                case "MODE":
                    if (writeState) {
                        dev.Mode = payload[0];
                        UpdateMode(dev.Mode);
                        Console.WriteLine($@"Updating mode: {dev.Mode}.");
                    }

                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState) {
                        dev.AmbientModeType = payload[0];
                        UpdateAmbientMode(dev.Mode);
                    }

                    break;
                case "AMBIENT_SCENE":
                    if (writeState) {
                        ambientShow = payload[0];
                        dev.AmbientShowType = ambientShow;
                        UpdateAmbientShow(ambientShow);
                        Console.WriteLine($@"Scene updated: {ambientShow}.");
                    }

                    break;
                case "AMBIENT_COLOR":
                    if (writeState) {
                        dev.AmbientColor = ByteUtils.ByteString(payload);
                        UpdateAmbientColor(dev.AmbientColor);
                    }

                    break;
            }

            if (writeState) DreamData.SetItem<BaseDevice>("myDevice", dev);
        }

        private void SendDeviceStatus(IPEndPoint src) {
            var dss = GetDeviceData();
            var payload = dss.EncodeState();
            DreamSender.SendUdpWrite(0x01, 0x0A, payload, 0x60, group, src);
        }


        public async Task<List<BaseDevice>> FindDevices() {
            _searching = true;
            Devices = new List<BaseDevice>();
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            DreamSender.SendUdpBroadcast(msg);
            var s = new Stopwatch();
            s.Start();
            await Task.Delay(3000).ConfigureAwait(true);
            Devices = Devices.Distinct().ToList();
            s.Stop();
            _searching = false;
            DreamData.SetItem("devices", Devices);
            return Devices;
        }

        private void CancelSource(CancellationTokenSource target) {
            if (target == null) return;
            if (!target.IsCancellationRequested) {
                target.Cancel();
            }
        }

        public void Dispose() {
            CancelSource(listenTokenSource);
            StopShowBuilder();
            StopStream();
            Dispose(true);
        }

        public void Dispose(bool finalize) {
            if (!finalize) GC.SuppressFinalize(this);
        }
    }
}