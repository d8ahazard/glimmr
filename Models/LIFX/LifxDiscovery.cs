using System;
using System.Collections.Generic;
using System.Diagnostics;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.LIFX {
    public sealed class LifxDiscovery : IDisposable {
        private readonly LifxClient _client;
        private List<LightBulb> _bulbs;
        private bool _disposed;

        public LifxDiscovery() {
            _client = LifxClient.CreateAsync().Result;
        }

        public List<LifxData> Discover(int timeOut) {
            _bulbs = new List<LightBulb>();
            _client.DeviceDiscovered += Client_DeviceDiscovered;
            var s = new Stopwatch();
            s.Start();
            _client.StartDeviceDiscovery();
            LogUtil.Write("Starting discovery.");
            while (s.ElapsedMilliseconds < timeOut * 1000) {
            }

            LogUtil.Write("Discovery completed.");
            _client.StopDeviceDiscovery();
            var output = new List<LifxData>();
            foreach (var b in _bulbs) {
                var state = _client.GetLightStateAsync(b).Result;
                var d = new LifxData(b) {
                    Power = _client.GetLightPowerAsync(b).Result,
                    Hue = state.Hue / 35565 * 360,
                    Saturation = state.Saturation / 35565,
                    Brightness = state.Brightness / 35565,
                    Kelvin =  state.Kelvin
                };
                output.Add(d);
            }

            return output;
        }

        public List<LifxData> Refresh() {
            var b = Discover(5);
            var output = new List<LifxData>();
            var existing = DataUtil.GetItem<List<LifxData>>("lifxBulbs");
            foreach (var bulb in b) {
                var add = true;
                if (existing != null) {
                    foreach (LifxData e in existing) {
                        if (e.MacAddress == bulb.MacAddress) {
                            add = false;
                        }
                    }
                }

                if (add) {
                    output.Add(bulb);
                }
            }

            if (existing != null) output.AddRange(existing);
            return output;
        }

        private void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            LogUtil.Write("Bulb discovered?");
            _bulbs.Add(bulb);
        }

        public void Dispose() {
            Dispose(true);
        }


        private void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (disposing) {
                _client?.Dispose();
            }

            _disposed = true;
        }
    }
}