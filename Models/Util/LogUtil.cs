using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Serilog;

namespace HueDream.Models.Util {
    public static class LogUtil {
        private static int msgCount;
        
        public static void Write(string text, dynamic myObject) {
            var objStr = string.Empty;
            if (!myObject.GetType().IsSerializable) {
                Write(text);
            } else {
                objStr = JsonConvert.SerializeObject(myObject, myObject.GetType());
            }
            Write(text + objStr);
        
        }

        public static void Write(string text) {
            var cls = GetCaller();
            Log.Information(text);
            Console.WriteLine($@"{cls} - {text}");
        }

        public static void WriteInc(string text, dynamic myObject) {
            var objStr = string.Empty;
            if (!myObject.GetType().IsSerializable) {
                WriteInc(text);
            } else {
                objStr = JsonConvert.SerializeObject(myObject, myObject.GetType());
            }
            WriteInc(text + objStr);
        }

        public static void WriteDec(string text, dynamic myObject) {
            var objStr = string.Empty;
            if (!myObject.GetType().IsSerializable) {
                WriteDec(text);
            } else {
                objStr = JsonConvert.SerializeObject(myObject, myObject.GetType());
            }
            WriteDec(text + objStr);
        }
        
        public static void WriteInc(string text) {
            Write($@"C{msgCount} - {text}");
            msgCount++;
        }

        public static void WriteDec(string text) {
            msgCount--;
            if (msgCount < 0) msgCount = 0;
            Write($@"C{msgCount} - {text}");
        }

        private static string GetCaller() {
            var cls = string.Empty;
            int stackInt = 1;
            while (stackInt < 5) {
                var mth = new StackTrace().GetFrame(stackInt).GetMethod();
                if (mth.ReflectedType != null) cls = mth.ReflectedType.Name;
                if (cls != string.Empty) {
                    if (cls != "LogUtil") {
                        return cls;
                    }
                }
                stackInt++;
            }

            return cls;
        }
    }
}