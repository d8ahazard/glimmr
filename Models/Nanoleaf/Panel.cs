using HueDream.Models.Util;
using Nanoleaf.Client;
using Nanoleaf.Client.Exceptions;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HueDream.Models.Nanoleaf {
    public class Panel {

        private string IpAddress;
        private string Token;
        private string BasePath;
        private HttpClient HC;
        public Panel(string ipAddress, string token = "") {
            IpAddress = ipAddress;
            Token = token;
            BasePath = "http://" + IpAddress + ":16021/api/v1/" + Token;
            LogUtil.Write("Created");
            HC = new HttpClient();
        }

        public async Task<UserToken> CheckAuth() {
            try {
                var nanoleaf = new NanoleafClient(IpAddress);
                return await nanoleaf.CreateTokenAsync();
            } catch (Exception) {

            }
            return null;
        }

        public async Task<NanoLayout> GetLayout() {
            if (string.IsNullOrEmpty(Token)) return null;
            LogUtil.Write("Getting layout.");
            var layout = await SendGetRequest("panelLayout/layout");
            var lObject = JsonConvert.DeserializeObject<NanoLayout>(layout);
            LogUtil.Write("We got a layout: " + JsonConvert.SerializeObject(lObject));
            return lObject;
        }

        public async Task SendPutRequest(string json, string path = "") {

            var authorizedPath = BasePath + "/" + path;
            
            using (var content = new StringContent(json, Encoding.UTF8, "application/json")) {
                using (var responseMessage = await HC.PutAsync(authorizedPath, content)) {
                    if (!responseMessage.IsSuccessStatusCode) {
                        HandleNanoleafErrorStatusCodes(responseMessage);
                    }
                }
            }
        }

        public async Task<string> SendGetRequest(string path = "") {
            var authorizedPath = BasePath + "/" + path;
            LogUtil.Write("Auth path is : " + authorizedPath);
            using (var responseMessage = await HC.GetAsync(authorizedPath)) {
                if (!responseMessage.IsSuccessStatusCode) {
                    LogUtil.Write("Error code?");
                    HandleNanoleafErrorStatusCodes(responseMessage);
                }
                LogUtil.Write("Returning");
                return await responseMessage.Content.ReadAsStringAsync();
            }
        }
        private void HandleNanoleafErrorStatusCodes(HttpResponseMessage responseMessage) {
            switch ((int)responseMessage.StatusCode) {
                case 400:
                    throw new NanoleafHttpException("Error 400: Bad request!");
                case 401:
                    throw new NanoleafUnauthorizedException($"Error 401: Not authorized! Provided an invalid token for this Aurora. Request path: {responseMessage.RequestMessage.RequestUri.AbsolutePath}");
                case 403:
                    throw new NanoleafHttpException("Error 403: Forbidden!");
                case 404:
                    throw new NanoleafResourceNotFoundException($"Error 404: Resource not found! Request Uri: {responseMessage.RequestMessage.RequestUri.AbsoluteUri}");
                case 422:
                    throw new NanoleafHttpException("Error 422: Unprocessable Entity");
                case 500:
                    throw new NanoleafHttpException("Error 500: Internal Server Error");
                default:
                    throw new NanoleafHttpException("ERROR! UNKNOWN ERROR " + (int)responseMessage.StatusCode);
            }
        }
    }

    
}
