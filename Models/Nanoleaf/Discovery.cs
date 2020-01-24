using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Makaretu.Dns;
using Newtonsoft.Json;

namespace HueDream.Models.Nanoleaf {
    public static class Discovery {
        public static List<NanoData> Discover(int timeout = 5) {
            var output = new List<NanoData>();
            var devices = new List<NanoData>();
            using (var mdns = new MulticastService()) {
                var sd = new ServiceDiscovery(mdns);

                mdns.NetworkInterfaceDiscovered += (s, e) =>
                {
                    foreach (var nic in e.NetworkInterfaces) {
                        Console.WriteLine($"NIC '{nic.Name}'");
                    }

                    // Ask for the name of all services.
                    sd.QueryServiceInstances("_nanoleafapi._tcp");
                    //sd.QueryAllServices();
                };

                sd.ServiceDiscovered += (s, serviceName) =>
                {
                    Console.WriteLine($"service '{serviceName}'");
                    mdns.SendQuery(serviceName, type: DnsType.PTR);
                };

                sd.ServiceInstanceDiscovered += (s, e) =>
                {
                    string name = e.ServiceInstanceName.ToString();
                    var nData = new NanoData();
                    if (name.Contains("nanoleafapi")) {
                        foreach (var msg in e.Message.AdditionalRecords) {
                            switch(msg.Type) {
                                case DnsType.A:
                                    var aString = msg.ToString();
                                    Console.WriteLine("Arecord found: " + aString);                                    
                                    var aValues = aString.Split(" ");
                                    nData.IpV4Address = aValues[4];
                                    break;
                                case DnsType.TXT:
                                    var txtString = msg.ToString();
                                    Console.WriteLine("TXT Record found: " + txtString);
                                    var txtValues = txtString.Split(" ");
                                    nData.Version = txtValues[5].Replace("srcvers=", string.Empty);
                                    nData.Type = txtValues[4].Replace("md=", string.Empty);
                                    nData.Id = txtValues[3].Replace("id=", string.Empty);
                                    break;
                                case DnsType.AAAA:                                    
                                    var mString = msg.ToString();
                                    Console.WriteLine("AAA Record Found: " + msg);
                                    var mValues = mString.Split(" ");
                                    nData.IpV6Address = mValues[4];
                                    break;
                                case DnsType.SRV:
                                    var sString = msg.ToString();
                                    Console.WriteLine("SRV Record Found: " + msg);
                                    var sValues = sString.Split(" ");
                                    nData.Port = int.Parse(sValues[6]);
                                    nData.Hostname = sValues[7];
                                    break;
                                default:
                                    Console.WriteLine($"{msg.Type} record: " + msg.ToString());
                                    break;
                            }                            
                        }
                        if (nData.IpV4Address != string.Empty && nData.Id != string.Empty) {
                            output.Add(nData);
                        }
                        Console.WriteLine($"service instance '{e.ServiceInstanceName}'" + JsonConvert.SerializeObject(nData));
                    }
                };

                
                Stopwatch s = new Stopwatch();
                s.Start();
                mdns.Start();
                LogUtil.Write("Discovery Started.");
                while (s.Elapsed < TimeSpan.FromSeconds(timeout)) {
                    //LogUtil.Write("Looping: " + s.Elapsed);
                }
                LogUtil.Write("Discovery stopped.");
                s.Stop();
                mdns.Stop();
                return output;
            }
        }
    }
}
