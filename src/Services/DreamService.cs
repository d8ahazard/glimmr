﻿using System;
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
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.StreamingDevice.Dreamscreen;
using Glimmr.Models.Util;
using LiteDB;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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
        private DreamUtil _dreamUtil;
        
        // Our functional values
        private int _devMode;

        private IPEndPoint _listenEndPoint;
        private UdpClient _listener;

        
        // Used by our loops to know when to update?
        private int _prevMode;

        
        
        // Value used to save where we're replying to
        private IPEndPoint _targetEndpoint;
        

        // We pass hub context to this so we can send data directly to the websocket
        public DreamService(IHubContext<SocketServer> hubContext, ControlService controlService) {
            _hubContext = hubContext;
            _controlService = controlService;
            _controlService.SetCaptureModeEvent += CheckSubscribe;
            _controlService.DreamSubscribeEvent += Subscribe;
            _controlService.RefreshDreamscreenEvent += Discover;
            _dreamUtil = new DreamUtil(_controlService.UdpClient);
            Initialize();
            Log.Debug("Initialisation complete.");
        }
        
        
        // This initializes all of the data in our class and starts function loops
        private void Initialize() {
            _dev = DataUtil.GetDeviceData();
            _devMode = _dev.DeviceMode;
            _ambientMode = _dev.AmbientMode;
            _ambientShow = _dev.AmbientShowType;
            _ambientColor = ColorFromString(_dev.AmbientColor);
            _brightness = _dev.Brightness;
            _group = (byte) _dev.DeviceGroup;
            CaptureMode = DataUtil.GetItem("CaptureMode") ?? 2;
            
            // Set default values
            _prevMode = -1;
            var dsIp = DataUtil.GetItem("DsIp");
            _devices = new List<DreamData>();

            if (!string.IsNullOrEmpty(dsIp)) {
                _targetEndpoint = new IPEndPoint(IPAddress.Parse(dsIp), 8888);    
            }
            // Start listening service 
            StartListening();
        }

        // This is called because it's a service. I thought I needed this, maybe I don't...
        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            return Task.Run(async () => {
                while (!cancellationToken.IsCancellationRequested) {
                    await Task.Delay(1, cancellationToken);
                }
                return Task.CompletedTask;
            }, cancellationToken);
        }
        
        public override Task StopAsync(CancellationToken cancellationToken) {
            Log.Debug("Stopping DreamScreen service...");
            StopServices();
            Log.Debug("Dreamscreen service stopped.");
            return base.StopAsync(cancellationToken);
        }
        
        
        // Update our device mode
        private void UpdateMode(int newMode) {
            // If the mode doesn't change, we don't need to do anything
            _devMode = newMode;
        }

        private void UpdateAmbientMode(int newMode) {
            // If nothing has changed, do nothing
            _hubContext.Clients.All.SendAsync("ambientMode", newMode);
            _controlService.SetAmbientMode(newMode);
        }

        
        
        private void UpdateAmbientColor(Color aColor) {
            _controlService.SetAmbientColor(aColor, "-1", -1);
        }

        private void UpdateAmbientShow(int newShow) {
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
            if (_targetEndpoint == null) return;
            _dreamUtil.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, _targetEndpoint);
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
                ProcessData(receivedResults, sourceEndPoint);
                _listener.BeginReceive(Recv, null);
            } catch (ObjectDisposedException) {
                Log.Warning("Object is already disposed.");
            }
        }

       

        private void ProcessData(byte[] receivedBytes, IPEndPoint receivedIpEndPoint) {
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) return;
            string command = null;
            string flag = null;
            var from = receivedIpEndPoint.Address.ToString();
            var areYouLocal = (from == IpUtil.GetLocalIpAddress());
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
            if (msg.IsValid) {
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
                    Log.Debug($"Flag is 41, we should save settings for {from}.");
                    tDevice = DataUtil.GetCollectionItem<DreamData>("Dev_Dreamscreen", from);
                    if (tDevice != null) {
                        refreshDevice = true;
                        writeDev = true;
                    }
                }
                if (command != null && command != "COLOR_DATA" && command != "SUBSCRIBE" && tDevice != null) {
                    Log.Debug($@"{from} -> {tDevice.IpAddress}::{command} {flag}-{msg.Group}.");
                }
            } else {
                Log.Warning($@"Invalid message from {from}");
            }
            switch (command) {
                case "SUBSCRIBE":
                    if (_devMode == 1 || _devMode == 2 && !areYouLocal) {
                        //Log.Debug("Sending sub message.");
                        _dreamUtil.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x10, _group, replyPoint);    
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
                        DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", d);
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
                case "DEVICE_DISCOVERY":
                    Log.Debug("DISCOVERY: " + (flag == "60") + " and " + _discovering + " and " + areYouLocal);
                    if (flag == "30" && !areYouLocal) {
                        SendDeviceStatus(replyPoint);
                    } else if (flag == "60" && _discovering && !areYouLocal) {
                        if (msgDevice != null) {
                            _targetEndpoint = replyPoint;
                            Log.Debug("Sending request for serial!");
                            msgDevice.IpAddress = from;
                            DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", msgDevice);
                            _devices.Add(msgDevice);
                            _dreamUtil.SendUdpWrite(0x01, 0x03, new byte[]{0},0x60,0,replyPoint);
                            
                        } else {
                            Log.Warning("Message device is null!.");
                        }
                    }

                    break;
                case "GET_SERIAL":
                    if (flag == "30") {
                        SendDeviceSerial(replyPoint);
                    } else {
                        Log.Debug("DEVICE SERIAL RETRIEVED: " + JsonConvert.SerializeObject(msg));
                        var md = DataUtil.GetCollectionItem<DreamData>("Dev_Dreamscreen", from);
                        if (md != null) {
                            md.SerialNumber = msg.PayloadString;
                            DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", md);
                        }
                    }
                    
                    break;
                
                case "GROUP_NAME":
                    var gName = Encoding.ASCII.GetString(payload);
                    Log.Debug("Setting group name to " + gName);
                    if (writeState | writeDev) tDevice.GroupName = gName;

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    Log.Debug("Setting group number to " + gNum);
                    if (writeState | writeDev) tDevice.DeviceGroup = gNum;

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (writeState | writeDev) tDevice.Name = dName;

                    break;
                case "BRIGHTNESS":
                    _brightness = payload[0];
                    if (writeState | writeDev) {
                        Log.Debug($@"Setting brightness to {_brightness}.");
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
                    if (writeState | writeDev && !areYouLocal) {
                        refreshDevice = false;
                        tDevice.DeviceMode = payload[0];
                    } else {
                        Log.Debug("Mode flag set, but we're not doing anything... " + flag);
                    }
                    
                    if (writeState && !areYouLocal) _controlService.SetMode(tDevice.DeviceMode);

                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState | writeDev) {
                        tDevice.AmbientMode = payload[0];
                    }

                    if (writeState) {
                        UpdateAmbientMode(tDevice.AmbientMode);
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
                    if (writeState && tDevice != null) UpdateAmbientColor(ColorUtil.ColorFromHex(tDevice.AmbientColor));

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
                DataUtil.SetDeviceData(tDevice);
                _dev = tDevice;
                _dreamUtil.SendUdpWrite(msg.C1, msg.C2, msg.GetPayload(), 0x41, (byte)msg.Group, receivedIpEndPoint);
            }

            if (!writeState || !writeDev) return;
            // Notify if the sender was not us
            if (!areYouLocal && refreshDevice) {
                _controlService.NotifyClients();
                _controlService.RefreshDevice(tDevice.Id);
            }

            if (tDevice == null) return;
            DreamData ex = DataUtil.GetCollectionItem<DreamData>("Dev_Dreamscreen", tDevice.Id);
            if (ex != null) {
                tDevice.Enable = ex.Enable;
            }
            DataUtil.InsertCollection<DreamData>("Dev_Dreamscreen", tDevice);
        }


        private async void Discover(CancellationToken ct) {
            try {
                Log.Debug("Discovery started..");
                _discovering = true;
                // Send a custom internal message to self to store discovery results
                var selfEp = new IPEndPoint(IPAddress.Loopback, 8888);
                _dreamUtil.SendUdpWrite(0x01, 0x0D, new byte[] {0x01}, 0x30, 0x00, selfEp);
                // Send our notification to actually discover
                var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
                _dreamUtil.SendUdpMessage(msg);
                await Task.Delay(3000, ct).ConfigureAwait(false);
                _dreamUtil.SendUdpWrite(0x01, 0x0E, new byte[] {0x01}, 0x30, 0x00, selfEp);
                await Task.Delay(500, ct).ConfigureAwait(false);
                _discovering = false;
            } catch (Exception e) {
                Log.Warning("Discovery exception: ", e);
            }
        }

        
        private void SendDeviceStatus(IPEndPoint src) {
            var dss = DataUtil.GetDeviceData();
            var payload = dss.EncodeState();
            _dreamUtil.SendUdpWrite(0x01, 0x0A, payload, 0x60, _group, src);
        }
        
        private void SendDeviceSerial(IPEndPoint src) {
            var serial = DataUtil.GetDeviceSerial();
            _dreamUtil.SendUdpWrite(0x01, 0x03, ByteUtils.StringBytes(serial), 0x60, _group, src);
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


        private void StopServices() {
            _listener?.Dispose();
        }

        #region Messaging

       

        #endregion
    }
}