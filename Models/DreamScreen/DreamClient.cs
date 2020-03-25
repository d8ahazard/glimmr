using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.DreamGrab;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.DreamScreen.Scenes;
using HueDream.Models.Hue;
using HueDream.Models.Nanoleaf;
using HueDream.Models.Util;
using Microsoft.Extensions.Hosting;
using Nanoleaf.Client;
using Newtonsoft.Json;
using Q42.HueApi.ColorConverters;
using Serilog;

namespace HueDream.Models.DreamScreen {
    public sealed class DreamClient : IHostedService, IDisposable
    {
        public int CaptureMode { get; }
        private static bool _searching;
        private readonly DreamScene dreamScene;
        private readonly byte group;
        private readonly Color ambientColor;
        private readonly int ambientMode;
        private int ambientShow;
        private List<HueBridge> bridges;
        private List<Panel> panels;
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
        // Use this for capturing screen/video
        private CancellationTokenSource captureTokenSource;
        // Use this to check if we've initialized our bridges
        private bool streamStarted;

        private bool disposed;
        
        // Use this to check if we've started our show builder
        private bool showBuilderStarted;
        private IPEndPoint targetEndpoint;
        private readonly LedStrip strip;
        
        public DreamClient() {
            var dd = DreamData.GetStore();
            dev = DreamData.GetDeviceData();
            dreamScene = new DreamScene();
            devMode = dev.Mode;
            ambientMode = dev.AmbientModeType;
            ambientShow = dev.AmbientShowType;
            ambientColor = ColorFromString(dev.AmbientColor);
            brightness = dev.Brightness;
            CaptureMode = dd.GetItem<int>("captureMode");
            // Set these to "unset" states
            prevMode = -1;
            prevAmbientMode = -1;
            prevAmbientShow = -1;
            streamStarted = false;
            showBuilderStarted = false;
            string sourceIp = dd.GetItem("dsIp");
            if (CaptureMode != 0) {
                var ledData = dd.GetItem<LedData>("ledData") ?? new LedData(true);
                try {
                    strip = new LedStrip(ledData);
                } catch (TypeInitializationException e) {
                    LogUtil.Write("Type init error: " + e.Message);
                }
            }

            group = (byte) dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            disposed = false;
            bridges = new List<HueBridge>();
            panels = new List<Panel>();
            captureTokenSource = new CancellationTokenSource();
        }


        private static List<BaseDevice> Devices { get; set; }
        
        public Task StartAsync(CancellationToken cancellationToken) {
            var listenTask = StartListening(cancellationToken);
            UpdateMode(dev.Mode);
            return listenTask;
        }
              
        public Task StopAsync(CancellationToken cancellationToken) {
            try {
                LogUtil.Write("StopAsync called.");
                
                StopStream();
                StopShowBuilder();
                Dispose(true);
            }
            finally {
                LogUtil.WriteDec(@"DreamClient Stopped.");
            }

            return Task.CompletedTask;
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
                    StopShowBuilder();
                    StopStream();
                    if (!captureTokenSource.IsCancellationRequested) captureTokenSource.Cancel();
                    break;
                case 1:
                    StartVideoCaptureTask();
                    break;
                case 2:
                    StopShowBuilder();
                    StartAudioCaptureTask();
                    Console.WriteLine($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                    if (!streamStarted) StartStream();
                    Subscribe(true);
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


        private Color ColorFromString(string inputString) {
            return ColorTranslator.FromHtml("#" + inputString);
        }

        private void StartStream() {
            if (!streamStarted) {
                LogUtil.Write("Starting stream.");
                var bridgeArray = DreamData.GetItem<List<BridgeData>>("bridges");
                bridges = new List<HueBridge>();
                sendTokenSource = new CancellationTokenSource();
                foreach (BridgeData bridge in bridgeArray) {
                    if (string.IsNullOrEmpty(bridge.Key) || string.IsNullOrEmpty(bridge.User) ||
                        bridge.SelectedGroup == "-1") continue;
                    var b = new HueBridge(bridge);
                    b.DisableStreaming();
                    var enable = b.EnableStreaming(sendTokenSource.Token);
                    if (enable) {
                        bridges.Add(b);
                        LogUtil.Write("Stream started to " + bridge.IpAddress);
                        streamStarted = true;
                    }
                }

                var leaves = DreamData.GetItem<List<NanoData>>("leaves");
                var lX = 0;
                var lY = 0;
                
                panels = new List<Panel>();
                LogUtil.Write("Loading nano panels??");
                foreach (NanoData n in leaves) {
                    if (string.IsNullOrEmpty(n.Token) || n.Layout == null) continue;
                    var p = new Panel(n);
                    p.DisableStreaming();
                    p.EnableStreaming(sendTokenSource.Token);
                    panels.Add(p);
                    LogUtil.Write("Panel stream enabled: " + n.IpV4Address);
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

                if (strip != null) {
                    strip.StopLights();
                }
                CancelSource(captureTokenSource);
                CancelSource(sendTokenSource);
                LogUtil.WriteDec("Stream stopped.");
                streamStarted = false;
            }
        }

        public void SendColors(Color[] colors, Color[] sectors, double fadeTime = 0) {
            //Console.WriteLine(@"Sending colors: " + JsonConvert.SerializeObject(sectors));
            
            if (bridges.Count > 0) {
                foreach (var bridge in bridges) {
                    var bridgeSectors = new Color[12];
                    Array.Copy(sectors, bridgeSectors, 12);
                    bridge.UpdateLights(bridgeSectors, brightness, sendTokenSource.Token, fadeTime);
                }

                foreach (var p in panels) {
                    p.UpdateLights(sectors);
                }
            } else {
                Console.WriteLine(@"No bridges to update.");
            }
            if (strip != null) {                
                strip.UpdateAll(colors);
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
                LogUtil.WriteDec(@"Stopping show builder.");
            }
        }

        private void UpdateAmbientColor(Color aColor) {
            // Re initialize, just in case
            StopShowBuilder();
            var colors = new Color[12];
            for (var i = 0; i < colors.Length; i++) colors[i] = Color.Black;
            Console.WriteLine($@"DreamScreen: Setting ambient color to: {aColor.ToString()}.");
            for (var i = 0; i < colors.Length; i++) {
                colors[i] = aColor;
            }
            
            SendColors(colors, colors);
        }

        private void Subscribe(bool log = false) {
            if (log) LogUtil.Write(@"Sending subscribe message.");
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

        private Task StartVideoCapture(CancellationToken cancellationToken) {
            Console.WriteLine("Start video capture has been initialized.");
            var grabber = new DreamGrab.DreamGrab(this);
            LogUtil.Write("Grabber acquired.");
            var captureTask = grabber.StartCapture(cancellationToken);
            if (captureTask.IsCompleted) {
                return captureTask;
            }

            // Otherwise it's running
            return Task.CompletedTask;
        }

        private void StartVideoCaptureTask() {
            if (CaptureMode == 0) {
                Subscribe();
                if (!streamStarted) StartStream();
            } else {                
                if (!captureTokenSource.IsCancellationRequested) {
                    captureTokenSource.Cancel();
                }
                captureTokenSource = new CancellationTokenSource();
                if (!streamStarted) StartStream();
                Task.Run(() => StartVideoCapture(captureTokenSource.Token));
            }
        }

        private Task StartAudioCapture(CancellationToken cancellation) {
            return Task.CompletedTask;
        }

        private void StartAudioCaptureTask() {
            if (CaptureMode == 0) return;
            if (!captureTokenSource.IsCancellationRequested) {
                captureTokenSource.Cancel();
            }
            captureTokenSource = new CancellationTokenSource();
            Task.Run(() => StartAudioCapture(captureTokenSource.Token));
        }



        private void ProcessData(byte[] receivedBytes, IPEndPoint receivedIpEndPoint) {
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) return;
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
                    dev = DreamData.GetDeviceData();
                    writeState = true;
                }
            } else {
                LogUtil.Write("Invalid message?");
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (devMode == 1 || devMode == 2)
                        DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, replyPoint);
                    break;
                case "COLOR_DATA":
                    if (CaptureMode == 0 && (devMode == 1 || devMode == 2)) {
                        //LogUtil.Write("Raw payload: " + payload);
                        //LogUtil.Write("Raw payload string: " + payloadString);
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        //LogUtil.Write("Color data is " + colorData.Count + " long.");
                        var lightCount = 0;
                        var colors = new List<Color>();
                        
                        foreach (var colorValue in colorData) {
                            colors.Add(ColorFromString(colorValue));
                        }
                        SendColors(colors.ToArray(), colors.ToArray());
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
                        Console.WriteLine($@"Updating mode: {dev.Mode}.");
                        UpdateMode(dev.Mode);
                        
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
                        UpdateAmbientColor(ColorFromString(dev.AmbientColor));
                    }

                    break;
                case "SKU_SETUP":
                    if (writeState) {
                        LogUtil.Write("Setting SKU type?");
                        //dev.SkuSetup = payload[0];
                    }
                    break;
                case "FLEX_SETUP":
                    if (writeState) {
                        LogUtil.Write("Setting FlexSetup");
                        int[] fSetup = payload.Select(x => (int)x).ToArray();
                        dev.flexSetup = fSetup;
                    }
                    break;
                case "RESET_PIC":
                    LogUtil.Write("Reboot command issued!");
                    
                    break;
            }

            if (writeState) DreamData.SetItem<BaseDevice>("myDevice", dev);
        }

        private void SendDeviceStatus(IPEndPoint src) {
            var dss = DreamData.GetDeviceData();
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

        private void CancelSource(CancellationTokenSource target, bool dispose = false) {
            if (target == null) return;
            if (!target.IsCancellationRequested) {
                target.Cancel();                
            }
            if (dispose) target.Dispose();
        }


        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing) {
            if (disposed) {
                LogUtil.Write("Already disposed.");
                return;
            }

            if (disposing) {
                LogUtil.Write("DISPOSING NOW.");
                listener?.Dispose();
                //listenTask?.Dispose();
                CancelSource(sendTokenSource, true);
                CancelSource(showBuilderSource, true);
                CancelSource(listenTokenSource, true);
                CancelSource(captureTokenSource, true);
                LogUtil.Write("Tokens canceled.");
                foreach (var b in bridges) {
                    b.DisableStreaming();
                    b.Dispose();
                }
                LogUtil.Write("Bridges disposed.");
                foreach (var n in panels) {
                    n.DisableStreaming();                    
                }
                LogUtil.Write("Panels disabled.");
            }

            disposed = true;
            LogUtil.Write("Disposal complete.");
            Log.CloseAndFlush();
        }
    }
}