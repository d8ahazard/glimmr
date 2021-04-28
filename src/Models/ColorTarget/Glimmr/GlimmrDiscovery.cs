using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorTarget.Glimmr {
    public class GlimmrDiscovery : ColorDiscovery, IColorDiscovery {

        private readonly MulticastService _mDns;
        private readonly ServiceDiscovery _sd;
        private bool _stopDiscovery;
        private readonly ControlService _controlService;
        public GlimmrDiscovery(ColorService cs) : base(cs) {
            _controlService = cs.ControlService;
            _mDns = _controlService.MulticastService;
            _sd = _controlService.ServiceDiscovery;
        }

        private void ServiceDiscovered(object? sender, DomainName serviceName) {
            if (!_stopDiscovery) _mDns.SendQuery(serviceName, type: DnsType.PTR);
        }

        private void InterfaceDiscovered(object? sender, NetworkInterfaceEventArgs e) {
            _sd.QueryServiceInstances("_glimmr._tcp");
        }

        public async Task Discover(CancellationToken ct, int timeout) {
            Log.Debug("Glimmr: Discovery started...");

            try {
                _mDns.NetworkInterfaceDiscovered += InterfaceDiscovered;
                _sd.ServiceDiscovered += ServiceDiscovered;
                _sd.ServiceInstanceDiscovered += GlimmrDiscovered;
                _mDns.Start();
                await Task.Delay(TimeSpan.FromSeconds(timeout), CancellationToken.None);
                _mDns.NetworkInterfaceDiscovered -= InterfaceDiscovered;
                _sd.ServiceDiscovered -= ServiceDiscovered;
                _sd.ServiceInstanceDiscovered -= GlimmrDiscovered;
                _stopDiscovery = true;
                //_mDns.Stop();
            } catch {
                // Ignore collection modified exception
            }

            Log.Debug("Glimmr: Discovery complete...");
        }

        private void GlimmrDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
            var name = e.ServiceInstanceName.ToString();
            if (!name.Contains("glimmr", StringComparison.InvariantCulture)) return;
            var rr = e.Message.AdditionalRecords;
            foreach (var id in from msg in rr where msg.Type == DnsType.A select msg.CanonicalName) {
                var split = id.Split(".")[0];
                var ip = IpUtil.GetIpFromHost(split);
                if (ip.ToString() != IpUtil.GetLocalIpAddress() && !string.Equals(split, Environment.MachineName, StringComparison.CurrentCultureIgnoreCase)) {
                    var nData = new GlimmrData(split);
                    Log.Debug($"Adding new glimmr {id}: " + JsonConvert.SerializeObject(nData));
                    _controlService.AddDevice(nData).ConfigureAwait(false);
                } else {
                    Log.Debug("Skipping self...");
                }
            }
        }

        public override string DeviceTag { get; set; } = "Glimmr";
    }
}