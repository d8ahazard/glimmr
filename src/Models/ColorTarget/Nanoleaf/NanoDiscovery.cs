using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
    public class NanoDiscovery {
        private readonly MulticastService _mDns;

        public NanoDiscovery(ControlService cs) {
            var sd = new ServiceDiscovery();
            _mDns = cs.MulticastService;
            _mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_nanoleafapi._tcp");
            };
            
            sd.ServiceDiscovered += (s, serviceName) => { _mDns.SendQuery(serviceName, type: DnsType.PTR); };
            sd.ServiceInstanceDiscovered += ParseInstance;
        }
        
        public async Task Discover(CancellationToken ct) {
            _mDns.Start();
            Log.Debug("Nano: Discovery started...");
            while (!ct.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }
            _mDns.Stop();
            Log.Debug("Nano: Discovery Complete.");
        }

        private static void ParseInstance(object o, ServiceInstanceDiscoveryEventArgs e) {
            var name = e.ServiceInstanceName.ToString();
                var nData = new NanoleafData {IpAddress = string.Empty};
                if (!name.Contains("nanoleafapi", StringComparison.InvariantCulture)) return;
                foreach (var msg in e.Message.AdditionalRecords) {
                    switch (msg.Type) {
                        case DnsType.A:
                            var aString = msg.ToString();
                            var aValues = aString.Split(" ");
                            nData.IpAddress = aValues[4];
                            nData.Name = aValues[0].Split(".")[0];
                            break;
                        case DnsType.TXT:
                            var txtString = msg.ToString();
                            var txtValues = txtString.Split(" ");
                            nData.Version = txtValues[5]
                                .Replace("srcvers=", string.Empty, StringComparison.InvariantCulture);
                            nData.Type = txtValues[4].Replace("md=", string.Empty, StringComparison.InvariantCulture);
                            nData.Id = txtValues[3].Replace("id=", string.Empty, StringComparison.InvariantCulture);
                            break;
                        case DnsType.AAAA:
                            var mString = msg.ToString();
                            var mValues = mString.Split(" ");
                            nData.IpV6Address = mValues[4];
                            // Remove rest of FQDN
                            nData.Name = mValues[0].Split(".")[0];
                            break;
                        case DnsType.SRV:
                            var sString = msg.ToString();
                            var sValues = sString.Split(" ");
                            nData.Port = int.Parse(sValues[6], CultureInfo.InvariantCulture);
                            nData.Hostname = sValues[7];
                            break;
                    }
                }

                if (string.IsNullOrEmpty(nData.IpAddress) && !string.IsNullOrEmpty(nData.Hostname)) {
                    nData.IpAddress = nData.Hostname;
                    
                }

                if (!string.IsNullOrEmpty(nData.IpAddress) && !string.IsNullOrEmpty(nData.Id)) {
                    var existing = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nanoleaf", nData.Id);
                    if (existing != null) {
                        nData.CopyExisting(existing);
                    }
                    if (nData.Token == null) {
                        Log.Debug("NO TOKEN!");
                        return;
                    }
                    using var nl = new NanoleafDevice(nData, new HttpClient());
                    var layout = nl.GetLayout().Result;
                    if (layout != null) {
                        nData.MergeLayout(layout);
                    } else {
                        Log.Debug("Layout is null.");
                    }
                    DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", nData).ConfigureAwait(false);
                }
            
        }
    }
}