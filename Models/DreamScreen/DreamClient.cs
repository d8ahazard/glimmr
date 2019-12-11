using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.DreamScreen;
using HueDream.DreamScreen.Devices;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.DreamScreen.Scenes;
using HueDream.Models.Hue;
using HueDream.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen {
    public class DreamClient : IDisposable, IHostedService {
        private static bool _searching;
        private readonly DreamScene dreamScene;
        private readonly byte group;
        // Our functional values
        private int devMode;
        private int ambientMode;
        private int brightness;
        private int ambientShow;
        private string ambientColor;
        // Used by our loops to know when to update?
        private int prevMode;
        private int prevAmbientShow;
        private int prevAmbientMode;
        private int prevBrightness;
        private string prevAmbientColor;
        // I don't know if we actually need all these
        private CancellationTokenSource listenTokenSource;
        private CancellationTokenSource showBuilderSource;
        private CancellationTokenSource sendTokenSource;
        private CancellationTokenSource ambientSource;
        private BaseDevice dev;
        private IPEndPoint listenEndPoint;
        private UdpClient listener;
        private bool showStarted;
        private IPEndPoint targetEndpoint;
        private Task listenTask;
        private Task ambientTask;
        private Task sendTask;
        private List<HueBridge> bridges;
        private SceneBase sceneBase;

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
            prevBrightness = -1;
            prevAmbientColor = string.Empty;
            
            // Might be able to get rid of this
            showStarted = false;
            string sourceIp = dd.GetItem("dsIp");
            group = (byte) dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            sceneBase = null;
            bridges = new List<HueBridge>();
            showBuilderSource = new CancellationTokenSource();
            sendTokenSource = new CancellationTokenSource();
            ambientSource = new CancellationTokenSource();
            listenTokenSource = new CancellationTokenSource();
            UpdateMode(dev.Mode);
        }

        
        private static List<BaseDevice> Devices { get; set; }
        public void Dispose() { }
        
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
            dev = DreamData.GetDeviceData();
            if (prevMode == newMode) return;
            dev.Mode = newMode;
            devMode = newMode;
            Console.WriteLine($@"DreamScreen: Updating mode to {dev.Mode}.");
            // If we are not in ambient mode and ambient scene is running, stop it
            if (newMode != 3 && ambientTask != null && ambientTask.IsCompleted) {
                ambientSource.Cancel();
            }
            // If we're running anything, stop it
            if (newMode == 0) {
                if (!sendTokenSource.IsCancellationRequested) sendTokenSource.Cancel();
                if (!ambientSource.IsCancellationRequested) ambientSource.Cancel();
            } else {
                switch (newMode) {
                    // If we're using video or music, send our first subscribe message
                    case 1:
                    case 2:
                        Console.WriteLine($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                        Subscribe();
                        break;
                    case 3:
                        UpdateAmbientMode(ambientMode);
                        break;
                }

                // If we are not sending color data, start
                if (sendTokenSource.IsCancellationRequested) {
                    sendTokenSource = new CancellationTokenSource();
                    StartSync(sendTokenSource.Token);
                }
            }
        }

        private void UpdateAmbientMode(int newMode) {
            if (prevAmbientMode == newMode && prevAmbientMode != -1) return;
            if (ambientMode == 0) {
                // Cancel ambient task
                if (ambientTask != null && !ambientTask.IsCompleted) {
                    ambientSource.Cancel();
                }
                UpdateAmbientColor(ambientColor);
            }

            if (ambientMode == 1) {
                if (ambientTask == null || ambientTask.IsCompleted) {
                    ambientSource = new CancellationTokenSource();
                    ambientTask = Task.Run(() => CheckShow(ambientSource.Token));
                }
            }
            prevAmbientMode = newMode;
        }

        private void UpdateAmbientShow(int newShow) {
            if (prevAmbientShow == newShow || ambientTask.IsCompleted) return;
            dreamScene.LoadScene(newShow);
            prevAmbientShow = ambientShow;
        }

        private void StartSync(CancellationToken ct) {
            Console.Write(@"DreamClient: Starting sync...");
            var bridgeArray = DreamData.GetItem<List<BridgeData>>("bridges");
            bridges = new List<HueBridge>();
            foreach (BridgeData bridge in bridgeArray) {
                if (string.IsNullOrEmpty(bridge.Key) || string.IsNullOrEmpty(bridge.User) || bridge.SelectedGroup == "-1") continue;
                var b = new HueBridge(bridge);
                b.EnableStreaming(ct);
                bridges.Add(b);
            }
            Console.WriteLine($@"DreamClient: Added {bridges.Count} bridges to control.");
        }

       

        public void SendColors(string[] colors, double fadeTime=0) {
            Console.WriteLine(@"Sending colors...");
            if (bridges.Count > 0) {
                Console.WriteLine(@"Updating colors: " + JsonConvert.SerializeObject(colors));
                foreach(var bridge in bridges) {
                    bridge.UpdateLights(colors, brightness, sendTokenSource.Token, fadeTime);
                }
            } else {
                Console.WriteLine(@"No bridges to update.");
            }
        }

        private void CheckShow(CancellationToken sCt) {
            Console.WriteLine(@"Check show Started.");
            showBuilderSource = new CancellationTokenSource();
            showStarted = false;
            while (!sCt.IsCancellationRequested) {
                // If we're still in ambient mode
                if (devMode == 3) {
                    if (ambientMode == 1) {
                        // Start our show generation if it's not running.
                        if (!showStarted) {
                            Console.WriteLine($@"Starting new ambient show: {ambientShow}.");
                            dreamScene.LoadScene(ambientShow);
                            Task.Run(() => dreamScene.BuildColors(this, showBuilderSource.Token));
                            prevAmbientShow = ambientShow;
                            showStarted = true;
                        }
                    } else {
                        if (prevAmbientColor != dev.AmbientColor) {
                            prevAmbientColor = dev.AmbientColor;
                            UpdateAmbientColor(dev.AmbientColor);
                        }
                    }
                }

                // If our ambient mode is not 1 and the show is running...
                if (devMode == 3 && ambientMode == 1 || !showStarted) continue;
                Console.WriteLine($@"Stopping ambient show: {ambientShow}.");
                showStarted = false;
                sceneBase = null;
                showBuilderSource.Cancel();
                showBuilderSource = new CancellationTokenSource();
            }
            Console.WriteLine(@"Show builder stopped Done");
        }

        private void UpdateAmbientColor(string aColor) {
            // Re initialize, just in case
            var colors = new string[12];
            for (var i = 0; i < colors.Length; i++) colors[i] = "FFFFFF";
            Console.WriteLine($@"DreamScreen: Setting ambient color to: {aColor}.");
            for (var i = 0; i < colors.Length; i++) colors[i] = aColor;
            SendColors(colors);
        }

        public void Subscribe() {
            DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, targetEndpoint);
        }

        
        public Task StartListening(CancellationToken ct) {
            try {
                listenEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                listener = new UdpClient();
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.EnableBroadcast = true;
                listener.Client.Bind(listenEndPoint);
                
                // Return listen task to kill later
                return Task.Run(() => {
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

                if (!groupMatch) Console.WriteLine($@"Group {msg.Group} doesn't match {dev.GroupNumber}.");
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
                        ambientShow = payload[0];;
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

        public Task StartAsync(CancellationToken cancellationToken) {
            listenTask = StartListening(listenTokenSource.Token);
            return listenTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken) {
            try {
                listenTokenSource.Cancel();
                showBuilderSource.Cancel();
                sendTokenSource.Cancel();
                ambientSource.Cancel();
            } finally {
                await Task.WhenAny(listenTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }
    }
}