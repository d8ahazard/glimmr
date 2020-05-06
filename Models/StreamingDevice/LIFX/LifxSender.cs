using LifxNet;

namespace HueDream.Models.StreamingDevice.LIFX {
    public class LifxSender {
        private static LifxClient client;

        public static LifxClient getClient() {
            if (client == null)
                client = LifxClient.CreateAsync().Result;
            return client;
        }

        public static void destroyClient() {
            client?.Dispose();
        }
    }
}