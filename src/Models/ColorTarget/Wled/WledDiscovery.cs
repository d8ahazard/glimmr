using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Makaretu.Dns;
using Serilog;

namespace Glimmr.Models.ColorTarget.Wled {
    public static class WledDiscovery {
        private static List<WledData> _discovered;
        
        public static async Task Discover(int timeout = 5) {
            Log.Debug("WLED: Discovery started...");
            var mDns = new MulticastService();
            var sd = new ServiceDiscovery(mDns);
            mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_wled._tcp");
            };

            sd.ServiceDiscovered += (s, serviceName) => {
                mDns.SendQuery(serviceName, type: DnsType.PTR);
            };

            sd.ServiceInstanceDiscovered += WledDiscovered;

            mDns.Start();
            await Task.Delay(timeout * 1000);
            mDns.Stop();
            sd.Dispose();
            mDns.Dispose();
            Log.Debug("WLED: Discovery complete.");
        }

        private static void WledDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
            var name = e.ServiceInstanceName.ToString();
            if (!name.Contains("wled", StringComparison.InvariantCulture)) return;
            var rr = e.Message.AdditionalRecords;
                
            foreach (var id in from msg in rr where msg.Type == DnsType.TXT select msg.CanonicalName.Split(".")[0]) {
                var nData = new WledData(id);
                var existing = DataUtil.GetCollectionItem<WledData>("Dev_Wled", nData.Id);
                if (existing != null) {
                    nData.CopyExisting(existing);
                }
                DataUtil.InsertCollection<WledData>("Dev_Wled", nData);
            }
        }
    }
}