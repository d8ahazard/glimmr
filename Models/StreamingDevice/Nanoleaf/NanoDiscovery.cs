using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Makaretu.Dns;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.Nanoleaf {
    public static class NanoDiscovery {
        public static async Task<List<NanoData>> Discover(int timeout = 5) {
            var output = new List<NanoData>();
            var mDns = new MulticastService();
            var sd = new ServiceDiscovery(mDns);
            mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_nanoleafapi._tcp");
            };

            sd.ServiceDiscovered += (s, serviceName) => { mDns.SendQuery(serviceName, type: DnsType.PTR); };

            sd.ServiceInstanceDiscovered += (s, e) => {
                var name = e.ServiceInstanceName.ToString();
                var nData = new NanoData {IpAddress = string.Empty};
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
                    output.Add(nData);
                }
            };

            mDns.Start();
            LogUtil.Write("Nano: Discovery Started.");
            await Task.Delay(timeout * 1000).ConfigureAwait(false);
            mDns.Stop();
            sd.Dispose();
            mDns.Dispose();
            LogUtil.Write($"Nano: Discovery complete, found {output.Count} devices.");
            return output;
        }

        public static async Task<List<NanoData>> Refresh(CancellationToken ct) {
            var foo = Task.Run(() => Discover(), ct);
            var output = new List<NanoData>();
            var newLeaves = await foo;
            foreach (var nl in newLeaves) {
                var cp = nl;
                var ex = DataUtil.GetCollectionItem<NanoData>("leaves", nl.Id);
                if (ex != null) {
                    cp = NanoData.CopyExisting(nl, ex);
                }
                output.Add(cp);
            }
            return output;
        }
    }
}