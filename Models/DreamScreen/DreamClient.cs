using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Hubs;
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
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Utilities;
using Color = System.Drawing.Color;

namespace HueDream.Models.DreamScreen {
    public class DreamClient : BackgroundService {
        private int CaptureMode { get; set; }
        private DreamScene _dreamScene;
        private byte _group;
        private Color _ambientColor;
        private int _ambientMode;
        private static IHubContext<SocketServer> _hubContext;
        private int _ambientShow;
        private List<HueBridge> _bridges;
        private List<NanoGroup> _panels;
        private List<BaseDevice> _devices;
        private List<IStreamingDevice> _sDevices;

        // Store our clients in an AIO config so we're not re-declaring it
        private LifxClient _lifxClient;
        private HttpClient _nanoClient;
        private Socket _nanoSocket;
        
        private bool _discovering;
        private int _brightness;
        private StreamCapture _grabber;
        private AudioStream _aStream;
        private BaseDevice _dev;
        private bool _timerStarted;

        // Our functional values
        private int _devMode;

        private IPEndPoint _listenEndPoint;
        private UdpClient _listener;

        // I don't know if we actually need all these
        private int _prevAmbientMode;
        private int _prevAmbientShow;

        // Used by our loops to know when to update?
        private int _prevMode;

        // Token passed to our hue bridges
        private CancellationTokenSource _sendTokenSource;

        // Token used by our show builder for ambient scenes
        private CancellationTokenSource _showBuilderSource;

        // Use this for capturing screen/video
        private CancellationTokenSource _captureTokenSource;

        // Use this for the camera
        private CancellationTokenSource _camTokenSource;

        // Use this to check if we've initialized our bridges
        private bool _streamStarted;


        // Use this to check if we've started our show builder
        private bool _showBuilderStarted;
        private IPEndPoint _targetEndpoint;
        private LedStrip _strip;

        private Timer _refreshTimer;

        public DreamClient(IHubContext<SocketServer> hubContext) {
            _hubContext = hubContext;
            LogUtil.Write("Initialized with hubcontext??");
            Initialize();
        }
        public DreamClient() {
            LogUtil.Write("Initialized without hubcontext.");
            Initialize();
        }

        private void Initialize() {
            // Init lifx client
            _lifxClient = LifxClient.CreateAsync().Result;
            
            // Init nano HttpClient
            _nanoClient = new HttpClient();
            
            // Init nano socket
            _nanoSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _nanoSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _nanoSocket.EnableBroadcast = false;

            var dd = DataUtil.GetStore();
            DataUtil.CheckDefaults(dd, _lifxClient);
            _aStream = GetStream();
            _dev = DataUtil.GetDeviceData();
            _dreamScene = new DreamScene();
            _devices = new List<BaseDevice>();
            _devMode = _dev.Mode;
            _ambientMode = _dev.AmbientModeType;
            _ambientShow = _dev.AmbientShowType;
            _ambientColor = ColorFromString(_dev.AmbientColor);
            _brightness = _dev.Brightness;
            CaptureMode = dd.GetItem<int>("captureMode");
            // Set these to "unset" states
            _prevMode = -1;
            _prevAmbientMode = -1;
            _prevAmbientShow = -1;
            _streamStarted = false;
            _showBuilderStarted = false;
            string sourceIp = dd.GetItem("dsIp");
            _group = (byte) _dev.GroupNumber;
            _targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            _bridges = new List<HueBridge>();
            _panels = new List<NanoGroup>();
            _sDevices = new List<IStreamingDevice>();
            _captureTokenSource = new CancellationTokenSource();
            _camTokenSource = new CancellationTokenSource();
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            LogUtil.WriteInc("Starting DreamClient services.");
            _refreshTimer = new Timer(
                e => DeviceDiscovery(),  
                null, 
                TimeSpan.Zero, 
                TimeSpan.FromMinutes(10));

            if (CaptureMode != 0) {
                _grabber = new StreamCapture(_camTokenSource.Token);
                var ledData = DataUtil.GetItem<LedData>("ledData") ?? new LedData(true);
                try {
                    _strip = new LedStrip(ledData);
                } catch (TypeInitializationException e) {
                    LogUtil.Write("Type init error: " + e.Message);
                }
            }

            var lt = StartListening(cancellationToken);
            LogUtil.Write("Listener retrieved.");
            UpdateMode(_dev.Mode);
            LogUtil.Write("Updating device mode on startup.");
            return lt;
        }
        
        private async Task DeviceDiscovery() {
            LogUtil.Write("Starting device discovery.");
            // Trigger a refresh
            DataUtil.RefreshDevices(_lifxClient);
            // Sleep for 5s for it to finish
            Thread.Sleep(5000);
            // Notify all clients to refresh data
            await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
        }

        private AudioStream GetStream() {
            try {
                return new AudioStream();
            } catch (DllNotFoundException e) {
                LogUtil.Write("Unable to load bass Dll: " + e.Message);
            }

            return null;
        }

        private void UpdateMode(int newMode) {
            if (_prevMode == newMode) return;
            _dev = DataUtil.GetDeviceData();
            _dev.Mode = newMode;
            _devMode = newMode;
            _hubContext.Clients.All.SendAsync("mode", newMode);
            LogUtil.Write($@"DreamScreen: Updating mode from {_prevMode} to {newMode}.");
            // If we are not in ambient mode and ambient scene is running, stop it
            switch (newMode) {
                case 0:
                    StopShowBuilder();
                    StopStream();
                    CancelSource(_captureTokenSource);
                    break;
                case 1:
                    StopShowBuilder();
                    StartVideoCaptureTask();
                    break;
                case 2:
                    StopShowBuilder();
                    StartAudioCaptureTask();
                    LogUtil.Write($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                    if (!_streamStarted) StartStream();
                    Subscribe(true);
                    break;
                case 3:
                    CancelSource(_captureTokenSource);
                    if (!_streamStarted) StartStream();
                    UpdateAmbientMode(_ambientMode);
                    break;
            }

            _prevMode = newMode;
        }

        private void UpdateAmbientMode(int newMode) {
            if (_prevAmbientMode == newMode) return;
            if (_ambientMode == 0 && _prevAmbientMode != -1) {
                // Cancel ambient task
                StopShowBuilder();
            } else {
                _hubContext.Clients.All.SendAsync("ambientMode", newMode);
            }

            switch (_ambientMode) {
                case 0:
                    UpdateAmbientColor(_ambientColor);
                    break;
                case 1:
                    StartShowBuilder();
                    break;
            }

            _prevAmbientMode = newMode;
        }

        private void UpdateAmbientShow(int newShow) {
            if (_prevAmbientShow == newShow) return;
            StopShowBuilder();
            StartShowBuilder();
            _dreamScene.LoadScene(newShow);
            _prevAmbientShow = newShow;
        }

        private void UpdateBrightness(int newBrightness) {
            if (_brightness == newBrightness) return;
            _brightness = newBrightness;
            _hubContext.Clients.All.SendAsync("setBrightness", _brightness);
            if (_ambientMode == 0 && _devMode == 3) {
                UpdateAmbientColor(_ambientColor);
            }
        }


        private static Color ColorFromString(string inputString) {
            return ColorTranslator.FromHtml("#" + inputString);
        }

        private void StartStream() {
            if (!_streamStarted) {
                LogUtil.Write("Starting stream.");
                // Init bridges
                var bridgeArray = DataUtil.GetCollection<BridgeData>("bridges");
                _bridges = new List<HueBridge>();
                _sendTokenSource = new CancellationTokenSource();
                foreach (var bridge in bridgeArray.Where(bridge => !string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) && bridge.SelectedGroup != "-1")) {
                    _sDevices.Add(new HueBridge(bridge));
                }

                // Init leaves
                var leaves = DataUtil.GetCollection<NanoData>("leaves");
                _panels = new List<NanoGroup>();
                foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
                    _sDevices.Add(new NanoGroup(n, _nanoClient, _nanoSocket));
                }

                // Init lifx
                var lifx = DataUtil.GetCollection<LifxData>("lifxBulbs");
                if (lifx != null) {
                    foreach (var b in lifx.Where(b => b.SectorMapping != -1)) {
                        _sDevices.Add(new LifxBulb(b, _lifxClient));
                    }
                }

                var added = false;
                foreach (var sd in _sDevices.Where(sd => !sd.Streaming)) {
                    sd.StartStream(_sendTokenSource.Token);
                    added = true;
                }
                
                _streamStarted = added;
            }

            if (_streamStarted) LogUtil.WriteInc("Stream started.");
        }

        private void StopStream() {
            if (_streamStarted) {
                CancelSource(_captureTokenSource);
                CancelSource(_sendTokenSource);
                _strip?.StopLights();
                
                foreach (var s in _sDevices.Where(s => s.Streaming)) {
                    s.StopStream();
                }
                
                LogUtil.WriteDec("Stream stopped.");
                _streamStarted = false;
            }
        }

        public void SendColors(List<Color> colors, List<Color> sectors, double fadeTime = 0) {
            _sendTokenSource ??= new CancellationTokenSource();
            if (_sendTokenSource.IsCancellationRequested) return;
            if (!_streamStarted) return;
            var output = sectors;
            foreach (var sd in _sDevices) {
                sd.SetColor(output, fadeTime);
            }
            
            _strip?.UpdateAll(colors);
        }


        private void StartShowBuilder() {
            if (_showBuilderStarted) return;
            _dreamScene.LoadScene(_ambientShow);
            _showBuilderSource = new CancellationTokenSource();
            Task.Run(() => _dreamScene.BuildColors(this, _showBuilderSource.Token));
            _prevAmbientShow = _ambientShow;
            _showBuilderStarted = true;
            LogUtil.WriteInc($"Started new ambient show {_ambientShow}.");
        }

        private void StopShowBuilder() {
            if (_showBuilderStarted) LogUtil.WriteDec(@"Stopping show builder.");
            CancelSource(_showBuilderSource);
            _showBuilderStarted = false;
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
            DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, _targetEndpoint);
        }


        private Task StartListening(CancellationToken ct) {
            try {
                _listenEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.EnableBroadcast = true;
                _listener.Client.Bind(_listenEndPoint);

                // Return listen task to kill later
                return Task.Run(() => {
                    LogUtil.Write("Listener started.");
                    while (!ct.IsCancellationRequested) {
                        var sourceEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        var receivedResults = _listener.Receive(ref sourceEndPoint);
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
            if (_grabber == null) return Task.CompletedTask;
            var captureTask = _grabber.StartCapture(this, cancellationToken);
            return captureTask.IsCompleted ? captureTask : Task.CompletedTask;
        }

        private void StartVideoCaptureTask() {
            if (CaptureMode == 0) {
                Subscribe();
                if (!_streamStarted) StartStream();
            } else {
                CancelSource(_captureTokenSource);
                _captureTokenSource = new CancellationTokenSource();
                if (!_streamStarted) StartStream();
                Task.Run(() => StartVideoCapture(_captureTokenSource.Token));
            }
        }

        public Task StartAudioCapture(CancellationToken cancellation) {
            if (_aStream == null) {
                LogUtil.Write("No Audio devices, no stream.");
                return Task.CompletedTask;
            }
            if (cancellation != CancellationToken.None) {
                LogUtil.Write("Starting audio capture service.");
                _aStream.StartStream(cancellation);
                return Task.Run(() => {
                    while (!cancellation.IsCancellationRequested) {
                        var cols = _aStream.GetColors();
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
            CancelSource(_captureTokenSource);
            LogUtil.Write("Starting ac");
            _captureTokenSource = new CancellationTokenSource();
            Task.Run(() => StartAudioCapture(_captureTokenSource.Token));
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
                    LogUtil.Write($@"{from} -> {_dev.IpAddress}::{command} {flag}-{msg.Group}.");
                }

                var groupMatch = msg.Group == _dev.GroupNumber || msg.Group == 255;
                if ((flag == "11" || flag == "21") && groupMatch) {
                    _dev = DataUtil.GetDeviceData();
                    writeState = true;
                    writeDev = true;
                }
                if (flag == "41") {
                    LogUtil.Write("We should save a device's updated setting?");
                    _dev = DataUtil.GetDreamDevice(from);
                    writeDev = true;
                }
            } else {
                LogUtil.Write($@"Invalid message from {from}");
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (_devMode == 1 || _devMode == 2)
                        DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, replyPoint);
                    break;
                case "DISCOVERY_START":
                    LogUtil.Write("DreamScreen: Starting discovery.");
                    _devices = new List<BaseDevice>();
                    _discovering = true;
                    break;
                case "DISCOVERY_STOP":
                    LogUtil.Write($"DreamScreen: Discovery complete, found {_devices.Count} devices.");
                    _discovering = false;
                    DataUtil.SetItem<List<BaseDevice>>("devices", _devices);
                    break;
                case "REMOTE_REFRESH":
                    var id = Encoding.UTF8.GetString(payload.ToArray());
                    var tDev = _sDevices.First(b => b.Id == id);
                    LogUtil.Write($"Triggering reload of device {id}.");
                    tDev?.ReloadData();
                    break;
                case "COLOR_DATA":
                    if (CaptureMode == 0 && (_devMode == 1 || _devMode == 2)) {
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
                                _targetEndpoint = replyPoint;
                            }

                            if (_discovering) {
                                _devices.Add(msgDevice);
                            }
                        }
                    }

                    break;
                case "GROUP_NAME":
                    var gName = Encoding.ASCII.GetString(payload);
                    if (writeState | writeDev) _dev.GroupName = gName;

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    if (writeState | writeDev) _dev.GroupNumber = gNum;

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (writeState | writeDev) _dev.Name = dName;

                    break;
                case "BRIGHTNESS":
                    _brightness = payload[0];
                    if (writeState | writeDev) {
                        LogUtil.Write($@"Setting brightness to {_brightness}.");
                        _dev.Brightness = payload[0];
                    }
                    if (writeState) UpdateBrightness(payload[0]);

                    break;
                case "SATURATION":
                    if (writeState | writeDev) {
                        _dev.Saturation = ByteUtils.ByteString(payload);
                    }

                    break;
                case "MODE":
                    if (writeState | writeDev) {
                        _dev.Mode = payload[0];
                        LogUtil.Write($@"Updating mode: {_dev.Mode}.");
                    }
                    if (writeState) UpdateMode(_dev.Mode);

                    break;
                case "TRIGGER_DISCOVERY":
                    LogUtil.Write("Triggering discovery.");
                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState | writeDev) {
                        _dev.AmbientModeType = payload[0];
                    }
                    if (writeState) UpdateAmbientMode(_dev.Mode);

                    break;
                case "AMBIENT_SCENE":
                    if (writeState | writeDev) {
                        _ambientShow = payload[0];
                        _dev.AmbientShowType = _ambientShow;
                        LogUtil.Write($@"Scene updated: {_ambientShow}.");
                    }
                    if (writeState) UpdateAmbientShow(_ambientShow);
                    break;
                case "AMBIENT_COLOR":
                    if (writeDev | writeState) {
                        _dev.AmbientColor = ByteUtils.ByteString(payload);
                    }
                    if (writeState) UpdateAmbientColor(ColorFromString(_dev.AmbientColor));

                    break;
                case "SKU_SETUP":
                    if (writeState | writeDev) {
                        LogUtil.Write("Setting SKU type?");
                        _dev.SkuSetup = payload[0];
                    }

                    break;
                case "FLEX_SETUP":
                    if (writeState | writeDev) {
                        LogUtil.Write("Setting FlexSetup");
                        int[] fSetup = payload.Select(x => (int) x).ToArray();
                        _dev.flexSetup = fSetup;
                    }

                    break;
                case "RESET_PIC":
                    LogUtil.Write("Reboot command issued!");

                    break;
            }

            if (writeState) {
                DataUtil.SetItem<BaseDevice>("myDevice", _dev);
                DreamSender.SendUdpWrite(msg.C1, msg.C2, msg.GetPayload(), 0x41, (byte)msg.Group, receivedIpEndPoint,false);
            }
            if (writeDev) DataUtil.InsertDsDevice(_dev);
            if (writeState || writeDev) {
                NotifyClients();
            }
        }

        private async void NotifyClients() {
            if (_timerStarted) return;
            _timerStarted = true;
            await Task.Delay(TimeSpan.FromSeconds(.5));
            await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
            _timerStarted = false;
            LogUtil.Write("Sending updated data via socket.");
        }
        
        private void SendDeviceStatus(IPEndPoint src) {
            var dss = DataUtil.GetDeviceData();
            var payload = dss.EncodeState();
            DreamSender.SendUdpWrite(0x01, 0x0A, payload, 0x60, _group, src);
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
            _listener?.Dispose();
            CancelSource(_sendTokenSource, true);
            CancelSource(_showBuilderSource, true);
            CancelSource(_captureTokenSource, true);
            CancelSource(_camTokenSource, true);
            LogUtil.Write("Tokens canceled.");
            Thread.Sleep(500);
            _strip?.StopLights();
            _strip?.Dispose();
            LogUtil.Write("Strip disposed.");
            foreach (var b in _bridges) {
                b.StopStream();
                b.Dispose();
            }

            _bridges = new List<HueBridge>();
            LogUtil.Write("Bridges disposed.");
            foreach (var p in _panels) {
                p.StopStream();
                p.Dispose();
            }

            _panels = new List<NanoGroup>();
            LogUtil.Write("Panels disposed.");
            _lifxClient.Dispose();
        }
    }
}