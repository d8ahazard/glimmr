using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HueDream.Models.StreamingDevice.Nanoleaf {
    public class Requests {
        public RequestObj Request;
        public static RequestObj Put(string addr, JObject data) {
            return DoRequest(addr, data, "PUT");
        }
        
        public static RequestObj Post(string addr, JObject data) {
            return DoRequest(addr, data, "POST");
        }
        
        public static RequestObj Get(string addr) {
            return DoRequest(addr, "GET");
        }
        
        public static RequestObj Delete(string addr) {
            return DoRequest(addr, "DELETE");
        }

        private static RequestObj DoRequest(string addr, JObject data, string method) {
            var res = new RequestObj(addr, data, method);
            res.Process();
            return res;
        }
        
        private static RequestObj DoRequest(string addr, string method) {
            var res = new RequestObj(addr, null, method);
            res.Process();
            return res;
        }
    }

    public class RequestResult {
        public RequestObj Request;
        public RequestResult(string addr, JObject data, string method) {
            Request = new RequestObj(addr, data, method);
        }
    }

    public class RequestObj {
        public string Destination { get; set; }
        public JObject Payload { get; set; }
        public string RequestType { get; set; }
        public int StatusCode { get; set; }
        public string Text { get; set; }
        public JObject Json { get; set; }

        public RequestObj(string dest, JObject data, string method) {
            Destination = dest;
            Payload = data;
            RequestType = method;
        }

        public void Process() {
            HttpResponseMessage res = null;
            var hc = new HttpClient();
            switch (RequestType) {
                case "GET":
                    res = hc.GetAsync(Destination).Result;
                    break;
                case "PUT":
                    var putContent = new StringContent(Payload.ToString(), Encoding.UTF8, "application/json");
                    res = hc.PutAsync(Destination, putContent).Result;
                    break;
                case "POST":
                    var postContent = new StringContent(Payload.ToString(), Encoding.UTF8, "application/json");
                    res = hc.PostAsync(Destination, postContent).Result;
                    break;
                case "DELETE":
                    res = hc.DeleteAsync(Destination).Result;
                    break;
            }

            if (res != null) {
                StatusCode = (int) res.StatusCode;
                var headers = res.Content.Headers;
                var ct = headers.ContentType.MediaType;
                if (ct == "application/json") {
                    Json = JObject.Parse(res.Content.ToString());
                } else {
                    Text = res.Content.ToString();
                }
            }
        }
    }
}