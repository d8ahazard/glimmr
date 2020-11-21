using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace Glimmr.Hubs {
    
    public class SocketServer : Hub {
        public int UserCount;
        private CancellationTokenSource _ct;
        private bool _initialized;
        private bool timerStarted;

        private ControlService _cs;
        //private readonly IHubContext<SocketServer> _hubContext;
        
        
        public SocketServer(ControlService cs) {
            _cs = cs;
        }
       
        public Task Mode(int mode) {
            LogUtil.Write($"WS Mode: {mode}");
            _cs.SetMode(mode);
            return Clients.All.SendAsync("mode", mode);
        }
       
        public Task Action(string action, string value) {
            LogUtil.Write($"WS Action: {action}: " + JsonConvert.SerializeObject(value));
            return Clients.Caller.SendAsync("ack", true);
        }

        public Task ScanDevices() {
            LogUtil.Write("Scan called from socket!");
            _cs.ScanDevices();
            return Task.CompletedTask;
        }

        private CpuData GetStats(CancellationToken token) {
            return CpuUtil.GetStats();
        }

        public async void AuthorizeHue(string id) {
            LogUtil.Write("AuthHue called, for real (socket): " + id);
            BridgeData bd;
            if (!string.IsNullOrEmpty(id)) {
                await Clients.All.SendAsync("hueAuth", "start");
                bd = DataUtil.GetCollectionItem<BridgeData>("Dev_Hue", id);
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
                try {
                    var appKey = HueDiscovery.CheckAuth(bd.IpAddress).Result;
                    LogUtil.Write("Appkey retrieved! " + JsonConvert.SerializeObject(appKey));
                    if (appKey != null) {
                        if (!string.IsNullOrEmpty(appKey.StreamingClientKey)) {
                            LogUtil.Write("Updating bridge?");
                            bd.Key = appKey.StreamingClientKey;
                            bd.User = appKey.Username;
                            LogUtil.Write("Creating new bridge...");
                            // Need to grab light group stuff here
                            var nhb = new HueBridge(bd);
                            bd = nhb.RefreshData(5).Result;
                            nhb.Dispose();
                            DataUtil.InsertCollection<BridgeData>("Dev_Hue", bd);
                            await Clients.All.SendAsync("hueAuth", "authorized");
                            await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
                            return;
                        }
                        LogUtil.Write("Appkey is null?");
                    }

                    LogUtil.Write("Waiting for app key.");
                } catch (NullReferenceException e) {
                    LogUtil.Write("NULL EXCEPTION: " + e.Message, "WARN");
                }
                await Clients.All.SendAsync("hueAuth", count);
                Thread.Sleep(1000);
            }
            LogUtil.Write("We should be authorized, returning.");
        }

        public async void AuthorizeNano(string id) {
            var leaf = DataUtil.GetCollectionItem<NanoData>("Dev_Nano", id);
            bool doAuth = leaf.Token == null;
            if (doAuth) {
                await Clients.All.SendAsync("nanoAuth", "authorized");
                await Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
                return;
            }
            var panel = new NanoGroup(id);
            var count = 0;
            while (count < 30) {
                var appKey = panel.CheckAuth().Result;
                if (appKey != null) {
                    leaf.Token = appKey.Token;
                    DataUtil.InsertCollection<NanoData>("Dev_NanoLeaf", leaf);
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
        
        public override Task OnDisconnectedAsync(Exception exception) {
            var dc = base.OnDisconnectedAsync(exception);
            UserCount--;
            LogUtil.Write("Disconnected: Users " + UserCount);
            return dc;
        }

        public override Task OnConnectedAsync() {
            var bc = base.OnConnectedAsync();
            UserCount++;
            LogUtil.Write("User Connected: " + UserCount);
            return bc;
        }
        
        public Task ThrowException() {
            throw new HubException("Is this better: ");
        }

    }
}