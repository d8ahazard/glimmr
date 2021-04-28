using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Nanoleaf.Client;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
    public class NanoleafDiscovery : ColorDiscovery, IColorDiscovery, IColorTargetAuth {
        private readonly MulticastService _mDns;
        private readonly ServiceDiscovery _sd;
        private readonly ControlService _controlService;

        public NanoleafDiscovery(ColorService cs) : base(cs) {
            _controlService = cs.ControlService;
            _mDns = _controlService.MulticastService;
            _sd = _controlService.ServiceDiscovery;
            DeviceTag = "Nanoleaf";
        }

        private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
            _sd.QueryServiceInstances("_nanoleafapi._tcp");
        }

        private void ServiceDiscovered(object? sender, DomainName serviceName) {
            _mDns.SendQuery(serviceName, type: DnsType.PTR);
        }

        public async Task Discover(CancellationToken ct, int timeout) {
            _mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
            _sd.ServiceDiscovered += ServiceDiscovered;
            _sd.ServiceInstanceDiscovered += NanoleafDiscovered;
            _mDns.Start();
            Log.Debug("Nano: Discovery started...");
            await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
            _mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
            _sd.ServiceDiscovered -= ServiceDiscovered;
            _sd.ServiceInstanceDiscovered -= NanoleafDiscovered;
            //_mDns.Stop();
            Log.Debug("Nano: Discovery complete.");
        }

        private void NanoleafDiscovered(object o, ServiceInstanceDiscoveryEventArgs e) {
            var name = e.ServiceInstanceName.ToString();
            var nData = new NanoleafData {IpAddress = string.Empty};
            if (!name.Contains("nanoleafapi", StringComparison.InvariantCulture)) return;
            foreach (var msg in e.Message.AdditionalRecords) {
                try {
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
                } catch (Exception f) {
                    Log.Debug("Exception caught: " + f.Message);
                }
                
            }

            if (string.IsNullOrEmpty(nData.IpAddress) && !string.IsNullOrEmpty(nData.Hostname)) {
                nData.IpAddress = nData.Hostname;
            }

            NanoleafData ex = DataUtil.GetDevice(nData.Id);
            if (!string.IsNullOrEmpty(ex.Token)) {
                nData.Token = ex.Token;
                try {
                    var nd = new NanoleafDevice(nData, _controlService.ColorService);
                    var layout = nd.GetLayout().Result;
                    nd.Dispose();
                    nData.Layout = layout;
                } catch (Exception f) {
                    Log.Debug("Exception: " + e.Message);
                }
            }
            _controlService.AddDevice(nData).ConfigureAwait(true);

        }

        public override string DeviceTag { get; set; }

        public async Task<dynamic> CheckAuthAsync(dynamic deviceData) {
            var nanoleaf = new NanoleafClient(deviceData.IpAddress, "");
            try {
                var result = await nanoleaf.CreateTokenAsync();
                Log.Debug("Authorized.");
                if (!string.IsNullOrEmpty(result.Token)) {
                    deviceData.Token = result.Token;
                    nanoleaf.Dispose();
                    nanoleaf = new NanoleafClient(deviceData.IpAddress, result.Token);
                    var layout = await nanoleaf.GetLayoutAsync();
                    deviceData.Layout = new TileLayout(layout);
                    Log.Debug("New nano info: " + JsonConvert.SerializeObject(deviceData));
                }
            } catch (AggregateException e) {
                Log.Debug("Unauthorized Exception: " + e.Message);
            }

            nanoleaf.Dispose();
            return deviceData;
        }
    }
}