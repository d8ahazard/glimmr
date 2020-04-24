using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HueDream.Models.Util;
using Makaretu.Dns;

namespace HueDream.Models.Nanoleaf {
    public static class NanoDiscovery {
        public static List<NanoData> Discover(int timeout = 5) {
            var output = new List<NanoData>();
            using (var mDns = new MulticastService()) {
                using (var sd = new ServiceDiscovery(mDns)) {
                    mDns.NetworkInterfaceDiscovered += (s, e) => {
                        // Ask for the name of all services.
                        sd.QueryServiceInstances("_nanoleafapi._tcp");
                    };

                    sd.ServiceDiscovered += (s, serviceName) => {
                        mDns.SendQuery(serviceName, type: DnsType.PTR);
                    };

                    sd.ServiceInstanceDiscovered += (s, e) => {
                        var name = e.ServiceInstanceName.ToString();
                        var nData = new NanoData {IpV4Address = string.Empty};
                        if (!name.Contains("nanoleafapi", StringComparison.InvariantCulture)) return;
                        foreach (var msg in e.Message.AdditionalRecords) {
                            switch (msg.Type) {
                                case DnsType.A:
                                    var aString = msg.ToString();
                                    var aValues = aString.Split(" ");
                                    nData.IpV4Address = aValues[4];
                                    nData.Name = aValues[0].Split(".")[0];
                                    break;
                                case DnsType.TXT:
                                    var txtString = msg.ToString();
                                    var txtValues = txtString.Split(" ");
                                    nData.Version = txtValues[5].Replace("srcvers=", string.Empty, StringComparison.InvariantCulture);
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

                        if (string.IsNullOrEmpty(nData.IpV4Address) && !string.IsNullOrEmpty(nData.Hostname)) {
                            nData.IpV4Address = nData.Hostname;
                        }

                        if (!string.IsNullOrEmpty(nData.IpV4Address) && !string.IsNullOrEmpty(nData.Id)) {
                            output.Add(nData);
                        }
                    };


                    var s = new Stopwatch();
                    s.Start();
                    mDns.Start();
                    LogUtil.Write("Discovery Started.");
                    while (s.Elapsed < TimeSpan.FromSeconds(timeout)) {
                        //LogUtil.Write("Looping: " + s.Elapsed);
                    }

                    s.Stop();
                    mDns.Stop();
                    LogUtil.Write($"Discovery complete, found {output.Count} devices.");
                    return output;
                }
            }
        }

        public static List<NanoData> Refresh(int timeout = 5) {
            var existingLeaves = DataUtil.GetItem<List<NanoData>>("leaves");
            var leaves = Discover(timeout);
            var nanoLeaves = new List<NanoData>();

            if (existingLeaves != null) {
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
                        nanoLeaves.Add(newLeaf);
                    }
                }

                nanoLeaves.AddRange(existingLeaves);
            } else {
                nanoLeaves.AddRange(leaves);
            }

            foreach (var leaf in nanoLeaves) {
                if (leaf.Token != null) {
                    try {
                        using var nl = new Panel(leaf.IpV4Address, leaf.Token);
                        var layout = nl.GetLayout().Result;
                        if (layout != null) leaf.Layout = layout;
                        leaf.Scale = 1;
                    } catch (Exception) {
                        LogUtil.Write("An exception occurred, probably the nanoleaf is unplugged.");
                    }
                }
            }
            DataUtil.SetItem<List<NanoData>>("leaves", nanoLeaves);
            return nanoLeaves;
        }
       

        private static NanoLayout MergeLayouts(NanoLayout source, NanoLayout dest) {
            var output = new NanoLayout {PositionData = new List<PanelLayout>()};
            if (source == null || dest == null) return output;
            foreach (var s in source.PositionData) {
                var sId = s.PanelId;
                foreach (var d in dest.PositionData.Where(d => d.PanelId == sId)) {
                    d.Sector = s.Sector;
                    output.PositionData.Add(d);
                }
            }

            return output;
        }
    }
}
