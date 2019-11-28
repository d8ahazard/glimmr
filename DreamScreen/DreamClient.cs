using HueDream.DreamScreen.Devices;
using HueDream.HueDream;
using HueDream.Util;
using JsonFlatFileDataStore;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace HueDream.DreamScreen {
    internal class DreamClient {

        public string[] colors { get; }
        public static bool listening { get; set; }
        public static bool subscribed { get; set; }
        private static readonly int Port = 8888;
        private static bool searching = false;
        public static List<BaseDevice> devices { get; set; }
        public int deviceMode { get; set; }

        private readonly DreamSync dreamSync;
        private IPEndPoint targetEndpoint;
        private IPEndPoint listenEndPoint;
        private UdpClient listener;
        private readonly Socket sender;
        private readonly string sourceIp;
        private readonly byte group;


        public DreamClient(DreamSync ds) {
            DataStore dd = DreamData.getStore();
            BaseDevice dev = GetDeviceData();
            sourceIp = dd.GetItem("dsIp");
            group = (byte)dev.GroupNumber;
            targetEndpoint = new IPEndPoint(IPAddress.Parse(sourceIp), 8888);
            dd.Dispose();
            dreamSync = ds;
            Console.WriteLine("Still alive");
            deviceMode = dev.Mode;
            colors = new string[12];
            for (int i = 0; i < colors.Length; i++) {
                colors[i] = "FFFFFF";
            }
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        }

        private BaseDevice GetDeviceData() {
            using DataStore dd = DreamData.getStore();
            BaseDevice dev;
            string devType = dd.GetItem("emuType");
            if (devType == "SideKick") {
                dev = dd.GetItem<SideKick>("myDevice");
            } else {
                dev = dd.GetItem<Connect>("myDevice");
            }
            return dev;
        }

        public void UpdateMode(int newMode) {
            BaseDevice dev = DreamData.GetDeviceData();
            if (deviceMode != newMode) {
                deviceMode = newMode;
                dev.Mode = newMode;
                int[] aColor = dev.AmbientColor;
                DreamData.SetItem("myDevice", dev);
                Console.WriteLine("Updating mode to " + dev.Mode);
                // If ambient, set to ambient colorS   
                bool sync = (newMode != 0);
                dreamSync.CheckSync(sync);
                if (newMode == 3) {
                    string cString = aColor[0].ToString("XX");
                    cString += aColor[1].ToString("XX");
                    cString += aColor[2].ToString("XX");

                    for (int i = 0; i < colors.Length; i++) {
                        colors[i] = cString;
                    }
                    Console.WriteLine("Updating color array to use ambient color: " + colors[0]);
                }
            }
        }

        private void SaveDeviceState(BaseDevice dev) {
            DataStore dd = DreamData.getStore();
            dd.ReplaceItemAsync("myDevice", dev);
            dd.Dispose();

        }


        public void Subscribe() {
            Console.WriteLine("Subscribing to color data...");
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
                Console.WriteLine("Starting DreamScreen Upd receiving on port: " + listenEndPoint);
                // Call DataReceived() every time it gets something
                listener.BeginReceive(DataReceived, listener);
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
                Console.WriteLine("CRC Check Failed!");
                return;
            }

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
                        Console.WriteLine("Grabbing device object.");
                        dss = GetDeviceData();
                        writeState = true;
                    }
                }
            } catch (Exception e) {
                Console.WriteLine("MSG parse Exception: " + e.Message);
            }

            switch (command) {
                case "SUBSCRIBE":
                    if (deviceMode != 0) {
                        Console.WriteLine("Sending Sub reply, mode is " + deviceMode);
                        //sendUDPWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10); // Send sub response
                        DreamSender.SendUDPWrite(0x01, 0x0C, new byte[] { 0x01 }, 0x10, group, replyPoint);
                    }
                    break;
                case "COLOR_DATA":
                    if (deviceMode != 0) {
                        //Console.WriteLine("ColorData: " + payload);
                        IEnumerable<string> colorData = ByteUtils.SplitHex(string.Join("", payloadString), 6); // Swap this with payload
                        int lightCount = 0;
                        //Console.WriteLine("ColorData: " + JsonConvert.SerializeObject(colorData));
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
                                DreamData.SetItem("dsIp", from);
                                // If we haven't set a target endpoint, set it
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
                    if (writeState) {
                        dss.Brightness = payload[0];
                    }

                    break;
                case "SATURATION":
                    if (writeState) {
                        dss.Saturation = Array.ConvertAll(payload, c => (int)c);
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
                        for (int i = 0; i < colors.Length; i++) {
                            colors[i] = payloadString.Join(string.Empty);
                        }
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
            string pString = BitConverter.ToString(payload).Replace("-", string.Empty);
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


        private void requestState() {
            using (MemoryStream stream = new MemoryStream()) {
                using (BinaryWriter response = new BinaryWriter(stream)) {
                    DreamSender.SendUDPWrite(0x01, 0x0A, Array.Empty<byte>(), 0x30);
                }
            }
        }

    }
}
