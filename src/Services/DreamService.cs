using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Services {
    public class DreamService : BackgroundService {
        private int CaptureMode { get; set; }
        private byte _group;
        private Color _ambientColor;
        private int _ambientMode;
        private static IHubContext<SocketServer> _hubContext;
        private readonly ControlService _controlService;
        private int _ambientShow;
        private List<DreamData> _devices;
        private bool _discovering;
        private int _brightness;
        private DreamData _dev;
        private SystemData _sd;
        private readonly DreamUtil _dreamUtil;
        private Dictionary<string, int> _subscribers;

        
        // Our functional values
        private int _devMode;

        private IPEndPoint _listenEndPoint;
        private UdpClient _listener;
        
        // Value used to save where we're replying to
        private IPEndPoint _targetEndpoint;
        

        // We pass hub context to this so we can send data directly to the websocket
        public DreamService(IHubContext<SocketServer> hubContext, ControlService controlService) {
            _hubContext = hubContext;
            _controlService = controlService;
            _controlService.DreamSubscribeEvent += Subscribe;
            _controlService.RefreshDreamscreenEvent += token => Discover(token).ConfigureAwait(false);
            _controlService.RefreshSystemEvent += RefreshSystemData;
            _controlService.SetModeEvent += UpdateMode;
            _controlService.AddSubscriberEvent += AddSubscriber;
            _dreamUtil = new DreamUtil(_controlService.UdpClient);
            _subscribers = new Dictionary<string, int>();
            Initialize();
        }
        
        
        // This initializes all of the data in our class and starts function loops
        private void Initialize() {
            _devices = new List<DreamData>();
            RefreshSystemData();
            // Start listening service 
            StartListening();
        }
        
        private void AddSubscriber(string ip) {
            if (!_subscribers.ContainsKey(ip)) {
                _controlService.EnableDevice(ip);

                Log.Debug("ADDING SUBSCRIBER: " + ip);
            }
            _subscribers[ip] = 3;
        }

        // This is called because it's a service. I thought I needed this, maybe I don't...
        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            Log.Debug("DreamService initialized.");
            return Task.Run(async () => {
                await CheckSubscribers(cancellationToken);
                return Task.CompletedTask;
            }, cancellationToken);
        }
        
        public override Task StopAsync(CancellationToken cancellationToken) {
            Log.Debug("Stopping DreamScreen service...");
            StopServices();
            Log.Debug("Dreamscreen service stopped.");
            return base.StopAsync(cancellationToken);
        }

        private void RefreshSystemData() {
            _dev = DataUtil.GetDeviceData();
            _sd = DataUtil.GetObject<SystemData>("SystemData");
            _devMode = _sd.DeviceMode;
            _ambientMode = _sd.AmbientMode;
            _ambientShow = _sd.AmbientShow;
            _ambientColor = ColorFromString(_sd.AmbientColor);
            _brightness = _dev.Brightness;
            _group = (byte) _dev.DeviceGroup;
            CaptureMode = _sd.CaptureMode;
            
            if (!string.IsNullOrEmpty(_sd.DsIp)) {
                _targetEndpoint = new IPEndPoint(IPAddress.Parse(_sd.DsIp), 8888);    
            }
        }
        
        
        // Update our device mode
        private Task UpdateMode(object o, DynamicEventArgs eventArgs) {
            // If the mode doesn't change, we don't need to do anything
            _devMode = eventArgs.P1;
            return Task.CompletedTask;
        }

        private void UpdateAmbientMode(int newMode) {
            // If nothing has changed, do nothing
            _hubContext.Clients.All.SendAsync("ambientMode", newMode);
            _sd.AmbientMode = newMode;
            DataUtil.SetObject<SystemData>("SystemData", _sd);
        }

        
        
        private void UpdateAmbientColor(Color aColor) {
            _sd.AmbientColor = aColor.ToString();
            DataUtil.SetObject<SystemData>("SystemData", _sd);
        }

        private void UpdateAmbientShow(int newShow) {
            _sd.AmbientShow = newShow;
            DataUtil.SetObject<SystemData>("SystemData", _sd);
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
            var output = Color.FromArgb(0, 0, 0, 0);
            try {
                output = ColorTranslator.FromHtml("#" + inputString);
            } catch (Exception) {
                // ignored
            }

            return output;
        }
      
        
        private void Subscribe() {
            if (_targetEndpoint == null) return;
            _dreamUtil.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, _targetEndpoint).ConfigureAwait(false);
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
                Log.Warning($"Socket exception: {e.Message}");
            } catch (ObjectDisposedException f) {
                Log.Warning($"Object already disposed: {f.Message}");
            }
        }
        
        private void Recv(IAsyncResult res) {
            //Process codes
            try {
                var sourceEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                var receivedResults = _listener.EndReceive(res, ref sourceEndPoint);
                ProcessData(receivedResults, sourceEndPoint).ConfigureAwait(false);
                _listener.BeginReceive(Recv, null);
            } catch (ObjectDisposedException) {
                Log.Warning("Object is already disposed.");
            }
        }

       

        private async Task ProcessData(byte[] receivedBytes, IPEndPoint receivedIpEndPoint) {
            // Convert data to ASCII and print in console
            var f1 = receivedBytes[4];
            var f2 = receivedBytes[5];
            var setValid = false;
            if (!MsgUtils.CheckCrc(receivedBytes)) {
                if (f1 == 5 && f2 == 22) {
                    setValid = true;
                } else {
                    Log.Debug("INVALID CRC");
                    return;    
                }
                
            }
            string command = null;
            string flag = null;
            var from = receivedIpEndPoint.Address.ToString();
            var areYouLocal = from == IpUtil.GetLocalIpAddress();
            var replyPoint = new IPEndPoint(receivedIpEndPoint.Address, 8888);
            var payloadString = string.Empty;
            var payload = Array.Empty<byte>();
            DreamData msgDevice = null;
            var writeState = false;
            var writeDev = false;
            var refreshDevice = false;
            var msg = new DreamscreenMessage(receivedBytes, from);
            var tDevice = DataUtil.GetDeviceData();
            if (string.IsNullOrEmpty(tDevice.IpAddress)) {
                tDevice.IpAddress = IpUtil.GetLocalIpAddress();
                DataUtil.SetDeviceData(tDevice);
            }
            if (msg.IsValid || setValid) {
                payload = msg.GetPayload();
                payloadString = msg.PayloadString;
                command = msg.Command;
                msgDevice = msg.Device;
                flag = msg.Flags;
                var groupMatch = msg.Group == _dev.DeviceGroup || msg.Group == 255;
                if ((flag == "11" || flag == "17" ||flag == "21") && groupMatch) {
                    writeState = true;
                    writeDev = true;
                    refreshDevice = true;
                }
                if (flag == "41") {
                    //Log.Debug($"Flag is 41, we should save settings for {from}.");
                    tDevice = DataUtil.GetCollectionItem<DreamData>("Dev_Dreamscreen", from);
                    if (tDevice != null) {
                        refreshDevice = true;
                        writeDev = true;
                    }
                }
                if (command != null && command != "COLOR_DATA" && command != "SUBSCRIBE" && tDevice != null) {
                    //Log.Debug($@"{from} -> {tDevice.IpAddress}::{command} {flag}-{msg.Group}.");
                }
            } else {
                Log.Warning($@"Invalid message from {from}");
            }
            switch (command) {
                case "SUBSCRIBE":
                    if (_devMode == 1 || _devMode == 2 && !areYouLocal) {
                        //Log.Debug("Sending sub message.");
                        await _dreamUtil.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, replyPoint).ConfigureAwait(false);    
                    }
                    
                    // If the device is on and capture mode is not using DS data
                    if (_devMode != 0 && CaptureMode != 0 && !areYouLocal) {
                        // If the device is replying to our sub broadcast
                        if (flag == "60") {
                            // Set our count to 3, which is how many tries we get before we stop sending data
                            if (!string.IsNullOrEmpty(from)) {
                                _controlService.AddSubscriber(from);
                            } else {
                                Log.Warning("Can't add subscriber, from is empty...");
                            }
                            
                        }
                    } 
                    break;
                case "DISCOVERY_START":
                    if (!areYouLocal) break;
                    Log.Debug("Dreamscreen: Starting discovery.");
                    _devices = new List<DreamData>();
                    _discovering = true;
                    break;
                case "DISCOVERY_STOP":
                    if (!areYouLocal) break;
                    Log.Debug($"Dreamscreen: Discovery complete, found {_devices.Count} devices.");
                    _discovering = false;
                    foreach (var d in _devices) {
                        await DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", d);
                    }
                    
                    break;
                case "COLOR_DATA":
                    if (_devMode == 1 || _devMode == 2) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var colors = colorData.Select(ColorFromString).ToList();
                        colors = ShiftColors(colors);
                        _controlService.SendColors(colors, colors);
                    }
                    break;
                case "COLOR_DATA_V2":
                    refreshDevice = false;
                    if (_devMode == 5) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var ledColors = colorData.Select(ColorFromString).ToList();
                        var sectorColors = ColorUtil.LedsToSectors(ledColors, _sd);
                        _controlService.SendColors(ledColors, sectorColors);
                    }

                    break;
                case "DEVICE_DISCOVERY":
                    if (flag == "30" && !areYouLocal) {
                        await SendDeviceStatus(replyPoint);
                    } else if (flag == "60" && _discovering && !areYouLocal) {
                        if (msgDevice != null) {
                            _targetEndpoint = replyPoint;
                            msgDevice.IpAddress = from;
                            await DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", msgDevice);
                            _devices.Add(msgDevice);
                            await _dreamUtil.SendUdpWrite(0x01, 0x03, new byte[]{0},0x60,0,replyPoint).ConfigureAwait(false);
                            
                        } else {
                            Log.Warning("Message device is null!.");
                        }
                    }

                    break;
                case "GET_SERIAL":
                    if (flag == "30") {
                        await SendDeviceSerial(replyPoint);
                    } else {
                        DreamData md = DataUtil.GetCollectionItem<DreamData>("Dev_Dreamscreen", from);
                        if (md != null) {
                            md.SerialNumber = msg.PayloadString;
                            await DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", md).ConfigureAwait(false);
                        }
                    }
                    
                    break;
                
                case "GROUP_NAME":
                    var gName = Encoding.ASCII.GetString(payload);
                    Log.Debug("Setting group name to " + gName);
                    if (tDevice != null && writeState | writeDev) tDevice.GroupName = gName;

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    Log.Debug("Setting group number to " + gNum);
                    if (tDevice != null && writeState | writeDev) tDevice.DeviceGroup = gNum;

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (tDevice != null && writeState | writeDev) tDevice.Name = dName;

                    break;
                case "BRIGHTNESS":
                    _brightness = payload[0];
                    if (tDevice != null && writeState | writeDev) {
                        Log.Debug($@"Setting brightness to {_brightness}.");
                        tDevice.Brightness = payload[0];
                    }
                    if (writeState) UpdateBrightness(payload[0]);

                    break;
                case "SATURATION":
                    if (tDevice != null && writeState | writeDev) {
                        tDevice.Saturation = ByteUtils.ByteString(payload);
                    }

                    break;
                case "MODE":
                    if (tDevice != null && writeState | writeDev && !areYouLocal) {
                        refreshDevice = false;
                        tDevice.DeviceMode = payload[0];
                    } else {
                        Log.Debug("Mode flag set, but we're not doing anything... " + flag);
                    }
                    
                    if (tDevice != null && writeState && !areYouLocal) await _controlService.SetMode(tDevice.DeviceMode).ConfigureAwait(false);

                    break;
                case "AMBIENT_MODE_TYPE":
                    if (tDevice != null && writeState | writeDev) {
                        tDevice.AmbientMode = payload[0];
                    }

                    if (writeState && tDevice != null) {
                        UpdateAmbientMode(tDevice.AmbientMode);
                    }

                    break;
                case "AMBIENT_SCENE":
                    if (tDevice != null && writeState | writeDev) {
                        _ambientShow = payload[0];
                        tDevice.AmbientShowType = _ambientShow;
                    }
                    if (writeState) UpdateAmbientShow(_ambientShow);
                    break;
                case "AMBIENT_COLOR":
                    if (writeDev | writeState) {
                        if (tDevice != null) tDevice.AmbientColor = ByteUtils.ByteString(payload);
                    }
                    if (writeState && tDevice != null) UpdateAmbientColor(ColorUtil.ColorFromHex(tDevice.AmbientColor));

                    break;
                case "SKU_SETUP":
                    if (tDevice != null && writeState | writeDev) {
                        tDevice.SkuSetup = payload[0];
                    }

                    break;
                case "FLEX_SETUP":
                    if (tDevice != null && writeState | writeDev) {
                        int[] fSetup = payload.Select(x => (int) x).ToArray();
                        tDevice.FlexSetup = fSetup;
                    }

                    break;
                case "RESET_PIC":
                    break;
            }

            if (writeState) {
                DataUtil.SetDeviceData(tDevice);
                _dev = tDevice;
                await _dreamUtil.SendUdpWrite(msg.C1, msg.C2, msg.GetPayload(), 0x41, (byte)msg.Group, receivedIpEndPoint).ConfigureAwait(false);
            }

            if (!writeState) return;
            // Notify if the sender was not us
            if (!areYouLocal && refreshDevice) {
                await _controlService.NotifyClients().ConfigureAwait(false);
                if (tDevice != null) await _controlService.RefreshDevice(tDevice.Id);
            }

            if (tDevice == null) return;
            DreamData ex = DataUtil.GetCollectionItem<DreamData>("Dev_Dreamscreen", tDevice.Id);
            if (ex != null) {
                tDevice.Enable = ex.Enable;
            }
            await DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", tDevice);
        }


        private async Task Discover(CancellationToken ct) {
            try {
                Log.Debug("Dreamscreen: Discovery started...");
                _discovering = true;
                // Send our notification to actually discover
                var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
                await _dreamUtil.SendUdpMessage(msg);
                while (!ct.IsCancellationRequested) {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                _discovering = false;
                Log.Debug("Dreamscreen: Discovery complete.");
            } catch (Exception e) {
                Log.Warning("Discovery exception: " + e.Message);
            }
            
        }

        
        private async Task SendDeviceStatus(IPEndPoint src) {
            var dss = DataUtil.GetDeviceData();
            var payload = dss.EncodeState();
            await _dreamUtil.SendUdpWrite(0x01, 0x0A, payload, 0x60, _group, src);
        }
        
        private async Task SendDeviceSerial(IPEndPoint src) {
            var serial = DataUtil.GetDeviceSerial();
            await _dreamUtil.SendUdpWrite(0x01, 0x03, ByteUtils.StringBytes(serial), 0x60, _group, src);
        }

        /// <summary>
        /// Take our input colors from DS, which are in the wrong spots, and make them into the big v2 array.
        /// </summary>
        /// <param name="input">The original 12 sectors from DS</param>
        /// <returns>a "V2" array of colors.</returns>
        private static List<Color> ShiftColors(List<Color> input) {
            if (input.Count != 12) return input;
            var output = new List<Color> {
                input[0],
                ColorUtil.AverageColors(input[0], input[1]),
                input[1],
                ColorUtil.AverageColors(input[1], input[2]),
                input[2],
                input[2],
                input[2],
                ColorUtil.AverageColors(input[3], input[4]),
                input[4],
                ColorUtil.AverageColors(input[4], input[5]),
                ColorUtil.AverageColors(input[4], input[5]),
                input[5],
                ColorUtil.AverageColors(input[5], input[6]),
                input[6],
                input[6],
                input[6],
                input[7],
                ColorUtil.AverageColors(input[7], input[8]),
                input[8],
                ColorUtil.AverageColors(input[9], input[10]),
                ColorUtil.AverageColors(input[9], input[10]),
                input[10],
                ColorUtil.AverageColors(input[10], input[11]),
                input[11],
                input[11],
                input[11].Blend(input[0], .5),
                input[11].Blend(input[0], .5),
                input[0]
            };
            return output;
        }
        
        private async Task CheckSubscribers(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    await _dreamUtil.SendBroadcastMessage(_dev.DeviceGroup);
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
                } catch (TaskCanceledException) {
                    _subscribers = new Dictionary<string, int>();
                }	
                await Task.Delay(5000, ct);
            }
			
        }


        private void StopServices() {
            _listener?.Dispose();
        }

        #region Messaging

       

        #endregion
    }
}