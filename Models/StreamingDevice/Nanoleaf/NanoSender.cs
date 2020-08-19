using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Nanoleaf.Client.Exceptions;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.Nanoleaf {
    public static class NanoSender {
        private static HttpClient client;

        public static HttpClient getClient() {
            client ??= new HttpClient();
            return client;
        }
        public static async Task<string> SendPutRequest(string basePath, string json, string path = "") {
            var authorizedPath = new Uri(basePath + "/" + path);
            try {
                var hc = getClient();
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var responseMessage = await hc.PutAsync(authorizedPath, content).ConfigureAwait(false);
                if (!responseMessage.IsSuccessStatusCode) {
                    HandleNanoleafErrorStatusCodes(responseMessage);
                }

                return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (HttpRequestException) {
                return null;
            }
        }

        public static async Task<string> SendGetRequest(string basePath, string path = "") {
            var authorizedPath = basePath + "/" + path;
            var uri = new Uri(authorizedPath);
            var hc = getClient();
            try {
                using var responseMessage = await hc.GetAsync(uri).ConfigureAwait(false);
                if (responseMessage.IsSuccessStatusCode)
                    return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogUtil.Write("Error contacting nanoleaf: " + responseMessage.Content);
                HandleNanoleafErrorStatusCodes(responseMessage);

                return await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (HttpRequestException) {
                return null;
            }
        }

        private static void HandleNanoleafErrorStatusCodes(HttpResponseMessage responseMessage) {
            LogUtil.Write("Error with nano request: " + responseMessage.StatusCode, "ERROR");
            throw (int) responseMessage.StatusCode switch {
                400 => new NanoleafHttpException("Error 400: Bad request!"),
                401 => new NanoleafUnauthorizedException(
                    $"Error 401: Not authorized! Provided an invalid token for this Aurora. Request path: {responseMessage.RequestMessage.RequestUri.AbsolutePath}"),
                403 => new NanoleafHttpException("Error 403: Forbidden!"),
                404 => new NanoleafResourceNotFoundException(
                    $"Error 404: Resource not found! Request Uri: {responseMessage.RequestMessage.RequestUri.AbsoluteUri}"),
                422 => new NanoleafHttpException("Error 422: Unprocessable Entity"),
                500 => new NanoleafHttpException("Error 500: Internal Server Error"),
                _ => new NanoleafHttpException("ERROR! UNKNOWN ERROR " + (int) responseMessage.StatusCode)
            };
        }
    }
}