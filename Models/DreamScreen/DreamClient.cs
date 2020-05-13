using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.CaptureSource.Audio;
using HueDream.Models.CaptureSource.Camera;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.LED;
using HueDream.Models.StreamingDevice;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.LIFX;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.Util;
using LifxNet;
using Microsoft.Extensions.Hosting;
using Color = System.Drawing.Color;

namespace HueDream.Models.DreamScreen {
    public class DreamClient : BackgroundService {
        private int CaptureMode { get; set; }
        private readonly DreamScene dreamScene;
        private readonly byte group;
        private readonly Color ambientColor;
        private readonly int ambientMode;
        private int ambientShow;
        private List<HueBridge> bridges;
        private List<NanoGroup> panels;
        private List<BaseDevice> devices;
        private List<LifxBulb> bulbs;
        private List<IStreamingDevice> sDevices;
        private LifxClient lifxClient;
        private bool discovering;
        private int brightness;
        private StreamCapture grabber;
        private AudioStream aStream;
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

        public DreamClient() {
            aStream = getStream();
            var dd = DataUtil.GetStore();
            dev = DataUtil.GetDeviceData();
            dreamScene = new DreamScene();
            devices = new List<BaseDevice>();
            bulbs = new List<LifxBulb>();
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
            panels = new List<NanoGroup>();
            sDevices = new List<IStreamingDevice>();
            captureTokenSource = new CancellationTokenSource();
            camTokenSource = new CancellationTokenSource();
        }


        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            LogUtil.WriteInc("Starting DreamClient services.");

            if (CaptureMode != 0) {
                grabber = new StreamCapture(camTokenSource.Token);
                var ledData = DataUtil.GetItem<LedData>("ledData") ?? new LedData(true);
                try {
                    strip = new LedStrip(ledData);
                } catch (TypeInitializationException e) {
                    LogUtil.Write("Type init error: " + e.Message);
                }
            }


            var lt = StartListening(cancellationToken);
            LogUtil.Write("Listener retrieved.");
            UpdateMode(dev.Mode);
            LogUtil.Write("Updating device mode on startup.");
            return lt;
        }

        private AudioStream getStream() {
            try {
                return new AudioStream();
            } catch (DllNotFoundException e) {
                LogUtil.Write("Unable to load bass Dll: " + e.Message);
            }

            return null;
        }

        private void UpdateMode(int newMode) {
            if (prevMode == newMode) return;
            dev = DataUtil.GetDeviceData();
            dev.Mode = newMode;
            devMode = newMode;
            LogUtil.Write($@"DreamScreen: Updating mode from {prevMode} to {newMode}.");
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
                // Init bridges
                var bridgeArray = DataUtil.GetCollection<BridgeData>("bridges");
                bridges = new List<HueBridge>();
                sendTokenSource = new CancellationTokenSource();
                foreach (var bridge in bridgeArray.Where(bridge => !string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) && bridge.SelectedGroup != "-1")) {
                    sDevices.Add(new HueBridge(bridge));
                }

                // Init leaves
                var leaves = DataUtil.GetCollection<NanoData>("leaves");
                panels = new List<NanoGroup>();
                foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
                    sDevices.Add(new NanoGroup(n));
                }

                // Init lifx
                var lifx = DataUtil.GetCollection<LifxData>("lifxBulbs");
                bulbs = new List<LifxBulb>();
                if (lifx != null) {
                    lifxClient = LifxSender.GetClient();
                    foreach (var b in lifx.Where(b => b.SectorMapping != -1)) {
                        sDevices.Add(new LifxBulb(b));
                    }
                }

                var added = false;
                foreach (var sd in sDevices.Where(sd => !sd.Streaming)) {
                    sd.StartStream(sendTokenSource.Token);
                    added = true;
                }
                
                streamStarted = added;
            }

            if (streamStarted) LogUtil.WriteInc("Stream started.");
        }

        private void StopStream() {
            if (streamStarted) {
                CancelSource(captureTokenSource);
                CancelSource(sendTokenSource);
                strip?.StopLights();
                
                foreach (var s in sDevices.Where(s => s.Streaming)) {
                    s.StopStream();
                }
                
                LogUtil.WriteDec("Stream stopped.");
                streamStarted = false;
            }
        }

        public void SendColors(List<Color> colors, List<Color> sectors, double fadeTime = 0) {
            sendTokenSource ??= new CancellationTokenSource();
            if (sendTokenSource.IsCancellationRequested) return;
            if (!streamStarted) return;
            var output = sectors;
            foreach (var sd in sDevices) {
                sd.SetColor(output, fadeTime);
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
            var colors = new List<Color>();
            for (var i = 0; i < 12; i++) colors[i] = Color.Black;
            LogUtil.Write($@"DreamScreen: Setting ambient color to: {aColor.ToString()}.");
            for (var i = 0; i < 12; i++) {
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
                    LogUtil.Write("Listener started.");
                    while (!ct.IsCancellationRequested) {
                        var sourceEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        var receivedResults = listener.Receive(ref sourceEndPoint);
                        ProcessData(receivedResults, sourceEndPoint);
                    }

                    LogUtil.Write("Listener cancelled.");
                    try {
                        StopServices();
                    } finally {
                        LogUtil.WriteDec(@"DreamClient Stopped.");
                    }
                });
            } catch (SocketException e) {
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

        public Task StartAudioCapture(CancellationToken cancellation) {
            if (aStream == null) {
                LogUtil.Write("No Audio devices, no stream.");
                return Task.CompletedTask;
            }
            if (cancellation != CancellationToken.None) {
                LogUtil.Write("Starting audio capture service.");
                aStream.StartStream(cancellation);
                return Task.Run(() => {
                    while (!cancellation.IsCancellationRequested) {
                        var cols = aStream.GetColors();
                        SendColors(cols, cols);
                    }
                });
            }

            LogUtil.Write("Cancellation token is null??");
            return Task.CompletedTask;
        }

        private void StartAudioCaptureTask() {
            // if (CaptureMode == 0) {
            //     LogUtil.Write("cap mode is 0");
            //     return;
            // }
            CancelSource(captureTokenSource);
            LogUtil.Write("Starting ac");
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
            var writeDev = false;
            var msg = new DreamScreenMessage(receivedBytes, from);
            if (msg.IsValid) {
                payload = msg.GetPayload();
                payloadString = msg.PayloadString;
                command = msg.Command;
                msgDevice = msg.Device;
                
                flag = msg.Flags;
                if (from != null && command != null && command != "COLOR_DATA" && command != "SUBSCRIBE") {
                    LogUtil.Write($@"{from} -> {dev.IpAddress}::{command} {flag}-{msg.Group}.");
                }

                var groupMatch = msg.Group == dev.GroupNumber || msg.Group == 255;
                if ((flag == "11" || flag == "21") && groupMatch) {
                    dev = DataUtil.GetDeviceData();
                    writeState = true;
                    writeDev = true;
                }
                if (flag == "41") {
                    LogUtil.Write("We should save a device's updated setting?");
                    dev = DataUtil.GetDreamDevice(from);
                    writeDev = true;
                }
            } else {
                LogUtil.Write($@"Invalid message from {from}");
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (devMode == 1 || devMode == 2)
                        DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, group, replyPoint);
                    break;
                case "DISCOVERY_START":
                    LogUtil.Write("DreamScreen: Starting discovery.");
                    devices = new List<BaseDevice>();
                    discovering = true;
                    break;
                case "DISCOVERY_STOP":
                    LogUtil.Write($"DreamScreen: Discovery complete, found {devices.Count} devices.");
                    discovering = false;
                    DataUtil.SetItem<List<BaseDevice>>("devices", devices);
                    break;
                case "REMOTE_REFRESH":
                    var id = Encoding.UTF8.GetString(payload.ToArray());
                    var tDev = sDevices.First(b => b.Id == id);
                    LogUtil.Write($"Triggering reload of device {id}.");
                    tDev?.ReloadData();
                    break;
                case "COLOR_DATA":
                    if (CaptureMode == 0 && (devMode == 1 || devMode == 2)) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var colors = new List<Color>();

                        foreach (var colorValue in colorData) {
                            colors.Add(ColorFromString(colorValue));
                        }

                        colors = ShiftColors(colors);
                        SendColors(colors, colors);
                    }

                    break;
                case "DEVICE_DISCOVERY":
                    if (flag == "30" && from != "0.0.0.0") {
                        SendDeviceStatus(replyPoint);
                    } else if (flag == "60") {
                        if (msgDevice != null) {
                            string dsIpCheck = DataUtil.GetItem("dsIp");
                            if (dsIpCheck == "0.0.0.0" &&
                                msgDevice.Tag.Contains("DreamScreen", StringComparison.CurrentCulture)) {
                                LogUtil.Write(@"Setting a target DS IP.");
                                DataUtil.SetItem("dsIp", from);
                                targetEndpoint = replyPoint;
                            }

                            if (discovering) {
                                devices.Add(msgDevice);
                            }
                        }
                    }

                    break;
                case "GROUP_NAME":
                    var gName = Encoding.ASCII.GetString(payload);
                    if (writeState | writeDev) dev.GroupName = gName;

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    if (writeState | writeDev) dev.GroupNumber = gNum;

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (writeState | writeDev) dev.Name = dName;

                    break;
                case "BRIGHTNESS":
                    brightness = payload[0];
                    if (writeState | writeDev) {
                        LogUtil.Write($@"Setting brightness to {brightness}.");
                        dev.Brightness = payload[0];
                    }
                    if (writeState) UpdateBrightness(payload[0]);

                    break;
                case "SATURATION":
                    if (writeState | writeDev) dev.Saturation = ByteUtils.ByteString(payload);

                    break;
                case "MODE":
                    if (writeState | writeDev) {
                        dev.Mode = payload[0];
                        LogUtil.Write($@"Updating mode: {dev.Mode}.");
                    }
                    if (writeState) UpdateMode(dev.Mode);

                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState | writeDev) {
                        dev.AmbientModeType = payload[0];
                    }
                    if (writeState) UpdateAmbientMode(dev.Mode);

                    break;
                case "AMBIENT_SCENE":
                    if (writeState | writeDev) {
                        ambientShow = payload[0];
                        dev.AmbientShowType = ambientShow;
                        LogUtil.Write($@"Scene updated: {ambientShow}.");
                    }
                    if (writeState) UpdateAmbientShow(ambientShow);
                    break;
                case "AMBIENT_COLOR":
                    if (writeDev | writeState) {
                        dev.AmbientColor = ByteUtils.ByteString(payload);
                    }
                    if (writeState) UpdateAmbientColor(ColorFromString(dev.AmbientColor));

                    break;
                case "SKU_SETUP":
                    if (writeState | writeDev) {
                        LogUtil.Write("Setting SKU type?");
                        dev.SkuSetup = payload[0];
                    }

                    break;
                case "FLEX_SETUP":
                    if (writeState | writeDev) {
                        LogUtil.Write("Setting FlexSetup");
                        int[] fSetup = payload.Select(x => (int) x).ToArray();
                        dev.flexSetup = fSetup;
                    }

                    break;
                case "RESET_PIC":
                    LogUtil.Write("Reboot command issued!");

                    break;
            }

            if (writeState) {
                DataUtil.SetItem<BaseDevice>("myDevice", dev);
                //DreamSender.SendUdpWrite(msg.C1, msg.C2, 0x41, msg.Group, null, true);
            }
            if (writeDev) DataUtil.InsertDsDevice(dev);
        }

        private void SendDeviceStatus(IPEndPoint src) {
            var dss = DataUtil.GetDeviceData();
            var payload = dss.EncodeState();
            DreamSender.SendUdpWrite(0x01, 0x0A, payload, 0x60, group, src);
        }

        private static List<Color> ShiftColors(List<Color> input) {
            if (input.Count < 12) return input;
            var output = new Color[12];
            output[0] = input[0];
            output[1] = input[1];
            output[2] = input[2];
            output[3] = input[3].Blend(input[4], .5);
            output[4] = input[4].Blend(input[5], .5);
            output[5] = input[5];
            output[6] = input[6];
            output[7] = input[7];
            output[8] = input[9].Blend(input[10], .5);
            output[9] = input[10];
            output[10] = input[11];
            output[11] = input[11].Blend(input[0], .5);
            var nl = new List<Color>();
            nl.AddRange(output);
            return nl;
        }

        private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
            if (target == null) return;
            if (!target.IsCancellationRequested) {
                target.Cancel();
            }

            if (dispose) target.Dispose();
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
                b.StopStream();
                b.Dispose();
            }

            bridges = new List<HueBridge>();
            LogUtil.Write("Bridges disposed.");
            foreach (var p in panels) {
                p.StopStream();
                p.Dispose();
            }

            panels = new List<NanoGroup>();
            LogUtil.Write("Panels disposed.");
            LifxSender.DestroyClient();
        }
    }
}