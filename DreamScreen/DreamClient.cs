using HueDream.DreamScreen.Devices;
using HueDream.HueDream;
using HueDream.Util;
using JsonFlatFileDataStore;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace HueDream.DreamScreen {
    internal class DreamClient : IDisposable {

        public string[] colors { get; }
        public int Brightness { get; set; }
        public int[] Saturation { get; set; }

        public static bool listening { get; set; }
        public static bool subscribed { get; set; }
        private static bool searching = false;
        public static List<BaseDevice> devices { get; set; }
        public int deviceMode { get; set; }

        private readonly DreamSync dreamSync;
        private IPEndPoint targetEndpoint;
        private IPEndPoint listenEndPoint;
        private UdpClient listener;
        private readonly string sourceIp;
        private readonly byte group;


        public DreamClient(DreamSync ds) {
            DataStore dd = DreamData.getStore();
            BaseDevice dev = GetDeviceData();
            int devB = dev.Brightness;
            Brightness = devB;
            Saturation = dev.Saturation;
            sourceIp = dd.GetItem("dsIp");
            group = (byte)dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            colors = new string[12];
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = "FFFFFF";
            }
            deviceMode = -1;
            dreamSync = ds;
        }

        public DreamClient() {
        }

        private BaseDevice GetDeviceData() {
            using DataStore dd = DreamData.getStore();
            BaseDevice dev;
            string devType = dd.GetItem<string>("emuType");
            if (devType == "SideKick") {
                dev = dd.GetItem<SideKick>("myDevice");
            } else {
                dev = dd.GetItem<Connect>("myDevice");
            }
            return dev;
        }

        public void UpdateMode(int newMode) {
            BaseDevice dev = DreamData.GetDeviceData();
            if (deviceMode != newMode || newMode == -1) {
                deviceMode = newMode;
                dev.Mode = newMode;
                Console.WriteLine("DreamScreen: Updating mode to " + dev.Mode);
                // If ambient, set to ambient colorS   
                bool sync = (newMode != 0);
                if (newMode == 3) {
                    SetAmbientColor(dev.AmbientColor);
                    Console.WriteLine("DreamScreen: Updating color array to use ambient color: " + colors[0]);
                } else if (newMode == 2 || newMode == 1) {
                    Console.WriteLine("DreamScreen: Subscribing to sector data.");
                    Subscribe();
                }
                dreamSync.CheckSync(sync);
            }
        }

        private void SetAmbientColor(int[] aColor) {
            string cString = aColor[0].ToString("X2");
            cString += aColor[1].ToString("X2");
            cString += aColor[2].ToString("X2");
            Console.WriteLine("DreamScreen: Setting ambient color to: " + cString + " from " + JsonConvert.SerializeObject(aColor));
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = cString;
            }
        }

        private void SaveDeviceState(BaseDevice dev) {
            DataStore dd = DreamData.getStore();
            dd.ReplaceItemAsync("myDevice", dev);
            dd.Dispose();

        }


        public void Subscribe() {
            DreamSender.SendUDPWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10, group, targetEndpoint);
        }


        public async Task Listen() {
            if (!listening) {
                // Create UDP client
                try {
                    listenEndPoint = new IPEndPoint(IPAddress.Any, 8888); ;
                    listener = new UdpClient();
                    listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    listener.EnableBroadcast = true;
                    listener.Client.Bind(listenEndPoint);
                    listening = true;
                } catch (SocketException e) {
                    Console.WriteLine("Socket exception: " + e.Message);
                }
                Console.WriteLine("DreamScreen: Starting UDP receiving on port " + listenEndPoint.Port);
                // Call DataReceived() every time it gets something
                listener.BeginReceive(DataReceived, listener);
                UpdateMode(-1);
            } else {
                Console.WriteLine("DreamScreen: Listen terminated");
            }
        }

        private void DataReceived(IAsyncResult ar) {
            UdpClient c = (UdpClient)ar.AsyncState;
            IPEndPoint receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);
            // Convert data to ASCII and print in console
            if (!MsgUtils.CheckCrc(receivedBytes)) {
                Console.WriteLine("DreamScreen: CRC Check Failed!");
                return;
            }
            //Console.WriteLine("DS: Brightness is " + Brightness);
            string command = null;
            string flag = null;
            string from = receivedIpEndPoint.Address.ToString();
            IPEndPoint replyPoint = new IPEndPoint(receivedIpEndPoint.Address, 8888);
            string[] payloadString = Array.Empty<string>();
            byte[] payload = Array.Empty<byte>();
            BaseDevice msgDevice = null;
            BaseDevice dss = null;
            bool writeState = false;

            try {
                DreamScreenMessage msg = new DreamScreenMessage(receivedBytes, from);
                if (msg.IsValid) {
                    payload = msg.payload;
                    payloadString = msg.payloadString;
                    command = msg.command;
                    msgDevice = msg.device;
                    string[] ignore = { "SUBSCRIBE", "READ_CONNECT_VERSION?", "COLOR_DATA", "DEVICE_DISCOVERY" };
                    if (!ignore.Contains(command)) {
                        Console.WriteLine(from + " -> " + JsonConvert.SerializeObject(msg));
                    }
                    flag = msg.flags;
                    if (flag == "11" || flag == "21") {
                        dss = GetDeviceData();
                        writeState = true;
                    }
                }
            } catch (Exception e) {
                Console.WriteLine("MSG parse Exception: " + e.Message);
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (deviceMode == 1 || deviceMode == 2) {
                        DreamSender.SendUDPWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10, group, replyPoint);
                    }
                    break;
                case "COLOR_DATA":
                    if (deviceMode == 1 || deviceMode == 2) {
                        IEnumerable<string> colorData = ByteUtils.SplitHex(string.Join("", payloadString), 6); // Swap this with payload
                        int lightCount = 0;
                        foreach (string colorValue in colorData) {
                            colors[lightCount] = colorValue;
                            lightCount++;
                            if (lightCount > 11) {
                                break;
                            }
                        }
                    }
                    break;
                case "DEVICE_DISCOVERY":
                    IPEndPoint target = receivedIpEndPoint;
                    if (flag == "30" && from != "0.0.0.0") {
                        SendDeviceStatus(replyPoint);
                    } else if (flag == "60") {
                        if (msgDevice != null) {
                            string dsIpCheck = DreamData.GetItem("dsIp");
                            if (dsIpCheck == "0.0.0.0" && msgDevice.Tag.Contains("DreamScreen")) {
                                Console.WriteLine("No DS IP Set, setting.");
                                DreamData.SetItem("dsIp", from);
                                targetEndpoint = replyPoint;
                            }
                            if (searching) {
                                devices.Add(msgDevice);
                            }
                        }
                    }
                    break;
                case "GROUP_NAME":
                    string gName = System.Text.Encoding.ASCII.GetString(payload);
                    if (writeState) {
                        dss.GroupName = gName;
                    }

                    break;
                case "GROUP_NUMBER":
                    int gNum = payload[0];
                    if (writeState) {
                        dss.GroupNumber = gNum;
                    }

                    break;
                case "NAME":
                    string dName = System.Text.Encoding.ASCII.GetString(payload);
                    if (writeState) {
                        dss.Name = dName;
                    }

                    break;
                case "BRIGHTNESS":
                    Brightness = payload[0];
                    if (writeState) {
                        Console.WriteLine("Setting brightness to " + Brightness);
                        dss.Brightness = payload[0];
                    }

                    break;
                case "SATURATION":
                    if (writeState) {
                        dss.Saturation = Array.ConvertAll(payload, c => (int)c);
                        Saturation = dss.Saturation;
                    }

                    break;
                case "MODE":
                    if (writeState) {
                        dss.Mode = payload[0];
                        UpdateMode(payload[0]);
                    }

                    break;
                case "AMBIENT_MODE_TYPE":
                    if (writeState) {
                        dss.AmbientModeType = payload[0];
                    }

                    break;
                case "AMBIENT_COLOR":
                    if (writeState) {
                        dss.AmbientColor = Array.ConvertAll(payload, c => (int)c);
                        SetAmbientColor(dss.AmbientColor);
                    }

                    break;
            }

            if (writeState) {
                SaveDeviceState(dss);
            }
            // Restart listening for udp data packages
            if (listening) {
                c.BeginReceive(DataReceived, ar.AsyncState);
            } else {
                Console.WriteLine("DreamScreen: Closing listening loop.");
                c.Close();
            }
        }

        public void SendDeviceStatus(IPEndPoint src) {
            BaseDevice dss = GetDeviceData();
            byte[] payload = dss.EncodeState();
            DreamSender.SendUDPWrite(0x01, 0x0A, payload, 0x60, group, src);
        }


        public async Task<List<BaseDevice>> FindDevices() {
            searching = true;
            devices = new List<BaseDevice>();
            byte[] msg = new byte[] { 0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A };
            DreamSender.SendUDPBroadcast(msg);
            Stopwatch s = new Stopwatch();
            s.Start();
            while (s.Elapsed < TimeSpan.FromSeconds(3)) {

            }
            devices = devices.Distinct().ToList();
            s.Stop();
            searching = false;
            return devices;
        }

        public void Dispose() {
        }
    }
}
