using Serilog;

namespace Glimmr.Models.Util {
    public static class LogUtil {
        public static void Write(string msg) {
            Log.Debug(msg);
        }
    }
}