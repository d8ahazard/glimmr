using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.CaptureSource.Ambient;
using Glimmr.Models.StreamingDevice.DreamScreen;
using Glimmr.Models.Util;
using LiteDB;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Color = System.Drawing.Color;

namespace Glimmr.Services {
    public class DreamService : BackgroundService {
        private int CaptureMode { get; set; }
        private Scene _scene;
        private byte _group;
        private Color _ambientColor;
        private int _ambientMode;
        private static IHubContext<SocketServer> _hubContext;
        private ControlService _controlService;
        private int _ambientShow;
        private List<DreamData> _devices;
        private Socket _sender;
        private UdpClient _client;
        private bool _discovering;
        private bool _autoDisabled;
        private int _brightness;
        private DreamData _dev;
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
            _controlService.SetModeEvent += UpdateMode;
            _controlService.RefreshDreamScreenEvent += Discover;
            Initialize();
            LogUtil.Write("Initialisation complete.");
        }
        
        
        // This initializes all of the data in our class and starts function loops
        private void Initialize() {
            LogUtil.Write("Initializing dream client...");
            _sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _sender.EnableBroadcast = true;
            _client = new UdpClient {Ttl = 128};
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _dev = DataUtil.GetDeviceData();
            LogUtil.Write("Device Data: " + JsonConvert.SerializeObject(_dev));
            // Create scene builder
            _scene = new Scene();
            // Get a list of devices
            _devices = new List<DreamData>();
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
                
                _subscribers = new Dictionary<string, int>();
                // Loop until canceled
                try {
                    while (!cancellationToken.IsCancellationRequested) {
                        // Send our subscribe multicast
                        SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x30, (byte) _dev.GroupNumber, null, true);
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
                        await Task.Delay(5000, cancellationToken);
                    }
                } catch (TaskCanceledException) {
                    _subscribers = new Dictionary<string, int>();
                }
                LogUtil.Write("DreamClient: Main loop terminated, stopping services...");
                StopServices();
                // Dispose our DB instance
                DataUtil.Dispose();
            }, cancellationToken);
        }
        
        

        
        
        // Update our device mode
        private void UpdateMode(int newMode) {
            // If the mode doesn't change, we don't need to do anything
            _prevMode = DataUtil.GetItem<int>("DeviceMode");
            if (_prevMode == newMode) {
                return;
            }
            // Reload our device data so we're sure it's fresh
            _dev = DataUtil.GetItem("MyDevice");
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
            _controlService.SetMode(newMode);
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
            _controlService.SetAmbientMode(newMode);
        }
        
        
        private void StartShowBuilder() {
            if (_showBuilderStarted) {
                StopShowBuilder();
            }
            _scene.LoadScene(_ambientShow);
            _showBuilderSource = new CancellationTokenSource();
            Task.Run(() => _scene.BuildColors(_controlService, _showBuilderSource.Token));
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
            _controlService.SendColors(colors);
        }

        private void UpdateAmbientShow(int newShow) {
            if (_prevAmbientShow == newShow) return;
            _scene.LoadScene(newShow);
            _prevAmbientShow = newShow;
            _controlService.SetAmbientShow(newShow);
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
            SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, _targetEndpoint);
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
            DreamData msgDevice = null;
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
                    tDevice = DataUtil.GetCollectionItem<DreamData>("Dev_DreamScreen", from);
                    if (tDevice != null) writeDev = true;
                }
                if (command != null && command != "COLOR_DATA" && command != "SUBSCRIBE" && tDevice != null) {
                    LogUtil.Write($@"{from} -> {tDevice.IpAddress}::{command} {flag}-{msg.Group}.");
                }
            } else {
                LogUtil.Write($@"Invalid message from {from}");
            }
            switch (command) {
                case "SUBSCRIBE":
                    
                    if (_devMode == 1 || _devMode == 2) {
                        SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, replyPoint);    
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
                    _devices = new List<DreamData>();
                    _discovering = true;
                    break;
                case "DISCOVERY_STOP":
                    LogUtil.Write($"DreamScreen: Discovery complete, found {_devices.Count} devices.");
                    _discovering = false;
                    foreach (var d in _devices) {
                        DataUtil.InsertCollection<DreamData>("Dev_DreamScreen", d);
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
                                SendUdpWrite(0x01, 0x03, new byte[]{0},0x60,0,replyPoint);
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
                        tDevice.FlexSetup = fSetup;
                    }

                    break;
                case "RESET_PIC":
                    break;
            }

            if (writeState) {
                DataUtil.SetObject("myDevice", tDevice);
                _dev = tDevice;
                SendUdpWrite(msg.C1, msg.C2, msg.GetPayload(), 0x41, (byte)msg.Group, receivedIpEndPoint);
            }

            if (!writeState && !writeDev) return;
            // Notify if the sender was not us
            if (from != _dev.IpAddress) _controlService.NotifyClients();
            DataUtil.InsertCollection<DreamData>("Dev_DreamScreen", tDevice);
        }
        
        
        public async void Discover(CancellationToken ct) {
            LogUtil.Write("Discovery started..");
            // Send a custom internal message to self to store discovery results
            var selfEp = new IPEndPoint(IPAddress.Loopback, 8888);
            SendUdpWrite(0x01, 0x0D, new byte[] {0x01}, 0x30, 0x00, selfEp);
            // Send our notification to actually discover
            var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
            SendUdpBroadcast(msg);
            await Task.Delay(3000, ct).ConfigureAwait(false);
            SendUdpWrite(0x01, 0x0E, new byte[] {0x01}, 0x30, 0x00, selfEp);
            await Task.Delay(500, ct).ConfigureAwait(false);
        }

        
        private void SendDeviceStatus(IPEndPoint src) {
            var dss = DataUtil.GetDeviceData();
            var payload = dss.EncodeState();
            SendUdpWrite(0x01, 0x0A, payload, 0x60, _group, src);
        }
        
        private void SendDeviceSerial(IPEndPoint src) {
            var serial = DataUtil.GetDeviceSerial();
            SendUdpWrite(0x01, 0x03, ByteUtils.StringBytes(serial), 0x60, _group, src);
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

        #region Messaging

        public void SendSectors(List<Color> sectors, string id, int group) {
            if (sectors == null) throw new InvalidEnumArgumentException("Invalid sector list.");
            byte flag = 0x3D;
            byte c1 = 0x03;
            byte c2 = 0x16;
            var p = new List<byte>();
            foreach (var col in sectors) {
                p.Add(ByteUtils.IntByte(col.R));
                p.Add(ByteUtils.IntByte(col.G));
                p.Add(ByteUtils.IntByte(col.B));
            }
            var ep = new IPEndPoint(IPAddress.Parse(id), 8888);
            SendUdpWrite(c1, c2, p.ToArray(), flag, (byte) group, ep);
        }
        
        public void SetAmbientColor(Color color, string id, int group) {
            if (color == null) throw new InvalidEnumArgumentException("Invalid sector list.");
            byte flag = 0x11;
            byte c1 = 0x03;
            byte c2 = 0x05;
            var p = new List<byte>();
            p.Add(ByteUtils.IntByte(color.R));
            p.Add(ByteUtils.IntByte(color.G));
            p.Add(ByteUtils.IntByte(color.B));
            var ep = new IPEndPoint(IPAddress.Parse(id), 8888);
            SendUdpWrite(c1, c2, p.ToArray(), flag, (byte) group, ep);
        }
        public void SendMessage(string command, dynamic value, string id) {
            var dev = DataUtil.GetDreamDevice(id);
            byte flag = 0x11;
            byte c1 = 0x03;
            byte c2 = 0x00;
            int v;
            var send = false;
            var payload = Array.Empty<byte>();
            var cFlags = MsgUtils.CommandBytes[command];
            if (cFlags != null) {
                c1 = cFlags[0];
                c2 = cFlags[1];
            }
            switch (command) {
                case "saturation":
                    c2 = 0x06;
                    payload = ByteUtils.StringBytes(value);
                    send = true;
                    break;
                case "minimumLuminosity":
                    c2 = 0x0C;
                    v = int.Parse(value);
                    payload = new[] {ByteUtils.IntByte(v), ByteUtils.IntByte(v), ByteUtils.IntByte(v)};
                    send = true;
                    break;
                case "ambientModeType":
                    if (cFlags != null) {
                        payload = new[] {ByteUtils.IntByte((int)value)};
                        c1 = cFlags[0];
                        c2 = cFlags[1];
                        send = true;
                    }
                    break;
                case "ambientScene":
                    if (cFlags != null) {
                        payload = new[] {ByteUtils.IntByte((int)value)};
                        c1 = cFlags[0];
                        c2 = cFlags[1];
                        send = true;
                    }
                    break;
            }

            if (send) {
                var ep = new IPEndPoint(IPAddress.Parse(dev.IpAddress), 8888);
                SendUdpWrite(c1, c2, payload, flag, (byte) dev.GroupNumber, ep, true);
            }
        }

        private void SendUdpWrite(byte command1, byte command2, byte[] payload, byte flag = 17, byte group = 0,
            IPEndPoint ep = null, bool groupSend = false) {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            // If we don't specify an endpoint...talk to self
            ep ??= new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8888);
            // Magic header
            // Payload length
            // Group number
            // Flag, should be 0x10 for subscription, 17 for everything else
            // Upper command
            // Lower command

            var msg = new List<byte> {
                0xFC,
                (byte) (payload.Length + 5),
                group,
                flag,
                command1,
                command2
            };
            // Payload
            msg.AddRange(payload);
            // CRC
            msg.Add(MsgUtils.CalculateCrc(msg.ToArray()));
            if (flag == 0x30 | groupSend) {
                SendUdpBroadcast(msg.ToArray());
                //if (cmd != "SUBSCRIBE" && cmd != "COLOR_DATA") LogUtil.Write($"localhost -> 255.255.255.255::{cmd} {flag}-{group}");
            } else {
                SendUdpUnicast(msg.ToArray(), ep);
                //if (cmd != "SUBSCRIBE" && cmd != "COLOR_DATA") LogUtil.Write($"localhost -> {ep.Address}::{cmd} {flag}-{group}");
            }
        }

        private void SendUdpUnicast(byte[] data, EndPoint ep) {
            try {
                _sender.SendTo(data, ep);
                _sender.Dispose();
            } catch (SocketException e) {
                LogUtil.Write($"Socket Exception: {e.Message}", "WARN");
            }
        }

        private void SendUdpBroadcast(byte[] bytes) {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            try {
                var ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 8888);
                _client.Send(bytes, bytes.Length, ip);
            } catch (SocketException e) {
                LogUtil.Write($"Socket Exception: {e.Message}", "WARN");
            }
        }

        #endregion
    }
}