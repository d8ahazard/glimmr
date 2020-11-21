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
using Glimmr.Hubs;
using Glimmr.Models.CaptureSource.Audio;
using Glimmr.Models.DreamScreen;
using Glimmr.Models.DreamScreen.Devices;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Color = System.Drawing.Color;

namespace Glimmr.Services {
    public class DreamService : BackgroundService {
        private int CaptureMode { get; set; }
        private DreamScene _dreamScene;
        private byte _group;
        private Color _ambientColor;
        private int _ambientMode;
        private static IHubContext<SocketServer> _hubContext;
        private ControlService _controlService;
        private int _ambientShow;
        private List<BaseDevice> _devices;
        
        
        private bool _discovering;
        private bool _autoDisabled;
        private int _brightness;
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

        
        // Token used by our show builder for ambient scenes
        private CancellationTokenSource _showBuilderSource;

        // Use this to check if devices are subscribed, and if so, send out color/sector data
        private Dictionary<string, int> _subscribers;
        
        // Use this to check if we've started our show builder
        private bool _showBuilderStarted;
        
        // Value used to save where we're replying to
        private IPEndPoint _targetEndpoint;
        

        // We pass hub context to this so we can send data directly to the websocket
        public DreamService(IHubContext<SocketServer> hubContext, ControlService controlService) {
            _hubContext = hubContext;
            _controlService = controlService;
            _controlService.SetCaptureModeEvent += CheckSubscribe;
            _controlService.DreamSubscribeEvent += Subscribe;
            Initialize();
            LogUtil.Write("Initialisation complete.");
        }
        
        
        // This initializes all of the data in our class and starts function loops
        private void Initialize() {
            LogUtil.Write("Initializing dream client...");
            // Create cancellation token sources
             var dd = DataUtil.GetDb();
            // Get audio input if exists
            _dev = DataUtil.GetDeviceData();
            LogUtil.Write("Device Data: " + JsonConvert.SerializeObject(_dev));
            // Create scene builder
            _dreamScene = new DreamScene();
            // Get a list of devices
            _devices = new List<BaseDevice>();
            // Read other variables
            _devMode = DataUtil.GetItem<int>("DeviceMode");
            _ambientMode = DataUtil.GetItem<int>("AmbientMode") ?? 0;
            _ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
            var ac = DataUtil.GetItem<string>("AmbientColor") ?? "FFFFFF";
            _ambientColor = ColorFromString(ac);
            _brightness = _dev.Brightness;
            CaptureMode = DataUtil.GetItem("CaptureMode");
            
            // Set default values
            _prevMode = -1;
            _prevAmbientMode = -1;
            _prevAmbientShow = -1;
            _showBuilderStarted = false;
            var devGroup = DataUtil.GetItem<int>("DeviceGroup") ?? 0;
            _group = (byte) devGroup;
            _targetEndpoint = new IPEndPoint(IPAddress.Parse(DataUtil.GetItem("DsIp")), 8888);
            // Start listening service 
            StartListening();
            // Finally start our normal device behavior
            if (!_autoDisabled) UpdateMode(_devMode);
        }

        // This is called because it's a service. I thought I needed this, maybe I don't...
        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            return Task.Run(async () => {
                LogUtil.Write("All DS Services should now be running, executing main loop...");
                var watch = new Stopwatch();
                while (!cancellationToken.IsCancellationRequested) {
                    await Task.Delay(1, cancellationToken);
                }
                LogUtil.Write("DreamClient: Main loop terminated, stopping services...");
                StopServices();
                // Dispose our DB instance
                DataUtil.Dispose();
            });
        }

       

        // This broadcasts the subscribe message that other devices reply to to get color data
       
        // Start a loop that refreshes our devices every 10 minutes
        

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
            _prevMode = DataUtil.GetItem<int>("DeviceMode");
            if (_prevMode == newMode) {
                return;
            }
            // Reload our device data so we're sure it's fresh
            _dev = DataUtil.GetDeviceData();
            _dev.Mode = newMode;
            // Notify web clients of mode change via socket
            LogUtil.Write($@"DreamScreen: Updating mode from {_prevMode} to {newMode}.");
            // If we are not in ambient mode and ambient scene is running, stop it
            
            if (newMode == 3) {
                StartShowBuilder();
            } else {
                StopShowBuilder();
            }            // Store last state
            _prevMode = newMode;
        }

        private void UpdateAmbientMode(int newMode) {
            // If nothing has changed, do nothing
            if (_prevAmbientMode == newMode) {
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
                StopShowBuilder();
            }
            _dreamScene.LoadScene(_ambientShow);
            _showBuilderSource = new CancellationTokenSource();
            Task.Run(() => _dreamScene.BuildColors(_controlService, _showBuilderSource.Token));
            _prevAmbientShow = _ambientShow;
            _showBuilderStarted = true;
        }

        private void StopShowBuilder() {
            if (_showBuilderStarted) {
                CancelSource(_showBuilderSource);
                _showBuilderStarted = false;
            }
        }

        private void UpdateAmbientColor(Color aColor) {
            LogUtil.Write($@"DreamScreen: Setting ambient color to: {aColor.ToString()}.");
            // Create a list
            var colors = new List<Color>();
            for (var q = 0; q < 12; q++) colors.Add(aColor);
            _controlService.SendColors(colors, colors);
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
      
        private void CheckSubscribe(int captureMode) {
            if (captureMode == 0) Subscribe();
        }
        
        private void Subscribe() {
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
                LogUtil.Write($@"Socket exception: {e.Message}.","WARN");
            } catch (ObjectDisposedException) {
                LogUtil.Write("Object Disposed exception caught.","WARN");
            }
            
        }
        
        private void Recv(IAsyncResult res) {
            //Process codes
            try {
                var sourceEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                var receivedResults = _listener.EndReceive(res, ref sourceEndPoint);
                ProcessData(receivedResults, sourceEndPoint);

                _listener.BeginReceive(Recv, null);
            } catch (ObjectDisposedException) {
                LogUtil.Write("Object is already disposed.");
            }
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
                if ((flag == "11" || flag == "17" ||flag == "21") && groupMatch) {
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
                    foreach (var d in _devices) {
                        DataUtil.InsertCollection<BaseDevice>("devices", d);
                    }
                    
                    break;
                case "COLOR_DATA":
                    if (_devMode == 1 || _devMode == 2) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var colors = new List<Color>();
                        foreach (var colorValue in colorData) {
                            colors.Add(ColorFromString(colorValue));
                        }

                        colors = ShiftColors(colors);
                        _controlService.SendColors(colors, colors);
                    }

                    break;
                case "DEVICE_DISCOVERY":
                    if (flag == "30" && from != "0.0.0.0") {
                        SendDeviceStatus(replyPoint);
                    } else if (flag == "60") {
                        if (msgDevice != null) {
                            string dsIpCheck = DataUtil.GetItem("DsIp");
                            if (dsIpCheck == "0.0.0.0" &&
                                msgDevice.Tag.Contains("DreamScreen", StringComparison.CurrentCulture)) {
                                LogUtil.Write(@"Setting a target DS IP.");
                                DataUtil.SetItem("DsIp", from);
                                _targetEndpoint = replyPoint;
                            }

                            if (_discovering) {
                                LogUtil.Write("Sending request for serial!");
                                DreamSender.SendUdpWrite(0x01, 0x03, new byte[]{0},0x60,0,replyPoint);
                                _devices.Add(msgDevice);
                            }
                        }
                    }

                    break;
                case "GET_SERIAL":
                    if (flag == "30") {
                        SendDeviceSerial(replyPoint);
                    } else {
                        LogUtil.Write("DEVICE SERIAL RETRIEVED: " + JsonConvert.SerializeObject(msg));
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
                        LogUtil.Write("UPDATING MODE FROM REMOTE");

                        LogUtil.Write($@"Updating mode: {tDevice.Mode}.");
                    } else {
                        LogUtil.Write("Mode flag set, but we're not doing anything... " + flag);
                    }
                    
                    if (writeState) UpdateMode(tDevice.Mode);

                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState | writeDev) {
                        tDevice.AmbientModeType = payload[0];
                    }

                    if (writeState) {
                        UpdateAmbientMode(tDevice.AmbientModeType);
                    }

                    break;
                case "AMBIENT_SCENE":
                    if (writeState | writeDev) {
                        _ambientShow = payload[0];
                        tDevice.AmbientShowType = _ambientShow;
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
                        tDevice.SkuSetup = payload[0];
                    }

                    break;
                case "FLEX_SETUP":
                    if (writeState | writeDev) {
                        int[] fSetup = payload.Select(x => (int) x).ToArray();
                        tDevice.flexSetup = fSetup;
                    }

                    break;
                case "RESET_PIC":
                    break;
            }

            if (writeState) {
                DataUtil.SetObject("myDevice", tDevice);
                _dev = tDevice;
                DreamSender.SendUdpWrite(msg.C1, msg.C2, msg.GetPayload(), 0x41, (byte)msg.Group, receivedIpEndPoint);
            }

            if (!writeState && !writeDev) return;
            // Notify if the sender was not us
            if (from != _dev.IpAddress) _controlService.NotifyClients();
            DataUtil.InsertDsDevice(tDevice);
        }

        
        private void SendDeviceStatus(IPEndPoint src) {
            var dss = DataUtil.GetDeviceData();
            var payload = dss.EncodeState();
            DreamSender.SendUdpWrite(0x01, 0x0A, payload, 0x60, _group, src);
        }
        
        private void SendDeviceSerial(IPEndPoint src) {
            var serial = DataUtil.GetDeviceSerial();
            DreamSender.SendUdpWrite(0x01, 0x03, ByteUtils.StringBytes(serial), 0x60, _group, src);
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
            CancelSource(_showBuilderSource, true);
            LogUtil.Write("All services have been stopped.");
        }
    }
}