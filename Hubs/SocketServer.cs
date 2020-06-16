using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Q42.HueApi.Models.Bridge;
using Serilog;

namespace HueDream.Hubs {
    public class SocketServer : Hub {
        public async Task SendMessage(string message) {
            await Clients.All.SendAsync("ReceiveMessage", message);
            LogUtil.Write("Sending message: " + message);
        }

        public void SetMode(string id, int mode) {
            LogUtil.Write($"WS Mode: {id}, {mode}");
        }

        public async void AuthorizeHue(string id) {
            LogUtil.Write("AuthHue called, for real.");
            BridgeData bd;
            if (!string.IsNullOrEmpty(id)) {
                await Clients.All.SendAsync("hueAuth", "start");
                bd = DataUtil.GetCollectionItem<BridgeData>("bridges", id);
                LogUtil.Write("BD: " + JsonConvert.SerializeObject(bd));
                if (bd == null) {
                    LogUtil.Write("Null bridge retrieved.");
                    await Clients.All.SendAsync("hueAuth", "stop");
                    return;
                }

                if (bd.Key != null && bd.User != null) {
                    LogUtil.Write("Bridge is already authorized.");
                    await Clients.All.SendAsync("hueAuth", "authorized");
                    await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
                    return;
                }
            } else {
                LogUtil.Write("Null value.", "WARN");
                await Clients.All.SendAsync("hueAuth", "stop");
                return;
            }

            LogUtil.Write("Trying to retrieve appkey...");
            var count = 0;
            while (count < 30) {
                count++;
                RegisterEntertainmentResult appKey = HueDiscovery.CheckAuth(bd.IpAddress).Result;
                if (!string.IsNullOrEmpty(appKey.StreamingClientKey)) {
                    bd.Key = appKey.StreamingClientKey;
                    bd.User = appKey.Username;
                    DataUtil.InsertCollection<BridgeData>("bridges", bd);
                    await Clients.All.SendAsync("hueAuth", "authorized");
                    await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
                    return;
                }
                await Clients.All.SendAsync("hueAuth", count);
                Thread.Sleep(1000);
            }
            LogUtil.Write("We should be authorized, returning.");
        }

        public async void AuthorizeNano(string id) {
            var leaves = DataUtil.GetItem<List<NanoData>>("leaves");
            NanoData bd = null;
            var nanoInt = -1;
            if (!string.IsNullOrEmpty(id)) {
                var nanoCount = 0;
                foreach (var n in leaves) {
                    if (n.IpV4Address == id) {
                        bd = n;
                        bool doAuth = n.Token == null;
                        if (doAuth) {
                            await Clients.All.SendAsync("nanoAuth", "authorized");
                            await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
                            return;
                        }
                        nanoInt = nanoCount;
                    }
                    nanoCount++;
                }
            }
            
            var panel = new NanoGroup(id);
            var count = 0;
            while (count < 30) {
                var appKey = panel.CheckAuth().Result;
                if (appKey != null && bd != null) {
                    bd.Token = appKey.Token;
                    leaves[nanoInt] = bd;
                    DataUtil.SetItem("leaves", leaves);
                    await Clients.All.SendAsync("nanoAuth", "authorized");
                    await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
                    panel.Dispose();
                    return;
                }
                await Clients.All.SendAsync("nanoAuth", count);
                Thread.Sleep(1000);
                count++;
            }
            await Clients.All.SendAsync("nanoAuth", "stop");

            panel.Dispose();
        }

        public void SendData(string command) {
        }

        public override Task OnDisconnectedAsync(Exception exception) {
            LogUtil.Write("User disconnected");
            return base.OnDisconnectedAsync(exception);
        }

            
    }
}