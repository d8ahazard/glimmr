﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

namespace Glimmr.Models.ColorTarget.Wled {
    public class WledDiscovery : ColorDiscovery, IColorDiscovery {

        private MulticastService _mDns;
        private bool _discovering;
        private bool _stopDiscovery;
        public WledDiscovery(ControlService cs) : base(cs) {
            _mDns = cs.MulticastService;
            var sd = new ServiceDiscovery(_mDns);
            _mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_wled._tcp");
            };

            sd.ServiceDiscovered += (s, serviceName) => {
                _mDns.SendQuery(serviceName, type: DnsType.PTR);
            };

            sd.ServiceInstanceDiscovered += WledDiscovered;
            DeviceTag = "Wled";
        }
        
        public async Task Discover(CancellationToken ct) {
            Log.Debug("WLED: Discovery started...");
            
            _mDns.Start();
            _discovering = true;
            while (!ct.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }
            _stopDiscovery = true;
            while (_discovering) {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }
            _mDns.Stop();
            Log.Debug("WLED: Discovery complete.");
        }

        private void WledDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
            var name = e.ServiceInstanceName.ToString();
            if (!name.Contains("wled", StringComparison.InvariantCulture)) return;
            var rr = e.Message.AdditionalRecords;
            
            foreach (var id in from msg in rr where msg.Type == DnsType.TXT select msg.CanonicalName.Split(".")[0]) {
                var nData = new WledData(id);
                var existing = DataUtil.GetCollectionItem<WledData>("Dev_Wled", nData.Id);
                if (existing != null) {
                    nData.CopyExisting(existing);
                }
                DataUtil.InsertCollection<WledData>("Dev_Wled", nData).ConfigureAwait(false);
            }

            if (_stopDiscovery) _discovering = false;
        }
    }
}