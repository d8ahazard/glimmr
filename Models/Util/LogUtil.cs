using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog;

namespace HueDream.Models.Util {
    public static class LogUtil {
        private static int _msgCount;

        public static void Write(string text, string level="INFO") {
            var cls = GetCaller();
            var msg = $@"{cls} - {text}";
            switch (level) {
                case "INFO":
                    Log.Information(msg);
                    break;
                case "DEBUG":
                    Log.Debug(msg);
                    break;
                case "WARN":
                    Log.Warning(msg);
                    break;
                case "ERROR":
                    Log.Error(msg);
                    break;
            }
        }
        
        public static void WriteInc(string text, string level="INFO") {
            Write($@"C{_msgCount} - {text}", level);
            _msgCount++;
        }

        public static void WriteDec(string text, string level="INFO") {
            _msgCount--;
            if (_msgCount < 0) _msgCount = 0;
            Write($@"C{_msgCount} - {text}", level);
        }

        private static string GetCaller() {
            var stackInt = 1;
            var st = new StackTrace();
            
            while (stackInt < 10) {
                var frame = st.GetFrame(stackInt);
                if (frame == null) continue;
                var mth = frame.GetMethod();
                if (mth == null) continue;
                var dType = mth.DeclaringType;
                if (dType != null) {
                    var cls = dType.Name;
                    if (!string.IsNullOrEmpty(cls)) {
                        if (cls != "LogUtil") {
                            var c1 = Regex.Match(cls, @"(?<=<).*?(?=>)", RegexOptions.None);
                            cls = c1.Success ? c1.Value : cls;
                            var name = mth.Name == "MoveNext" ? "" : mth.Name;
                            return cls + "::" + name;
                        }
                    }
                }

                stackInt++;
            }

            return string.Empty;
        }
    }
}