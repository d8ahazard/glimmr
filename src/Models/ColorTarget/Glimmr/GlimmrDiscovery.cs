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
        private bool _discovering;
        private bool _stopDiscovery;
        private readonly ControlService _controlService;
        public GlimmrDiscovery(ColorService cs) : base(cs) {
            _mDns = cs.ControlService.MulticastService;
            _controlService = cs.ControlService;
            var sd = new ServiceDiscovery(_mDns);
            _mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_glimmr._tcp");
            };

            sd.ServiceDiscovered += (s, serviceName) => {
                if (!_stopDiscovery) _mDns.SendQuery(serviceName, type: DnsType.PTR);
            };

            sd.ServiceInstanceDiscovered += GlimmrDiscovered;
        }
        
        public async Task Discover(CancellationToken ct) {
            Log.Debug("Glimmr: Discovery started...");

            try {
                _mDns.Start();
                while (!ct.IsCancellationRequested) {
                    await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
                }

                _stopDiscovery = true;
                _mDns.Stop();
            } catch {
                // Ignore collection modified exception
            }

            Log.Debug("Glimmr: Discovery complete.");
        }

        private void GlimmrDiscovered(object sender, ServiceInstanceDiscoveryEventArgs e) {
            var name = e.ServiceInstanceName.ToString();
            if (!name.Contains("glimmr", StringComparison.InvariantCulture)) return;
            var rr = e.Message.AdditionalRecords;
            foreach (var id in from msg in rr where msg.Type == DnsType.A select msg.CanonicalName) {
                var split = id.Split(".")[0];
                var ip = IpUtil.GetIpFromHost(split);
                if (ip.ToString() != IpUtil.GetLocalIpAddress()) {
                    var nData = new GlimmrData(split);
                    Log.Debug($"Adding new glimmr {id}: " + JsonConvert.SerializeObject(nData));
                    _controlService.AddDevice(nData).ConfigureAwait(false);
                } else {
                    Log.Debug("Skipping self.");
                }
            }

            if (_stopDiscovery) _discovering = false;
        }

        public override string DeviceTag { get; set; } = "Glimmr";
    }
}