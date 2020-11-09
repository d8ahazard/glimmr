using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Makaretu.Dns;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.WLed {
    public static class WledDiscovery {
        
        public static async Task<List<WLedData>> Discover(int timeout = 5) {
            var output = new List<WLedData>();
            var existing = new List<WLedData>();
            try {
                existing = DataUtil.GetCollection<WLedData>("Dev_Wled");
            } catch (Exception e) {
                LogUtil.Write("No Led data...");
            }

            if (existing == null) {
                existing = new List<WLedData>();
            }

            var mDns = new MulticastService();
            var sd = new ServiceDiscovery(mDns);
            mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_wled._tcp");
            };

            sd.ServiceDiscovered += (s, serviceName) => {
                mDns.SendQuery(serviceName, type: DnsType.PTR);
            };

            sd.ServiceInstanceDiscovered += (s, e) => {
                var name = e.ServiceInstanceName.ToString();
                
                
				if (!name.Contains("wled", StringComparison.InvariantCulture)) return;
                var rr = e.Message.AdditionalRecords;
                
                foreach (var id in from msg in rr where msg.Type == DnsType.TXT select msg.CanonicalName.Split(".")[0]) {
                    var nData = new WLedData(id);
                    foreach (var ee in existing) {
                        if (ee.Id == nData.Id) {
                            nData.CopyExisting(ee);
                        }
                    }
                    LogUtil.Write("We should be inserting here: " + JsonConvert.SerializeObject(nData));
                    DataUtil.InsertCollection<WLedData>("Dev_Wled", nData);
                }
            };

            mDns.Start();
            LogUtil.Write("WLED: Discovery Started.");
            await Task.Delay(timeout * 1000);
            mDns.Stop();
            sd.Dispose();
            mDns.Dispose();
            LogUtil.Write($"WLED: Discovery complete, found {output.Count} devices.");
            return output;
        }
    }
}