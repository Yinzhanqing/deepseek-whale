using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DeepSeekWhale
{
    public class DshSession
    {
        public string Id = "";
        public string Title = "";
        public string Model = "";
        public long LastPromptAt;
        public long CreatedAt;
        public long CacheRead;
        public long Uncached;
        public long CacheWrite;
        public long Output;
        public long Total;
        public int Turns;
        public int Steps;
        public long SurfaceTokens;
        public long ContextWindow;
        public decimal Cost;
        public decimal CacheSaved;

        public double CacheHitPct
        {
            get
            {
                long denom = CacheRead + Uncached;
                return denom > 0 ? CacheRead * 100.0 / denom : 0;
            }
        }

        public double ContextPct
        {
            get { return ContextWindow > 0 ? SurfaceTokens * 100.0 / ContextWindow : 0; }
        }
    }

    public class DshSnapshot
    {
        public bool Available;
        public string Error;
        public readonly List<DshSession> Sessions = new List<DshSession>();
        public DshSession Current;
        public decimal TodayCost;
        public decimal WeekCost;
        public decimal MonthCost;
        public decimal TotalCost;
        public decimal CacheSavings;
        public long TodayTokens;
        public int TodaySessions;
    }

    public static class Pricing
    {
        public static bool IsPeak(DateTime local)
        {
            DateTime bj = local.ToUniversalTime().AddHours(8);
            DayOfWeek d = bj.DayOfWeek;
            if (d == DayOfWeek.Saturday || d == DayOfWeek.Sunday) return false;
            int m = bj.Hour * 60 + bj.Minute;
            return (m >= 540 && m < 720) || (m >= 840 && m < 1080);
        }

        public static TimeSpan UntilNextChange(DateTime local, out bool nextIsPeak)
        {
            bool nowPeak = IsPeak(local);
            for (int d = 0; d < 8; d++)
            {
                DateTime day = local.Date.AddDays(d);
                int[] hours = { 9, 12, 14, 18 };
                foreach (int h in hours)
                {
                    DateTime t = day.AddHours(h);
                    if (t <= local) continue;
                    if (IsPeak(t) != nowPeak)
                    {
                        nextIsPeak = IsPeak(t);
                        return t - local;
                    }
                }
            }
            nextIsPeak = !nowPeak;
            return TimeSpan.FromDays(1);
        }

        public static bool IsPro(string model)
        {
            return model != null && model.IndexOf("pro", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ModelLabel(string model)
        {
            if (string.IsNullOrEmpty(model)) return "";
            if (IsPro(model)) return "pro";
            if (model.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0) return "flash";
            return model;
        }

        public static decimal Cost(string model, long cacheRead, long uncached, long cacheWrite, long output, DateTime at)
        {
            bool peak = IsPeak(at);
            bool pro = IsPro(model);
            decimal cr = pro ? 0.15m : 0.02m;
            decimal ui = pro ? 4.5m : 1.0m;
            decimal op = pro ? 13.5m : 4.0m;
            if (peak) { cr *= 2m; ui *= 2m; op *= 2m; }
            return (cacheRead * cr + uncached * ui + output * op) / 1000000m;
        }

        public static decimal CacheSavings(string model, long cacheRead, DateTime at)
        {
            bool peak = IsPeak(at);
            bool pro = IsPro(model);
            decimal cr = pro ? 0.15m : 0.02m;
            decimal ui = pro ? 4.5m : 1.0m;
            if (peak) { cr *= 2m; ui *= 2m; }
            return cacheRead * (ui - cr) / 1000000m;
        }
    }

    public static class DshUsage
    {
        public static DshSnapshot Scan()
        {
            var snap = new DshSnapshot();
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dsh", "storages", "session_projcache", "sessions");
            if (!Directory.Exists(dir))
            {
                snap.Error = "没有找到 DSH 会话缓存";
                return snap;
            }
            snap.Available = true;
            string[] files;
            try { files = Directory.GetFiles(dir, "*.json"); }
            catch (Exception ex) { snap.Error = "读不了 DSH 会话目录：" + ex.Message; return snap; }

            foreach (string file in files)
            {
                try
                {
                    var s = Parse(file);
                    if (s != null) snap.Sessions.Add(s);
                }
                catch { }
            }

            snap.Sessions.Sort(delegate(DshSession a, DshSession b) { return b.LastPromptAt.CompareTo(a.LastPromptAt); });
            DateTime today = DateTime.Today;
            DateTime weekStart = today.AddDays(-6);
            foreach (var s in snap.Sessions)
            {
                if (s.Total <= 0) continue;
                if (snap.Current == null) snap.Current = s;
                snap.TotalCost += s.Cost;
                snap.CacheSavings += s.CacheSaved;
                DateTime at = Fmt.FromUnix(s.LastPromptAt > 0 ? s.LastPromptAt : s.CreatedAt).ToLocalTime();
                if (at.Date == today)
                {
                    snap.TodayCost += s.Cost;
                    snap.TodayTokens += s.Total;
                    snap.TodaySessions++;
                }
                if (at.Date >= weekStart && at.Date <= today) snap.WeekCost += s.Cost;
                if (at.Year == today.Year && at.Month == today.Month) snap.MonthCost += s.Cost;
            }
            if (snap.Sessions.Count == 0) snap.Error = "DSH 还没有用量记录";
            return snap;
        }

        static DshSession Parse(string file)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            var root = JsonUtil.Parse(text);
            object recObj;
            if (!root.TryGetValue("record", out recObj)) return null;
            var rec = recObj as Dictionary<string, object>;
            if (rec == null) return null;
            object rowsObj;
            if (!rec.TryGetValue("rows", out rowsObj)) return null;
            var rows = rowsObj as Dictionary<string, object>;
            if (rows == null) return null;

            var usage = Val(rows, "tokenUsage");
            if (usage == null) return null;
            object totalsObj;
            if (!usage.TryGetValue("totals", out totalsObj)) return null;
            var totals = totalsObj as Dictionary<string, object>;
            if (totals == null) return null;

            long cacheRead = Long(totals, "cacheReadTokens");
            long uncached = Long(totals, "uncachedInputTokens");
            long cacheWrite = Long(totals, "cacheWriteTokens");
            long output = Long(totals, "outputTokens");
            long total = cacheRead + uncached + cacheWrite + output;
            if (total <= 0) return null;

            var s = new DshSession();
            s.Id = Path.GetFileNameWithoutExtension(file);
            s.CacheRead = cacheRead;
            s.Uncached = uncached;
            s.CacheWrite = cacheWrite;
            s.Output = output;
            s.Total = total;

            s.Title = StrVal(rows, "title") ?? "";

            var meta = Val(rows, "sessionListMetadata");
            if (meta != null) s.LastPromptAt = (long)(Dbl(meta, "lastPromptAt") / 1000.0);

            object identObj;
            if (rec.TryGetValue("identity", out identObj))
            {
                var ident = identObj as Dictionary<string, object>;
                if (ident != null) s.CreatedAt = (long)(Dbl(ident, "createdAt") / 1000.0);
            }
            if (s.LastPromptAt <= 0) s.LastPromptAt = s.CreatedAt;

            var modelSel = Val(rows, "modelSelection");
            if (modelSel != null)
            {
                object lastObj;
                if (modelSel.TryGetValue("lastUsed", out lastObj))
                {
                    var last = lastObj as Dictionary<string, object>;
                    if (last != null) s.Model = JsonUtil.Str(last, "model", "");
                }
            }

            var stats = Val(rows, "sessionStats");
            if (stats != null)
            {
                s.Turns = (int)Dbl(stats, "turns");
                s.Steps = (int)Dbl(stats, "steps");
            }

            var pressure = Val(rows, "contextPressure");
            if (pressure != null)
            {
                s.SurfaceTokens = Long(pressure, "surfaceTokens");
                s.ContextWindow = Long(pressure, "contextWindow");
            }

            DateTime at = Fmt.FromUnix(s.LastPromptAt > 0 ? s.LastPromptAt : s.CreatedAt).ToLocalTime();
            s.Cost = Pricing.Cost(s.Model, s.CacheRead, s.Uncached, s.CacheWrite, s.Output, at);
            s.CacheSaved = Pricing.CacheSavings(s.Model, s.CacheRead, at);
            return s;
        }

        static Dictionary<string, object> Val(Dictionary<string, object> rows, string key)
        {
            object o;
            if (!rows.TryGetValue(key, out o)) return null;
            var d = o as Dictionary<string, object>;
            if (d == null) return null;
            object v;
            if (!d.TryGetValue("val", out v)) return null;
            return v as Dictionary<string, object>;
        }

        static string StrVal(Dictionary<string, object> rows, string key)
        {
            object o;
            if (!rows.TryGetValue(key, out o)) return null;
            var d = o as Dictionary<string, object>;
            if (d == null) return null;
            object v;
            if (!d.TryGetValue("val", out v)) return null;
            return v as string;
        }

        static long Long(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        static double Dbl(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }
    }
}
