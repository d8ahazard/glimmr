using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace Glimmr.Models.Util {
    public static class IpUtil {
        public static IPEndPoint Parse(string endpointstring, int defaultport) {
            if (string.IsNullOrEmpty(endpointstring)
                || endpointstring.Trim().Length == 0) {
                throw new ArgumentException("Endpoint descriptor may not be empty.");
            }

            if (defaultport != -1 &&
                (defaultport < IPEndPoint.MinPort
                 || defaultport > IPEndPoint.MaxPort)) {
                throw new ArgumentException(string.Format("Invalid default port '{0}'", defaultport));
            }

            string[] values = endpointstring.Split(new[] {':'});
            IPAddress ipaddy;
            int port;

            //check if we have an IPv6 or ports
            if (values.Length <= 2) // ipv4 or hostname
            {
                if (values.Length == 1)
                    //no port is specified, default
                    port = defaultport;
                else
                    port = GetPort(values[1]);

                //try to use the address as IPv4, otherwise get hostname
                if (!IPAddress.TryParse(values[0], out ipaddy))
                    ipaddy = GetIpFromHost(values[0]);
                if (ipaddy == null) return null;
            } else if (values.Length > 2) //ipv6
            {
                //could [a:b:c]:d
                if (values[0].StartsWith("[") && values[values.Length - 2].EndsWith("]")) {
                    string ipaddressstring = string.Join(":", values.Take(values.Length - 1).ToArray());
                    ipaddy = IPAddress.Parse(ipaddressstring);
                    port = GetPort(values[values.Length - 1]);
                } else //[a:b:c] or a:b:c
                {
                    ipaddy = IPAddress.Parse(endpointstring);
                    port = defaultport;
                }
            } else {
                throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endpointstring));
            }

            if (port == -1)
                throw new ArgumentException(string.Format("No port specified: '{0}'", endpointstring));

            return new IPEndPoint(ipaddy, port);
        }

        private static int GetPort(string p) {
            int port;

            if (!int.TryParse(p, out port)
                || port < IPEndPoint.MinPort
                || port > IPEndPoint.MaxPort) {
                throw new FormatException($@"Invalid end point port '{p}'");
            }

            return port;
        }

        public static IPAddress GetIpFromHost(string p) {
            if (string.IsNullOrEmpty(p)) return null;
            var hosts = Dns.GetHostAddresses(p);

            if (hosts == null || hosts.Length == 0)
                throw new ArgumentException(string.Format("Host not found: {0}", p));

            return hosts[0];
        }

       
        public static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            throw new Exception("No network adapters found in " + JsonConvert.SerializeObject(host));
        }
       
    }
}