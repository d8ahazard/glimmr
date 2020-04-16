using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.DreamGrab;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Hue;
using HueDream.Models.Nanoleaf;
using HueDream.Models.Util;
using Microsoft.Extensions.Hosting;
using MMALSharp.Native;
using Newtonsoft.Json;
using Org.BouncyCastle.Cms;

namespace HueDream.Models.DreamScreen {
    public class DreamClient : BackgroundService {
        private int CaptureMode { get; }
        private static bool _searching;
        private readonly DreamScene dreamScene;
        private readonly byte group;
        private readonly Color ambientColor;
        public int ambientMode;
        private int ambientShow;
        private List<HueBridge> bridges;
        private List<Panel> panels;
        private int brightness;
        private StreamCapture grabber;
        private BaseDevice dev;

        // Our functional values
        private int devMode;
        
        private IPEndPoint listenEndPoint;
        private UdpClient listener;

        // I don't know if we actually need all these
        private int prevAmbientMode;
        private int prevAmbientShow;

        // Used by our loops to know when to update?
        private int prevMode;

        // Token passed to our hue bridges
        private CancellationTokenSource sendTokenSource;

        // Token used by our show builder for ambient scenes
        private CancellationTokenSource showBuilderSource;

        // Use this for capturing screen/video
        private CancellationTokenSource captureTokenSource;

        // Use this for the camera
        private readonly CancellationTokenSource camTokenSource;

        // Use this to check if we've initialized our bridges
        private bool streamStarted;

        
        // Use this to check if we've started our show builder
        private bool showBuilderStarted;
        private IPEndPoint targetEndpoint;
        private LedStrip strip;
        
        private List<BaseDevice> Devices { get; set; }
        public DreamClient() {
            DreamData.CheckDefaults(this);
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
            group = (byte) dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            bridges = new List<HueBridge>();
            panels = new List<Panel>();
            captureTokenSource = new CancellationTokenSource();
            camTokenSource = new CancellationTokenSource();
        }

        
        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            LogUtil.WriteInc("Starting DreamClient services.");

            if (CaptureMode != 0) {
                grabber = new StreamCapture(camTokenSource.Token);
                var ledData = DreamData.GetItem<LedData>("ledData") ?? new LedData(true);
                try {
                    strip = new LedStrip(ledData);
                }
                catch (TypeInitializationException e) {
                    LogUtil.Write("Type init error: " + e.Message);
                }
            }

            var lt = StartListening(cancellationToken);
            LogUtil.Write("Listener retrieved.");
            UpdateMode(dev.Mode);
            LogUtil.Write("Updating device mode on startup.");
            return lt;
        }


        public void UpdateMode(int newMode) {
            if (prevMode == newMode) return;
            dev = DreamData.GetDeviceData();
            dev.Mode = newMode;
            devMode = newMode;
            LogUtil.Write($@"DreamScreen: Updating mode to {newMode} from {prevMode}.");
            // If we are not in ambient mode and ambient scene is running, stop it
            switch (newMode) {
                case 0:
                    StopShowBuilder();
                    StopStream();
                    CancelSource(captureTokenSource);
                    break;
                case 1:
                    StopShowBuilder();
                    StartVideoCaptureTask();
                    break;
                case 2:
                    StopShowBuilder();
                    StartAudioCaptureTask();
                    LogUtil.Write($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                    if (!streamStarted) StartStream();
                    Subscribe(true);
                    break;
                case 3:
                    CancelSource(captureTokenSource);
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


        private static Color ColorFromString(string inputString) {
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
                    } else {
                        b.Dispose();
                    }
                }

                var leaves = DreamData.GetItem<List<NanoData>>("leaves");

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
                CancelSource(captureTokenSource);
                CancelSource(sendTokenSource);
                strip?.StopLights();
                foreach (var b in bridges) {
                    b.DisableStreaming();
                }

                LogUtil.WriteDec("Stream stopped.");
                streamStarted = false;
            }
        }

        public void SendColors(Color[] colors, Color[] sectors, double fadeTime = 0) {
            if (sendTokenSource == null) sendTokenSource = new CancellationTokenSource();
            if (sendTokenSource.IsCancellationRequested) return;

            foreach (var bridge in bridges) {
                bridge.UpdateLights(sectors, brightness, sendTokenSource.Token, fadeTime);
            }

            foreach (var p in panels) {
                p.UpdateLights(sectors);
            }

            strip?.UpdateAll(colors);
        }


        private void StartShowBuilder() {
            if (showBuilderStarted) return;
            dreamScene.LoadScene(ambientShow);
            showBuilderSource = new CancellationTokenSource();
            Task.Run(() => dreamScene.BuildColors(this, showBuilderSource.Token));
            prevAmbientShow = ambientShow;
            showBuilderStarted = true;
            LogUtil.WriteInc($"Started new ambient show {ambientShow}.");
        }

        private void StopShowBuilder() {
            if (showBuilderStarted) LogUtil.WriteDec(@"Stopping show builder.");
            CancelSource(showBuilderSource);
            showBuilderStarted = false;

        }

        private void UpdateAmbientColor(Color aColor) {
            // Re initialize, just in case
            StopShowBuilder();
            var colors = new Color[12];
            for (var i = 0; i < colors.Length; i++) colors[i] = Color.Black;
            LogUtil.Write($@"DreamScreen: Setting ambient color to: {aColor.ToString()}.");
            for (var i = 0; i < colors.Length; i++) {
                colors[i] = aColor;
            }

            SendColors(colors, colors);
        }

        private void Subscribe(bool log = false) {
            if (log) LogUtil.Write(@"Sending subscribe message.");
            SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, targetEndpoint);
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

                    LogUtil.WriteDec("Listener cancelled.");
                    try {
                        StopServices();
                    }
                    finally {
                        LogUtil.WriteDec(@"DreamClient Stopped.");
                    }
                });
            }
            catch (SocketException e) {
                LogUtil.Write($@"Socket exception: {e.Message}.");
            }
            return null;
        }

        private Task StartVideoCapture(CancellationToken cancellationToken) {
            LogUtil.Write("Start video capture has been initialized.");
            if (grabber == null) return Task.CompletedTask;
            var captureTask = grabber.StartCapture(this, cancellationToken);
            return captureTask.IsCompleted ? captureTask : Task.CompletedTask;
        }

        private void StartVideoCaptureTask() {
            if (CaptureMode == 0) {
                Subscribe();
                if (!streamStarted) StartStream();
            } else {
                CancelSource(captureTokenSource);
                captureTokenSource = new CancellationTokenSource();
                if (!streamStarted) StartStream();
                Task.Run(() => StartVideoCapture(captureTokenSource.Token));
            }
        }

        private Task StartAudioCapture(CancellationToken cancellation) {
            if (cancellation != CancellationToken.None) {
                LogUtil.Write("Foo-fiddy foo foo foo.");
            }
            return Task.CompletedTask;
        }

        private void StartAudioCaptureTask() {
            if (CaptureMode == 0) return;
            CancelSource(captureTokenSource);
            captureTokenSource = new CancellationTokenSource();
            Task.Run(() => StartAudioCapture(captureTokenSource.Token));
        }



        private void ProcessData(byte[] receivedBytes, IPEndPoint receivedIpEndPoint) {
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) {
                if (_searching) LogUtil.Write("Discarding message from " + receivedIpEndPoint.Address);
                return;
            }
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
                LogUtil.Write($@"{from} -> localhost::{command}.");
                flag = msg.Flags;
                var groupMatch = msg.Group == dev.GroupNumber || msg.Group == 255;
                if ((flag == "11" || flag == "21") && groupMatch) {
                    dev = DreamData.GetDeviceData();
                    writeState = true;
                }
            } else {
                LogUtil.Write($@"Invalid message from {from}");
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (devMode == 1 || devMode == 2)
                        SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, replyPoint);
                    break;
                case "COLOR_DATA":
                    if (CaptureMode == 0 && (devMode == 1 || devMode == 2)) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
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
                                LogUtil.Write(@"No DS IP Set, setting.");
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
                        LogUtil.Write($@"Setting brightness to {brightness}.");
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
                        LogUtil.Write($@"Updating mode: {dev.Mode}.");
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
                        LogUtil.Write($@"Scene updated: {ambientShow}.");
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
                        int[] fSetup = payload.Select(x => (int) x).ToArray();
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
            SendUdpWrite(0x01, 0x0A, payload, 0x60, group, src);
        }


        public async Task<List<BaseDevice>> FindDevices() {
            _searching = true;
            Devices = new List<BaseDevice>();
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            var ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
            SendUdpMessage(msg, ip, "DISCOVER");
            var s = new Stopwatch();
            s.Start();
            await Task.Delay(3000).ConfigureAwait(true);
            Devices = Devices.Distinct().ToList();
            s.Stop();
            _searching = false;
            LogUtil.Write("DEVICES: " + JsonConvert.SerializeObject(Devices));
            return Devices;
        }

        private void CancelSource(CancellationTokenSource target, bool dispose = false) {
            if (target == null) return;
            if (!target.IsCancellationRequested) {
                target.Cancel();
            }

            if (dispose) target.Dispose();
        }
        
        public void SendUdpWrite(byte command1, byte command2, byte[] payload, byte flag = 17, byte group = 0,
            IPEndPoint ep = null, bool groupSend = false) {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            // If we don't specify an endpoint...talk to self
            if (ep == null) ep = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8888);
            using var stream = new MemoryStream();
            using var response = new BinaryWriter(stream);
            // Magic header
            response.Write((byte) 0xFC);
            // Payload length
            response.Write((byte) (payload.Length + 5));
            // Group number
            response.Write(group);
            // Flag, should be 0x10 for subscription, 17 for everything else
            response.Write(flag);
            // Upper command
            response.Write(command1);
            // Lower command
            response.Write(command2);
            // Payload
            foreach (var b in payload) response.Write(b);

            var byteSend = stream.ToArray();
            // CRC
            response.Write(MsgUtils.CalculateCrc(byteSend));

            if (flag == 0x30 | groupSend) {
                ep = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
            }
            SendUdpMessage(stream.ToArray(), ep, $"{command1}{command2}");
        }

        private void SendUdpMessage(byte[] data, IPEndPoint ep, string cmd=null) {
            listener.Send(data, data.Length, ep);
            if (cmd != null) {
                LogUtil.Write($"localhost -> {ep.Address}::{cmd}");
            } else {
                LogUtil.Write($"localhost -> {ep.Address}");
            }
            
        }


        
        private void StopServices() {
            listener?.Dispose();
            CancelSource(sendTokenSource, true);
            CancelSource(showBuilderSource, true);
            CancelSource(captureTokenSource, true);
            CancelSource(camTokenSource, true);
            LogUtil.Write("Tokens canceled.");
            Thread.Sleep(500);
            strip?.StopLights();
            strip?.Dispose();
            LogUtil.Write("Strip disposed.");
            foreach (var b in bridges) {
                b.DisableStreaming();
                b.Dispose();
            }

            bridges = new List<HueBridge>();
            LogUtil.Write("Bridges disposed.");
            foreach (var p in panels) {
                p.DisableStreaming();
                p.Dispose();
            }

            panels = new List<Panel>();
            LogUtil.Write("Panels disposed.");
        }
    }
}