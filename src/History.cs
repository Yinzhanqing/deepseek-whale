using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DeepSeekWhale
{
    public class Sample
    {
        public long T;
        public string C;
        public decimal Total;
        public decimal Granted;
        public decimal Topped;
    }

    public class TopupEvent
    {
        public long T;
        public string C;
        public decimal Amount;
    }

    public class SpendStats
    {
        public decimal Today;
        public decimal Week;
        public decimal Month;
        public decimal Total;
        public decimal BurnPerDay;
        public double DaysLeft;
        public decimal[] Daily = new decimal[30];
        public DateTime[] DayDates = new DateTime[30];
        public decimal MaxDaily;
        public decimal Balance;
        public int SampleCount;
        public long FirstT;
    }

    public class HistoryStore
    {
        public readonly List<Sample> Samples = new List<Sample>();
        public readonly List<TopupEvent> Topups = new List<TopupEvent>();
        public string Path;
        public long FirstT;

        public static HistoryStore Load(string path)
        {
            var h = new HistoryStore();
            h.Path = path;
            try
            {
                if (!File.Exists(path)) return h;
                var root = JsonUtil.Parse(File.ReadAllText(path, Encoding.UTF8));
                object arr;
                if (root.TryGetValue("samples", out arr) && arr != null)
                {
                    var list = arr as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        foreach (object o in list)
                        {
                            var d = o as Dictionary<string, object>;
                            if (d == null) continue;
                            string c = JsonUtil.Str(d, "c", null);
                            if (string.IsNullOrEmpty(c)) continue;
                            var s = new Sample();
                            s.T = JsonUtil.Long(d, "t", 0);
                            s.C = c;
                            s.Total = JsonUtil.Dec(d, "total", 0m);
                            s.Granted = JsonUtil.Dec(d, "granted", 0m);
                            s.Topped = JsonUtil.Dec(d, "topped", 0m);
                            if (s.T > 0) h.Samples.Add(s);
                        }
                    }
                }
                if (root.TryGetValue("topups", out arr) && arr != null)
                {
                    var list = arr as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        foreach (object o in list)
                        {
                            var d = o as Dictionary<string, object>;
                            if (d == null) continue;
                            var e = new TopupEvent();
                            e.T = JsonUtil.Long(d, "t", 0);
                            e.C = JsonUtil.Str(d, "c", "CNY");
                            e.Amount = JsonUtil.Dec(d, "amount", 0m);
                            if (e.T > 0 && e.Amount > 0) h.Topups.Add(e);
                        }
                    }
                }
                h.FirstT = JsonUtil.Long(root, "firstT", h.Samples.Count > 0 ? h.Samples.Min(x => x.T) : 0);
                h.Samples.Sort(delegate(Sample a, Sample b) { return a.T.CompareTo(b.T); });
            }
            catch (Exception ex)
            {
                Log.Warn("history load failed: " + ex.Message);
            }
            return h;
        }

        public void Save()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var root = new Dictionary<string, object>();
                var arr = new List<object>();
                foreach (var s in Samples)
                {
                    var d = new Dictionary<string, object>();
                    d["t"] = s.T;
                    d["c"] = s.C;
                    d["total"] = s.Total;
                    d["granted"] = s.Granted;
                    d["topped"] = s.Topped;
                    arr.Add(d);
                }
                var tarr = new List<object>();
                foreach (var e in Topups)
                {
                    var d = new Dictionary<string, object>();
                    d["t"] = e.T;
                    d["c"] = e.C;
                    d["amount"] = e.Amount;
                    tarr.Add(d);
                }
                root["samples"] = arr;
                root["topups"] = tarr;
                root["firstT"] = FirstT;
                string tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtil.Ser.Serialize(root), new UTF8Encoding(false));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
            }
            catch (Exception ex)
            {
                Log.Error("history save failed: " + ex.Message);
            }
        }

        public TopupEvent Add(BalanceResult r, string cur)
        {
            CurrencyBalance b;
            if (r == null || !r.Ok || !r.Cur.TryGetValue(cur, out b)) return null;
            Sample last = null;
            for (int i = Samples.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Samples[i].C, cur, StringComparison.OrdinalIgnoreCase)) { last = Samples[i]; break; }
            }
            if (last != null && last.Total == b.Total && r.T - last.T < 1800) return null;

            TopupEvent topup = null;
            if (last != null && b.Total > last.Total + 0.005m)
            {
                decimal amount = b.Total - last.Total;
                if (amount >= 0.01m)
                {
                    topup = new TopupEvent();
                    topup.T = r.T;
                    topup.C = cur;
                    topup.Amount = amount;
                    Topups.Add(topup);
                    Log.Info("topup detected " + cur + " +" + amount.ToString(CultureInfo.InvariantCulture));
                }
            }

            var s = new Sample();
            s.T = r.T;
            s.C = cur;
            s.Total = b.Total;
            s.Granted = b.Granted;
            s.Topped = b.Topped;
            Samples.Add(s);
            if (FirstT == 0 || r.T < FirstT) FirstT = r.T;
            Prune();
            Save();
            return topup;
        }

        void Prune()
        {
            long cutoff = Fmt.NowUnix() - 400 * 86400;
            Samples.RemoveAll(delegate(Sample s) { return s.T < cutoff; });
            Topups.RemoveAll(delegate(TopupEvent e) { return e.T < cutoff; });
        }

        public SpendStats Compute(string cur, decimal balance, DateTime nowLocal)
        {
            var st = new SpendStats();
            st.Balance = balance;
            var today = nowLocal.Date;
            for (int i = 0; i < 30; i++) st.DayDates[i] = today.AddDays(i - 29);

            var list = Samples.Where(delegate(Sample s) { return string.Equals(s.C, cur, StringComparison.OrdinalIgnoreCase); })
                              .OrderBy(delegate(Sample s) { return s.T; }).ToList();
            st.SampleCount = list.Count;
            if (list.Count == 0) return st;

            st.FirstT = list[0].T;
            var byDay = new Dictionary<DateTime, decimal>();
            for (int i = 1; i < list.Count; i++)
            {
                decimal delta = list[i - 1].Total - list[i].Total;
                if (delta <= 0.004m) continue;
                DateTime day = Fmt.FromUnix(list[i].T).ToLocalTime().Date;
                decimal prev;
                byDay.TryGetValue(day, out prev);
                byDay[day] = prev + delta;
                st.Total += delta;
            }

            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime weekStart = today.AddDays(-6);
            for (int i = 0; i < 30; i++)
            {
                decimal v;
                byDay.TryGetValue(st.DayDates[i], out v);
                st.Daily[i] = v;
                if (v > st.MaxDaily) st.MaxDaily = v;
            }
            foreach (var kv in byDay)
            {
                if (kv.Key == today) st.Today += kv.Value;
                if (kv.Key >= weekStart && kv.Key <= today) st.Week += kv.Value;
                if (kv.Key >= monthStart && kv.Key <= today) st.Month += kv.Value;
            }

            st.BurnPerDay = st.Week / 7m;
            if (st.BurnPerDay > 0) st.DaysLeft = (double)(balance / st.BurnPerDay);
            else st.DaysLeft = -1;

            if (st.DaysLeft > 3650) st.DaysLeft = 3650;
            return st;
        }

        public List<TopupEvent> RecentTopups(string cur, int max)
        {
            return Topups.Where(delegate(TopupEvent e) { return string.Equals(e.C, cur, StringComparison.OrdinalIgnoreCase); })
                         .OrderByDescending(delegate(TopupEvent e) { return e.T; })
                         .Take(max).ToList();
        }
    }
}
