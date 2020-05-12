using LifxNet;

namespace HueDream.Models.StreamingDevice.LIFX {
    public static class LifxSender {
        private static LifxClient _client;

        public static LifxClient GetClient() {
            return _client ??= LifxClient.CreateAsync().Result;
        }

        public static void DestroyClient() {
            _client?.Dispose();
        }
    }
}