using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DeepSeekWhale
{
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Money(decimal v, string sym)
        {
            if (v == 0m) return sym + "0.00";
            decimal a = Math.Abs(v);
            string num;
            if (a >= 1000m) num = v.ToString("#,0.00", Inv);
            else if (a >= 1m) num = v.ToString("0.00", Inv);
            else if (a >= 0.01m) num = v.ToString("0.000", Inv);
            else num = v.ToString("0.0000", Inv);
            return sym + num;
        }

        public static string Number(decimal v)
        {
            if (v == 0m) return "0.00";
            decimal a = Math.Abs(v);
            if (a >= 1000m) return v.ToString("#,0.00", Inv);
            if (a >= 1m) return v.ToString("0.00", Inv);
            if (a >= 0.01m) return v.ToString("0.000", Inv);
            return v.ToString("0.0000", Inv);
        }

        public static string Compact(decimal v)
        {
            decimal a = Math.Abs(v);
            if (a >= 1000000m) return (v / 1000000m).ToString("0.#", Inv) + "M";
            if (a >= 1000m) return (v / 1000m).ToString("0.#", Inv) + "k";
            return Number(v);
        }

        public static string Days(double d)
        {
            if (d < 0) return "—";
            if (d >= 365) return "1 年+";
            if (d >= 100) return ((int)d) + " 天";
            if (d >= 10) return d.ToString("0", Inv) + " 天";
            return d.ToString("0.#", Inv) + " 天";
        }

        public static string Tokens(long n)
        {
            if (n >= 1000000L) return (n / 1000000.0).ToString("0.#", Inv) + "M";
            if (n >= 1000L) return (n / 1000.0).ToString("0.#", Inv) + "k";
            return n.ToString(Inv);
        }

        public static string Span(TimeSpan s)
        {
            if (s.TotalDays >= 1) return ((int)Math.Round(s.TotalDays)) + " 天";
            if (s.TotalHours >= 1) return ((int)Math.Floor(s.TotalHours)) + " 小时";
            return Math.Max(1, (int)Math.Round(s.TotalMinutes)) + " 分钟";
        }

        public static string Ago(long utc)
        {
            if (utc <= 0) return "还没更新";
            double s = (DateTime.UtcNow - FromUnix(utc)).TotalSeconds;
            if (s < 0) s = 0;
            if (s < 45) return "刚刚";
            if (s < 3600) return ((int)(s / 60)) + " 分钟前";
            if (s < 86400) return ((int)(s / 3600)) + " 小时前";
            return ((int)(s / 86400)) + " 天前";
        }

        public static string Clock(long utc)
        {
            if (utc <= 0) return "--:--";
            return FromUnix(utc).ToLocalTime().ToString("HH:mm", Inv);
        }

        public static string ShortDate(long utc)
        {
            return FromUnix(utc).ToLocalTime().ToString("M/d", Inv);
        }

        public static DateTime FromUnix(long t)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(t);
        }

        public static long ToUnix(DateTime utc)
        {
            return (long)(utc.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static long NowUnix()
        {
            return ToUnix(DateTime.UtcNow);
        }

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, Math.Max(0, max - 1)) + "…";
        }
    }

    public static class Log
    {
        static readonly object Gate = new object();
        static string file;

        public static void Init(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                file = Path.Combine(dir, "whale.log");
            }
            catch { }
        }

        public static void Info(string msg) { Write("INFO", msg); }
        public static void Warn(string msg) { Write("WARN", msg); }
        public static void Error(string msg) { Write("ERR ", msg); }

        static void Write(string level, string msg)
        {
            if (file == null) return;
            try
            {
                lock (Gate)
                {
                    var fi = new FileInfo(file);
                    if (fi.Exists && fi.Length > 512 * 1024)
                    {
                        try { File.Delete(file + ".old"); } catch { }
                        try { File.Move(file, file + ".old"); } catch { }
                    }
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        + " [" + level + "] " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    public static class JsonUtil
    {
        public static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };

        public static Dictionary<string, object> Parse(string json)
        {
            return Ser.Deserialize<Dictionary<string, object>>(json);
        }

        public static string Str(Dictionary<string, object> d, string key, string def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return def;
        }

        public static double Dbl(Dictionary<string, object> d, string key, double def)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try
            {
                if (v is string) return double.Parse((string)v, CultureInfo.InvariantCulture);
                return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
            catch { return def; }
        }

        public static int Int(Dictionary<string, object> d, string key, int def)
        {
            return (int)Dbl(d, key, def);
        }

        public static long Long(Dictionary<string, object> d, string key, long def)
        {
            return (long)Dbl(d, key, def);
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try
            {
                if (v is bool) return (bool)v;
                if (v is string) return string.Equals((string)v, "true", StringComparison.OrdinalIgnoreCase);
                return Convert.ToInt32(v, CultureInfo.InvariantCulture) != 0;
            }
            catch { return def; }
        }

        public static decimal Dec(Dictionary<string, object> d, string key, decimal def)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try
            {
                if (v is string) return decimal.Parse((string)v, CultureInfo.InvariantCulture);
                return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
            }
            catch { return def; }
        }
    }

    public static class Paths
    {
        public static string DefaultDataDir
        {
            get
            {
                string a = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(a, "DeepSeekWhale");
            }
        }
    }
}
