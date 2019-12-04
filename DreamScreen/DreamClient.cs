using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HueDream.DreamScreen.Devices;
using HueDream.DreamScreen.Scenes;
using HueDream.HueDream;
using HueDream.Util;
using Newtonsoft.Json;

namespace HueDream.DreamScreen {
    internal class DreamClient : IDisposable {

        public string[] Colors { get; private set; }
        public SceneBase SceneBase { get; private set; }
        public int Brightness { get; private set; }

        public static bool Listening { get; set; }
        private static bool _searching;
        private static List<BaseDevice> Devices { get; set; }
        private int DeviceMode { get; set; }

        private readonly DreamSync dreamSync;
        private IPEndPoint targetEndpoint;
        private IPEndPoint listenEndPoint;
        private UdpClient listener;
        private BaseDevice dev;
        private readonly DreamScene dreamScene;
        private CancellationTokenSource cts;
        private int ambientMode;
        private int ambientShow;
        private int prevShow;
        private bool showStarted;
        private readonly byte group;


        public DreamClient(DreamSync ds) {
            var dd = DreamData.GetStore();
            dev = GetDeviceData();
            dreamScene = new DreamScene();
            ambientMode = dev.AmbientModeType;
            ambientShow = dev.AmbientShowType;
            prevShow = -1;
            showStarted = false;
            var devB = dev.Brightness;
            Brightness = devB;
            string sourceIp = dd.GetItem("dsIp");
            group = (byte)dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            Colors = new string[12];
            for (var i = 0; i < Colors.Length; i++) {
                Colors[i] = "FFFFFF";
            }
            SceneBase = null;
            DeviceMode = -1;
            dreamSync = ds;
            UpdateMode(dev.Mode);
        }

        public DreamClient() {
        }

        private static BaseDevice GetDeviceData() {
            using var dd = DreamData.GetStore();
            BaseDevice myDev;
            var devType = dd.GetItem<string>("emuType");
            if (devType == "SideKick") {
                myDev = dd.GetItem<SideKick>("myDevice");
            } else {
                myDev = dd.GetItem<Connect>("myDevice");
            }
            return myDev;
        }

        private void UpdateMode(int newMode) {
            dev = DreamData.GetDeviceData();
            if (DeviceMode == newMode && newMode != -1) return;
            DeviceMode = newMode;
            dev.Mode = newMode;
            Console.WriteLine($@"DreamScreen: Updating mode to {dev.Mode}.");
            // If ambient, set to ambient colorS   
            var sync = newMode != 0;
            if (newMode == 3) {
                if (dev.AmbientModeType == 0) {
                    SetAmbientColor(dev.AmbientColor);
                }
            } else if (newMode == 2 || newMode == 1) {
                Console.WriteLine($@"DreamScreen: Subscribing to sector data for mode: {newMode}");
                Subscribe();
            }

            dreamSync.CheckSync(sync);
        }

        public async Task CheckShow(CancellationToken sCt) {
            await Task.Run(() => {
                while (!sCt.IsCancellationRequested) {
                    // If we're still in ambient mode
                    if (DeviceMode == 3 && ambientMode == 1) {
                        // Start our show generation if it's not running.
                        if (!showStarted) {
                            Console.WriteLine($@"Starting new ambient show: {ambientShow}.");
                            SceneBase = dreamScene.CurrentScene;
                            Console.WriteLine($@"Updated stored base to {JsonConvert.SerializeObject(SceneBase)}.");
                            Task.Run(() => dreamScene.BuildColors(cts.Token), sCt).ConfigureAwait(false);
                            prevShow = ambientShow;
                            showStarted = true;
                        } else {
                            if (prevShow != ambientShow) {
                                dreamScene.LoadScene(ambientShow);
                                prevShow = ambientShow;
                            }
                        }

                        // Start updating our color data
                        if (showStarted) {
                            Colors = dreamScene.GetColorArray();
                            //Console.WriteLine("COLORS: " + JsonConvert.SerializeObject(colors));
                        }

                    }

                    // If our ambient mode is not 1 and the show is running...
                    if (DeviceMode == 3 && ambientMode == 1 || !showStarted) continue;
                    Console.WriteLine($@"Stopping ambient show: {ambientShow}.");
                    showStarted = false;
                    SceneBase = null;
                    cts.Cancel();
                    cts = new CancellationTokenSource();
                }
            }, sCt).ConfigureAwait(true);
        }

        private void SetAmbientColor(string aColor) {
            Console.WriteLine($@"DreamScreen: Setting ambient color to: {aColor}.");
            for (var i = 0; i < Colors.Length; i++) {
                Colors[i] = aColor;
            }
        }

        public void Subscribe() {
            DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10, group, targetEndpoint);
        }


        public async Task Listen() {
            if (!Listening) {
                // Create UDP client
                await Task.Run(() => {
                    try {
                        listenEndPoint = new IPEndPoint(IPAddress.Any, 8888);
                        listener = new UdpClient();
                        listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        listener.EnableBroadcast = true;
                        listener.Client.Bind(listenEndPoint);
                        Listening = true;
                    } catch (SocketException e) {
                        Console.WriteLine($@"Socket exception: {e.Message}.");
                    }
                }).ConfigureAwait(true);
                Console.WriteLine($@"DreamScreen: Starting UDP receiving on port {listenEndPoint.Port}.");
                // Call DataReceived() every time it gets something
                listener.BeginReceive(DataReceived, listener);
            } else {
                Console.WriteLine(@"DreamScreen: Listen terminated.");
            }
        }

        private void DataReceived(IAsyncResult ar) {
            var c = (UdpClient)ar.AsyncState;
            var receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            var receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) {
                return;
            }
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
                string[] ignore = { "SUBSCRIBE", "READ_CONNECT_VERSION?", "COLOR_DATA", "DEVICE_DISCOVERY" };
                if (!ignore.Contains(command)) {
                    Console.WriteLine($@"{from} -> {JsonConvert.SerializeObject(msg)}.");
                }
                flag = msg.Flags;
                var groupMatch = msg.Group == dev.GroupNumber;
                if ((flag == "11" || flag == "21") && groupMatch) {
                    dev = GetDeviceData();
                    writeState = true;
                }
                if (!groupMatch) {
                    Console.WriteLine($@"Group {msg.Group} doesn't match {dev.GroupNumber}.");
                }
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (DeviceMode == 1 || DeviceMode == 2) {
                        DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10, group, replyPoint);
                    }
                    break;
                case "COLOR_DATA":
                    if (DeviceMode == 1 || DeviceMode == 2) {
                        var colorData = ByteUtils.SplitHex(payloadString, 6); // Swap this with payload
                        var lightCount = 0;
                        foreach (var colorValue in colorData) {
                            Colors[lightCount] = colorValue;
                            lightCount++;
                            if (lightCount > 11) {
                                break;
                            }
                        }
                    }
                    break;
                case "DEVICE_DISCOVERY":
                    if (flag == "30" && from != "0.0.0.0") {
                        SendDeviceStatus(replyPoint);
                    } else if (flag == "60") {
                        if (msgDevice != null) {
                            string dsIpCheck = DreamData.GetItem("dsIp");
                            if (dsIpCheck == "0.0.0.0" && msgDevice.Tag.Contains("DreamScreen", StringComparison.CurrentCulture)) {
                                Console.WriteLine(@"No DS IP Set, setting.");
                                DreamData.SetItem("dsIp", from);
                                targetEndpoint = replyPoint;
                            }
                            if (_searching) {
                                Devices.Add(msgDevice);
                            }
                        }
                    }
                    break;
                case "GROUP_NAME":
                    var gName = Encoding.ASCII.GetString(payload);
                    if (writeState) {
                        dev.GroupName = gName;
                    }

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    if (writeState) {
                        dev.GroupNumber = gNum;
                    }

                    break;
                case "NAME":
                    var dName = Encoding.ASCII.GetString(payload);
                    if (writeState) {
                        dev.Name = dName;
                    }

                    break;
                case "BRIGHTNESS":
                    Brightness = payload[0];
                    if (writeState) {
                        Console.WriteLine($@"Setting brightness to {Brightness}.");
                        dev.Brightness = payload[0];
                    }

                    break;
                case "SATURATION":
                    if (writeState) {
                        dev.Saturation = ByteUtils.ByteString(payload);
                    }

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
                        ambientMode = payload[0];
                    }

                    break;
                case "AMBIENT_SCENE":
                    if (writeState) {
                        dev.AmbientShowType = payload[0];
                        ambientShow = payload[0];
                        Console.WriteLine($@"Scene updated: {ambientShow}.");
                    }

                    break;
                case "AMBIENT_COLOR":
                    if (writeState) {
                        dev.AmbientColor = ByteUtils.ByteString(payload);
                        SetAmbientColor(dev.AmbientColor);
                    }

                    break;
            }

            if (writeState) {
                DreamData.SetItem<BaseDevice>("myDevice", dev);
            }
            // Restart listening for udp data packages
            if (Listening) {
                c.BeginReceive(DataReceived, ar.AsyncState);
            } else {
                Console.WriteLine(@"DreamScreen: Closing listening loop.");
                c.Close();
            }
        }

        private void SendDeviceStatus(IPEndPoint src) {
            var dss = GetDeviceData();
            var payload = dss.EncodeState();
            DreamSender.SendUdpWrite(0x01, 0x0A, payload, 0x60, group, src);
        }


        public async Task<List<BaseDevice>> FindDevices() {
            _searching = true;
            Devices = new List<BaseDevice>();
            var msg = new byte[] { 0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A };
            DreamSender.SendUdpBroadcast(msg);
            var s = new Stopwatch();
            s.Start();
            await Task.Delay(3000).ConfigureAwait(true);
            Devices = Devices.Distinct().ToList();
            s.Stop();
            _searching = false;
            return Devices;
        }

        public void Dispose() {
        }
    }
}