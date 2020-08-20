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
using System.Timers;
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

        // Use this to check if devices are subscribed, and if so, send out color/sector data
        private Dictionary<string, int> _subscribers;
        
        // Use this in the future to possibly send richer color/sector data to devices
        //private int _sectorVersion = 1;

        // Use this to check if we've started our show builder
        private bool _showBuilderStarted;
        
        // Value used to save where we're replying to
        private IPEndPoint _targetEndpoint;
        
        // Our LED strip, if we have one
        private LedStrip _strip;

        // Timer for refreshing devices
        private System.Timers.Timer _refreshTimer;

        // We pass hub context to this so we can send data directly to the websocket
        public DreamClient(IHubContext<SocketServer> hubContext) {
            _hubContext = hubContext;
            LogUtil.Write("Initializing with hubcontext...");
            Initialize();
            LogUtil.Write("Initialisation complete.");
        }
        
        // Normal initializer, not actually used
        public DreamClient() {
            LogUtil.Write("Initializing without hubcontext.");
            Initialize();
            LogUtil.Write("Initialisation complete.");
        }

        // This initializes all of the data in our class and starts function loops
        private void Initialize() {
            // Create cancellation token sources
            _sendTokenSource = new CancellationTokenSource();
            _captureTokenSource = new CancellationTokenSource();
            _camTokenSource = new CancellationTokenSource();

            // Init lifx client
            _lifxClient = LifxClient.CreateAsync().Result;
            
            // Init nano HttpClient
            _nanoClient = new HttpClient();
            
            // Init nano socket
            _nanoSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _nanoSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _nanoSocket.EnableBroadcast = false;

            // Cache data store
            var dd = DataUtil.GetStore();
            // Check default settings
            DataUtil.CheckDefaults(dd, _lifxClient);
            // Get audio input if exists
            _aStream = GetStream();
            // Get our device data
            _dev = DataUtil.GetDeviceData();
            // Create scene builder
            _dreamScene = new DreamScene();
            // Get a list of devices
            _devices = new List<BaseDevice>();
            // Read other variables
            _devMode = _dev.Mode;
            _ambientMode = _dev.AmbientModeType;
            _ambientShow = _dev.AmbientShowType;
            _ambientColor = ColorFromString(_dev.AmbientColor);
            _brightness = _dev.Brightness;
            CaptureMode = dd.GetItem<int>("captureMode");
            
            // Set default values
            _prevMode = -1;
            _prevAmbientMode = -1;
            _prevAmbientShow = -1;
            _streamStarted = false;
            _showBuilderStarted = false;
            _group = (byte) _dev.GroupNumber;
            _targetEndpoint = new IPEndPoint(IPAddress.Parse(dd.GetItem("dsIp")), 8888);
            dd.Dispose();
            _sDevices = new List<IStreamingDevice>();
            LogUtil.Write("Starting DreamClient services.");
            // Start our timer to refresh devices
            StartRefreshTimer();
            // Start our service to capture (if capture mode is set)
            StartCaptureServices(_captureTokenSource.Token);
            // Now, start listening for UDP commands
            StartListening();
            // Finally start our normal device behavior
            UpdateMode(_dev.Mode);
        }

        // This is called because it's a service. I thought I needed this, maybe I don't...
        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            return Task.Run(async () => {
                LogUtil.Write("Just chilling, waiting to kill some stuff.");
                while (!cancellationToken.IsCancellationRequested) {
                    // Just sit here until the process ends.
                    // We probably should do the stop somewhere else.
                }
                LogUtil.Write("All done.");
                StopServices();
            });
        }

        // Starts our capture service and initializes our LEDs
        private void StartCaptureServices(CancellationToken ct) {
            if (CaptureMode == 0) return;
            var ledData = DataUtil.GetItem<LedData>("ledData") ?? new LedData(true);
            try {
                _strip = new LedStrip(ledData);
            } catch (TypeInitializationException e) {
                LogUtil.Write("Type init error: " + e.Message);
            }

            LogUtil.Write("Setting grabber...");
            _grabber = new StreamCapture(_camTokenSource.Token);
            LogUtil.Write("Grabber set.");

            if (_grabber == null) return;
            LogUtil.Write("We have a video source, starting subscription broadcast...");
            Task.Run(() => SubscribeBroadcast(ct), ct);
            LogUtil.Write("Subscribe broadcast started.");
        }

        // This broadcasts the subscribe message that other devices reply to to get color data
        private async void SubscribeBroadcast(CancellationToken ct) {
            _subscribers = new Dictionary<string, int>();
            // Loop until canceled
            try {
                while (!ct.IsCancellationRequested) {
                    // Send our subscribe multicast
                    DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x30, (byte) _dev.GroupNumber, null, true);
                    // Enumerate all subscribers, check to see that they are still valid
                    var keys = new List<string>(_subscribers.Keys);
                    foreach (var key in keys) {
                        // If the subscribers haven't replied in three messages, remove them, otherwise, count down one
                        if (_subscribers[key] <= 0) {
                            _subscribers.Remove(key);
                        } else {
                            _subscribers[key] -= 1;
                        }
                    }

                    // Sleep for 5s
                    await Task.Delay(5000, ct);
                }
            } catch (TaskCanceledException) {
                LogUtil.Write("Broadcast task was canceled.");
                _subscribers = new Dictionary<string, int>();
            }

            LogUtil.Write("Sub broadcast canceled.");
        }

        // Start a loop that refreshes our devices every 10 minutes
        private void StartRefreshTimer(bool refreshNow = false) {
            // Option to refresh immediately on execute
            if (refreshNow) {
                DeviceDiscovery(null, null);
            }
            
            // Reset and restart our timer
            _refreshTimer = new System.Timers.Timer(600000);
            _refreshTimer.Elapsed += DeviceDiscovery;
            _refreshTimer.AutoReset = true;
            _refreshTimer.Enabled = true;
        }
        
        // Discover...devices?
        private async void DeviceDiscovery(object sender, ElapsedEventArgs elapsedEventArgs) {
            LogUtil.Write("Starting device discovery.");
            // Trigger a refresh
            DataUtil.RefreshDevices(_lifxClient);
            // Sleep for 5s for it to finish
            Thread.Sleep(5000);
            // Notify all clients to refresh data
            await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
        }

        // Get our audio stream and catch errors
        private AudioStream GetStream() {
            try {
                return new AudioStream();
            } catch (DllNotFoundException e) {
                LogUtil.Write("Unable to load bass Dll: " + e.Message);
            }

            return null;
        }

        // Update our device mode
        private void UpdateMode(int newMode) {
            // If the mode doesn't change, we don't need to do anything
            if (_prevMode == newMode) return;
            // Reload our device data so we're sure it's fresh
            _dev = DataUtil.GetDeviceData();
            _dev.Mode = newMode;
            _devMode = newMode;
            // Notify web clients of mode change via socket
            _hubContext.Clients.All.SendAsync("mode", newMode);
            LogUtil.Write($@"DreamScreen: Updating mode from {_prevMode} to {newMode}.");
            // If we are not in ambient mode and ambient scene is running, stop it
            StopShowBuilder();

            switch (newMode) {
                case 0: // Off
                    StopStream();
                    CancelSource(_captureTokenSource);
                    break;
                case 1: // Video
                    StartVideoCaptureTask();
                    if (!_streamStarted) StartStream();
                    Subscribe(true);
                    break;
                case 2: // Audio
                    StartAudioCaptureTask();
                    LogUtil.Write($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                    if (!_streamStarted) StartStream();
                    Subscribe(true);
                    break;
                case 3: // Ambient
                    CancelSource(_captureTokenSource);
                    if (!_streamStarted) StartStream();
                    UpdateAmbientMode(_ambientMode);
                    break;
            }
            // Store last state
            _prevMode = newMode;
        }

        private void UpdateAmbientMode(int newMode) {
            // If nothing has changed, do nothing
            if (_prevAmbientMode == newMode) {
                LogUtil.Write("Nothing to do, ambient mode hasn't changed.");
                return;
            }
            _hubContext.Clients.All.SendAsync("ambientMode", newMode);
            switch (newMode) {
                case 0:
                    StopShowBuilder();
                    UpdateAmbientColor(_ambientColor);
                    break;
                case 1:
                    StartShowBuilder();
                    break;
            }

            _prevAmbientMode = newMode;
            _ambientMode = newMode;
        }
        
        
        private void StartShowBuilder() {
            if (_showBuilderStarted) {
                LogUtil.Write("Show builder is already started?", "WARN");
                StopShowBuilder();
            }
            _dreamScene.LoadScene(_ambientShow);
            _showBuilderSource = new CancellationTokenSource();
            Task.Run(() => _dreamScene.BuildColors(this, _showBuilderSource.Token));
            _prevAmbientShow = _ambientShow;
            LogUtil.WriteInc($"Started new ambient show {_ambientShow}.");
            _showBuilderStarted = true;
        }

        private void StopShowBuilder() {
            if (_showBuilderStarted) {
                LogUtil.WriteDec(@"Stopping show builder.");
                CancelSource(_showBuilderSource);
                _showBuilderStarted = false;
            }
        }

        private void UpdateAmbientColor(Color aColor) {
            LogUtil.Write($@"DreamScreen: Setting ambient color to: {aColor.ToString()}.");
            // Create a list
            var colors = new List<Color>();
            for (var q = 0; q < 12; q++) colors.Add(aColor);
            SendColors(colors, colors);
        }

        private void UpdateAmbientShow(int newShow) {
            if (_prevAmbientShow == newShow) return;
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
                LogUtil.Write("Starting stream...");
                // Init bridges
                var bridgeArray = DataUtil.GetCollection<BridgeData>("bridges");
                _sendTokenSource = new CancellationTokenSource();
                foreach (var bridge in bridgeArray.Where(bridge => !string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) && bridge.SelectedGroup != "-1")) {
                    _sDevices.Add(new HueBridge(bridge));
                }

                // Init leaves
                var leaves = DataUtil.GetCollection<NanoData>("leaves");
                foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
                    _sDevices.Add(new NanoGroup(n, _nanoClient, _nanoSocket));
                }

                // Init lifx
                var lifx = DataUtil.GetCollection<LifxData>("lifxBulbs");
                if (lifx != null) {
                    foreach (var b in lifx.Where(b => b.TargetSector != -1)) {
                        _sDevices.Add(new LifxBulb(b, _lifxClient));
                    }
                }

                var added = false;
                foreach (var sd in _sDevices.Where(sd => !sd.Streaming)) {
                    added = false;
                    LogUtil.Write("Starting device: " + sd.IpAddress);
                    sd.StartStream(_sendTokenSource.Token);
                    added = true;
                    LogUtil.Write($"Started {sd.IpAddress}.");
                }
                
                _streamStarted = added;
            }

            if (_streamStarted) LogUtil.WriteInc("Streaming on all devices should be started now.");
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

            if (CaptureMode != 0) {
                // If we have subscribers and we're capturing
                if (_subscribers.Count > 0) {
                    LogUtil.Write("We have " + _subscribers.Count + " subscribers: " + CaptureMode);
                }

                if (_subscribers.Count > 0 && CaptureMode != 0) {
                    var keys = new List<string>(_subscribers.Keys);
                    foreach (var ip in keys) {
                        DreamSender.SendSectors(sectors, ip, _dev.GroupNumber);
                        LogUtil.Write("Sent.");
                    }

                    LogUtil.Write("Sent to each subscriber.");
                }

                _strip?.UpdateAll(colors);
            }
        }

        // If we pass in a third set of sectors, use that info instead.
        public void SendColors(List<Color> colors, List<Color> sectors, List<Color> sectorsV2, double fadeTime = 0) {
            _sendTokenSource ??= new CancellationTokenSource();
            if (_sendTokenSource.IsCancellationRequested) return;
            if (!_streamStarted) return;
            foreach (var sd in _sDevices) {
                sd.SetColor(sectorsV2, fadeTime);
            }
            if (CaptureMode != 0) {
                // If we have subscribers and we're capturing
                if (_subscribers.Count > 0 && CaptureMode != 0) {
                    var keys = new List<string>(_subscribers.Keys);
                    foreach (var ip in keys) {
                        DreamSender.SendSectors(sectors, ip, _dev.GroupNumber);
                    }
                }

                _strip?.UpdateAll(colors);
            }
        }


        private void Subscribe(bool log = false) {
            if (log) LogUtil.Write(@"Sending subscribe message.");
            DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, _targetEndpoint);
        }


        private void StartListening() {
            try {
                _listenEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.EnableBroadcast = true;
                _listener.Client.Bind(_listenEndPoint);

                // Return listen task to kill later
                _listener.BeginReceive(Recv, null);
            } catch (SocketException e) {
                LogUtil.Write($@"Socket exception: {e.Message}.");
            }
        }
        
        private void Recv(IAsyncResult res) {
            //Process codes
            var sourceEndPoint = new IPEndPoint(IPAddress.Any, 8888);
            var receivedResults = _listener.EndReceive(res, ref sourceEndPoint);
                ProcessData(receivedResults, sourceEndPoint);
            
            _listener.BeginReceive(Recv, null);
        }

        private Task StartVideoCapture(CancellationToken cancellationToken) {
            LogUtil.Write("Start video capture has been initialized.");
            if (_grabber == null) return Task.CompletedTask;
            var captureTask = _grabber.StartCapture(this, cancellationToken);
            LogUtil.Write("Capture task is set.");
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
                LogUtil.Write("Capture task should be running and not blocking...");
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
            var tDevice = _dev;
            if (msg.IsValid) {
                payload = msg.GetPayload();
                payloadString = msg.PayloadString;
                command = msg.Command;
                msgDevice = msg.Device;
                flag = msg.Flags;
                var groupMatch = msg.Group == _dev.GroupNumber || msg.Group == 255;
                if ((flag == "11" || flag == "21") && groupMatch) {
                    writeState = true;
                    writeDev = true;
                }
                if (flag == "41") {
                    LogUtil.Write($"Flag is 41, we should save settings for {from}.");
                    tDevice = DataUtil.GetDreamDevice(from);
                    if (tDevice != null) writeDev = true;
                }
                if (from != null && command != null && command != "COLOR_DATA" && command != "SUBSCRIBE" && tDevice != null) {
                    LogUtil.Write($@"{from} -> {tDevice.IpAddress}::{command} {flag}-{msg.Group}.");
                }
            } else {
                LogUtil.Write($@"Invalid message from {from}");
            }
            switch (command) {
                case "SUBSCRIBE":
                    
                    if (_devMode == 1 || _devMode == 2) {
                        DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, replyPoint);    
                    }
                    
                    // If the device is on and capture mode is not using DS data
                    if (_devMode != 0 && CaptureMode != 0) {
                        // If the device is replying to our sub broadcast
                        if (flag == "60") {
                            // Set our count to 3, which is how many tries we get before we stop sending data
                            if (!string.IsNullOrEmpty(from)) {
                                if (!_subscribers.ContainsKey(from)) {
                                    LogUtil.Write("Adding new subscriber: " + from);
                                }
                                _subscribers[from] = 3;
                            } else {
                                LogUtil.Write("Can't add subscriber, from is empty...");
                            }
                            
                        }
                    } 
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
                    var tDev = _sDevices.FirstOrDefault(b => b.Id == id);
                    LogUtil.Write($"Triggering reload of device {id}.");
                    tDev?.ReloadData();
                    break;
                case "COLOR_DATA":
                    if (_devMode == 1 || _devMode == 2) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var colors = new List<Color>();
                        foreach (var colorValue in colorData) {
                            colors.Add(ColorFromString(colorValue));
                        }

                        colors = ShiftColors(colors);
                        SendColors(colors, colors);
                    } else {
                        LogUtil.Write("Not doing anything with color data because...");
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
                    if (writeState | writeDev) tDevice.GroupName = gName;

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    if (writeState | writeDev) tDevice.GroupNumber = gNum;

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (writeState | writeDev) tDevice.Name = dName;

                    break;
                case "BRIGHTNESS":
                    _brightness = payload[0];
                    if (writeState | writeDev) {
                        LogUtil.Write($@"Setting brightness to {_brightness}.");
                        tDevice.Brightness = payload[0];
                    }
                    if (writeState) UpdateBrightness(payload[0]);

                    break;
                case "SATURATION":
                    if (writeState | writeDev) {
                        tDevice.Saturation = ByteUtils.ByteString(payload);
                    }

                    break;
                case "MODE":
                    if (writeState | writeDev) {
                        tDevice.Mode = payload[0];
                        LogUtil.Write($@"Updating mode: {tDevice.Mode}.");
                    } else {
                        LogUtil.Write("Mode flag set, but we're not doing anything... " + flag);
                    }
                    
                    if (writeState) UpdateMode(tDevice.Mode);

                    break;
                case "REFRESH_CLIENTS":
                    LogUtil.Write("Triggering discovery.");
                    StartRefreshTimer(true);
                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState | writeDev) {
                        tDevice.AmbientModeType = payload[0];
                    }

                    if (writeState) {
                        LogUtil.Write($"Write state is set, we should be updating our ambient mode to {tDevice.AmbientModeType}.");
                        UpdateAmbientMode(tDevice.AmbientModeType);
                    }

                    break;
                case "AMBIENT_SCENE":
                    if (writeState | writeDev) {
                        _ambientShow = payload[0];
                        tDevice.AmbientShowType = _ambientShow;
                        LogUtil.Write($@"Scene updated: {_ambientShow}.");
                    }
                    if (writeState) UpdateAmbientShow(_ambientShow);
                    break;
                case "AMBIENT_COLOR":
                    if (writeDev | writeState) {
                        if (tDevice != null) tDevice.AmbientColor = ByteUtils.ByteString(payload);
                    }
                    if (writeState && tDevice != null) UpdateAmbientColor(ColorFromString(tDevice.AmbientColor));

                    break;
                case "SKU_SETUP":
                    if (writeState | writeDev) {
                        LogUtil.Write("Setting SKU type?");
                        tDevice.SkuSetup = payload[0];
                    }

                    break;
                case "FLEX_SETUP":
                    if (writeState | writeDev) {
                        LogUtil.Write("Setting FlexSetup");
                        int[] fSetup = payload.Select(x => (int) x).ToArray();
                        tDevice.flexSetup = fSetup;
                    }

                    break;
                case "RESET_PIC":
                    LogUtil.Write("Reboot command issued!");

                    break;
            }

            if (writeState) {
                DataUtil.SetItem<BaseDevice>("myDevice", tDevice);
                _dev = tDevice;
                DreamSender.SendUdpWrite(msg.C1, msg.C2, msg.GetPayload(), 0x41, (byte)msg.Group, receivedIpEndPoint);
            }

            if (!writeState && !writeDev) return;
            // Notify if the sender was not us
            if (from != _dev.IpAddress) NotifyClients();
            DataUtil.InsertDsDevice(tDevice);
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
                LogUtil.Write("Canceling source...");
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
            foreach (var s in _sDevices) {
                s.StopStream();
                s.Dispose();
            }
        }
    }
}