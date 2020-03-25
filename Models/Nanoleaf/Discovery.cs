using System;
using System.Collections.Generic;
using System.Diagnostics;
using HueDream.Models.Util;
using Makaretu.Dns;
using Newtonsoft.Json;

namespace HueDream.Models.Nanoleaf {
    public static class Discovery {
        public static List<NanoData> Discover(int timeout = 5) {
            var output = new List<NanoData>();
            using (var mDns = new MulticastService()) {
                using (var sd = new ServiceDiscovery(mDns)) {

                    mDns.NetworkInterfaceDiscovered += (s, e) => {
                        foreach (var nic in e.NetworkInterfaces) {
                            LogUtil.Write($"NIC '{nic.Name}'");
                        }

                        // Ask for the name of all services.
                        sd.QueryServiceInstances("_nanoleafapi._tcp");
                        //sd.QueryAllServices();
                    };

                    sd.ServiceDiscovered += (s, serviceName) => {
                        LogUtil.Write($"service '{serviceName}'");
                        mDns.SendQuery(serviceName, type: DnsType.PTR);
                    };

                    sd.ServiceInstanceDiscovered += (s, e) => {
                        var name = e.ServiceInstanceName.ToString();
                        var nData = new NanoData();
                        nData.IpV4Address = String.Empty;
                        if (!name.Contains("nanoleafapi")) return;
                        foreach (var msg in e.Message.AdditionalRecords) {
                            switch (msg.Type) {
                                case DnsType.A:
                                    var aString = msg.ToString();
                                    LogUtil.Write("Arecord found: " + aString);
                                    var aValues = aString.Split(" ");
                                    nData.IpV4Address = aValues[4];
                                    nData.Name = aValues[0].Split(".")[0];
                                    break;
                                case DnsType.TXT:
                                    var txtString = msg.ToString();
                                    LogUtil.Write("TXT Record found: " + txtString);
                                    var txtValues = txtString.Split(" ");
                                    nData.Version = txtValues[5].Replace("srcvers=", string.Empty);
                                    nData.Type = txtValues[4].Replace("md=", string.Empty);
                                    nData.Id = txtValues[3].Replace("id=", string.Empty);
                                    break;
                                case DnsType.AAAA:
                                    var mString = msg.ToString();
                                    LogUtil.Write("AAA Record Found: " + msg);
                                    var mValues = mString.Split(" ");
                                    nData.IpV6Address = mValues[4];
                                    // Remove rest of FQDN
                                    nData.Name = mValues[0].Split(".")[0];
                                    break;
                                case DnsType.SRV:
                                    var sString = msg.ToString();
                                    LogUtil.Write("SRV Record Found: " + msg);
                                    var sValues = sString.Split(" ");
                                    nData.Port = int.Parse(sValues[6]);
                                    nData.Hostname = sValues[7];
                                    break;
                                default:
                                    LogUtil.Write($"{msg.Type} record: " + msg);
                                    break;
                            }
                        }

                        if (string.IsNullOrEmpty(nData.IpV4Address) && !string.IsNullOrEmpty(nData.Hostname)) {
                            nData.IpV4Address = nData.Hostname;
                        }


                        if (!string.IsNullOrEmpty(nData.IpV4Address) && !string.IsNullOrEmpty(nData.Id)) {
                            output.Add(nData);
                        }

                        LogUtil.Write(
                            $"service instance '{e.ServiceInstanceName}'" + JsonConvert.SerializeObject(nData));
                    };


                    var s = new Stopwatch();
                    s.Start();
                    mDns.Start();
                    LogUtil.Write("Discovery Started.");
                    while (s.Elapsed < TimeSpan.FromSeconds(timeout)) {
                        //LogUtil.Write("Looping: " + s.Elapsed);
                    }

                    LogUtil.Write("Discovery stopped.");
                    s.Stop();
                    mDns.Stop();
                    return output;
                }
            }
        }

        public static List<NanoData> Refresh(int timeout = 5) {
            var existingLeaves = DreamData.GetItem<List<NanoData>>("leaves");
            var leaves = Discover(timeout);
            var nanoLeaves = new List<NanoData>();

            if (existingLeaves != null) {
                LogUtil.Write("Adding range.");
                foreach (var newLeaf in leaves) {
                    var add = true;
                    var exInt = 0;
                    foreach (var leaf in existingLeaves) {
                        if (leaf.Id == newLeaf.Id) {
                            LogUtil.Write("Updating existing leaf.");
                            newLeaf.Token = leaf.Token;
                            newLeaf.X = leaf.X;
                            newLeaf.Y = leaf.Y;
                            newLeaf.Scale = 1;
                            newLeaf.Rotation = leaf.Rotation;
                            newLeaf.Layout = MergeLayouts(leaf.Layout, newLeaf.Layout);
                            existingLeaves[exInt] = newLeaf;
                            add = false;
                            break;
                        }

                        exInt++;
                    }

                    if (add) {
                        LogUtil.Write("Adding new leaf.");
                        nanoLeaves.Add(newLeaf);
                    }
                }

                nanoLeaves.AddRange(existingLeaves);
            } else {
                nanoLeaves.AddRange(leaves);
            }

            LogUtil.Write("Looping: " + nanoLeaves.Count);
            foreach (var leaf in nanoLeaves) {
                if (leaf.Token != null) {
                    LogUtil.Write("Fetching leaf data.");
                    try {
                        var nl = new Panel(leaf.IpV4Address, leaf.Token);
                        var layout = nl.GetLayout().Result;
                        if (layout != null) leaf.Layout = layout;
                        leaf.Scale = 1;
                    } catch (Exception) {
                        LogUtil.Write("An exception occurred, probably the nanoleaf is unplugged.");
                    }

                    Console.WriteLine("Device: " + JsonConvert.SerializeObject(leaf));
                }
            }
            LogUtil.Write("Returning from refresh: " + JsonConvert.SerializeObject(nanoLeaves));
            DreamData.SetItem<List<NanoData>>("leaves", nanoLeaves);
            return nanoLeaves;
        }

        private static NanoLayout MergeLayouts(NanoLayout source, NanoLayout dest) {
            var output = new NanoLayout();
            output.PositionData = new List<PanelLayout>();
            if (source != null && dest != null) {
                foreach (PanelLayout s in source.PositionData) {
                    var sId = s.PanelId;
                    foreach (PanelLayout d in dest.PositionData) {
                        if (d.PanelId == sId) {
                            d.Sector = s.Sector;
                            output.PositionData.Add(d);
                        }
                    }
                }    
            }
            
            return output;
        }
    }
}
