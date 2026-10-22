using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;

namespace DeepSeekWhale
{
    public static class SelfTest
    {
        static int failures;
        static StringBuilder log = new StringBuilder();

        public static int Run()
        {
            failures = 0;
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_selftest");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Log.Init(dir);

            try
            {
                TestConfig(dir);
                TestHistory();
                TestApiParse();
                TestFormat();
                TestRender(dir);
            }
            catch (Exception ex)
            {
                Fail("unhandled: " + ex);
            }

            Console.WriteLine(log.ToString());
            Console.WriteLine(failures == 0 ? "SELFTEST PASS" : "SELFTEST FAIL (" + failures + ")");
            Console.WriteLine("ARTIFACTS " + dir);
            return failures == 0 ? 0 : 1;
        }

        static void Check(bool ok, string name, string detail)
        {
            if (ok) log.AppendLine("  ok  " + name);
            else { failures++; log.AppendLine("  FAIL " + name + " :: " + detail); }
        }

        static void Fail(string name) { failures++; log.AppendLine("  FAIL " + name); }

        static void TestConfig(string dir)
        {
            log.AppendLine("[config]");
            string path = Path.Combine(dir, "config.json");
            var c = new AppConfig();
            c.SetKey("sk-demo-key");
            string key = c.GetKey();
            Check(key == "sk-demo-key", "dpapi roundtrip", key == null ? "null" : key);
            Check(c.KeyHint == "cdef", "key hint", c.KeyHint);
            c.IntervalMin = 15;
            c.LowBalance = 3.5;
            c.Save(path);
            var c2 = AppConfig.Load(path);
            Check(c2.IntervalMin == 15, "interval reload", c2.IntervalMin.ToString());
            Check(Math.Abs(c2.LowBalance - 3.5) < 0.001, "threshold reload", c2.LowBalance.ToString(CultureInfo.InvariantCulture));
            Check(c2.GetKey() == "sk-demo-key", "key reload", "mismatch");
            Check(File.ReadAllText(path).IndexOf("sk-demo-key", StringComparison.Ordinal) < 0, "key not plaintext", "found key in json");
        }

        static void TestHistory()
        {
            log.AppendLine("[history]");
            var h = new HistoryStore();
            h.Path = Path.Combine(Path.GetTempPath(), "dswhale-history-" + Guid.NewGuid().ToString("N") + ".json");
            DateTime today = DateTime.Now.Date;
            long t0 = Fmt.ToUnix(today.AddDays(-2).AddHours(12));
            h.Samples.Add(MakeSample(t0, "CNY", 100m));
            h.Samples.Add(MakeSample(t0 + 3600, "CNY", 90m));
            h.Samples.Add(MakeSample(t0 + 7200, "CNY", 140m));
            h.Samples.Add(MakeSample(t0 + 9000, "CNY", 139.5m));
            h.Topups.Add(new TopupEvent { T = t0 + 7200, C = "CNY", Amount = 50m });

            var st = h.Compute("CNY", 139.5m, today.AddHours(20));
            Check(st.Total == 10.5m, "total spend", st.Total.ToString(CultureInfo.InvariantCulture));
            Check(h.RecentTopups("CNY", 3).Count == 1, "topup count", h.RecentTopups("CNY", 3).Count.ToString());
            Check(st.Daily[29] == 0m, "today empty", st.Daily[29].ToString(CultureInfo.InvariantCulture));
            Check(st.Daily[27] == 10.5m, "two days ago", st.Daily[27].ToString(CultureInfo.InvariantCulture));

            DateTime today2 = DateTime.Now.Date.AddHours(23);
            long t3 = Fmt.ToUnix(today2.AddMinutes(-30));
            h.Samples.Add(MakeSample(t3, "CNY", 138m));
            var st2 = h.Compute("CNY", 138m, today2);
            Check(st2.Today == 1.5m, "today spend", st2.Today.ToString(CultureInfo.InvariantCulture));
            Check(st2.Week == 12m, "week spend", st2.Week.ToString(CultureInfo.InvariantCulture));
            Check(st2.DaysLeft > 0, "days left positive", st2.DaysLeft.ToString(CultureInfo.InvariantCulture));

            var h2 = new HistoryStore();
            h2.Path = h.Path + ".2";
            var res = new BalanceResult();
            res.T = t0;
            res.Cur["CNY"] = new CurrencyBalance { Total = 100m, Granted = 100m };
            Check(h2.Add(res, "CNY") == null, "first sample no topup", "unexpected topup");
            res = new BalanceResult();
            res.T = t0 + 60;
            res.Cur["CNY"] = new CurrencyBalance { Total = 150m, Granted = 150m };
            var tu = h2.Add(res, "CNY");
            Check(tu != null && tu.Amount == 50m, "topup detection", tu == null ? "null" : tu.Amount.ToString(CultureInfo.InvariantCulture));
        }

        static Sample MakeSample(long t, string cur, decimal total)
        {
            var s = new Sample();
            s.T = t; s.C = cur; s.Total = total; s.Granted = 0; s.Topped = total;
            return s;
        }

        static void TestApiParse()
        {
            log.AppendLine("[api]");
            string json = "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"42.80\",\"granted_balance\":\"30.00\",\"topped_up_balance\":\"12.80\"},{\"currency\":\"USD\",\"total_balance\":\"5.00\",\"granted_balance\":\"0.00\",\"topped_up_balance\":\"5.00\"}]}";
            var r = new BalanceResult();
            Api.Parse(json, r);
            Check(r.Ok, "parse ok", r.Error);
            Check(r.Cur.ContainsKey("CNY") && r.Cur["CNY"].Total == 42.80m, "cny total", "bad");
            Check(r.Cur["USD"].Topped == 5.00m, "usd topped", "bad");
            Check(r.Available, "available flag", "false");
        }

        static void TestFormat()
        {
            log.AppendLine("[fmt]");
            Check(Fmt.Money(42.8m, "¥") == "¥42.80", "money 2dp", Fmt.Money(42.8m, "¥"));
            Check(Fmt.Money(0.0042m, "¥") == "¥0.0042", "money 4dp", Fmt.Money(0.0042m, "¥"));
            Check(Fmt.Money(1234.5m, "¥") == "¥1,234.50", "money thousands", Fmt.Money(1234.5m, "¥"));
            Check(Fmt.Days(49.2) == "49 天", "days", Fmt.Days(49.2));
        }

        static void TestRender(string dir)
        {
            log.AppendLine("[render]");
            string p = Path.Combine(dir, "smoke.png");
            using (var bmp = new Bitmap(400, 300))
            using (var g = Graphics.FromImage(bmp))
            {
                bmp.SetResolution(96f, 96f);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);
                var vm = new Vm();
                vm.KeySet = true; vm.HasData = true; vm.Cur = "CNY"; vm.Sym = "¥"; vm.Balance = 42.8m;
                vm.Mood = Mood.Happy;
                Renderer.DrawCompact(g, vm, 1.0, new PointF(-1, -1));
                bmp.Save(p);
            }
            Check(new FileInfo(p).Length > 1000, "render png", "too small");
        }
    }
}
