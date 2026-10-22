using System;
using System.Collections.Generic;

namespace DeepSeekWhale
{
    public enum Mood
    {
        Happy,
        Calm,
        Worried,
        Sad,
        Offline
    }

    public class Vm
    {
        public bool KeySet;
        public bool HasData;
        public bool Available;
        public string Error;
        public string Cur = "CNY";
        public string Sym = "¥";
        public decimal Balance;
        public decimal Granted;
        public decimal Topped;
        public SpendStats Stats = new SpendStats();
        public List<TopupEvent> Topups = new List<TopupEvent>();
        public Mood Mood = Mood.Offline;
        public bool Refreshing;
        public long LastOk;
        public string Message;
        public long MessageUntil;
        public decimal Budget;
        public decimal MonthSpend;
        public bool DshRunning;
        public bool Dnd;
        public DshSnapshot Dsh;
        public bool PeakNow;
        public string PeakChip = "闲时";
        public string PeakHint = "";
        public decimal DailyAllowance;
        public bool OverBudget;
        public DateTime RunOutDate = DateTime.MinValue;
        public int IntervalMin = 5;

        public decimal LowBalance;

        public bool IsStale
        {
            get
            {
                if (LastOk <= 0) return true;
                return Fmt.NowUnix() - LastOk > Math.Max(600, IntervalMin * 60 * 3);
            }
        }

        public string MoodText
        {
            get
            {
                if (!KeySet) return "未设置";
                if (!HasData) return "离线";
                if (Error != null) return "离线";
                if (Balance <= 0) return "欠费";
                if (LowBalance > 0 && Balance < (decimal)LowBalance) return "偏低";
                return "正常";
            }
        }
    }

    public static class VmFactory
    {
        public static Vm Build(AppConfig cfg, HistoryStore hist, BalanceResult last, bool refreshing,
                               string message, long messageUntil, bool dshRunning)
        {
            var vm = new Vm();
            vm.KeySet = cfg.HasKey;
            vm.Refreshing = refreshing;
            vm.Message = message;
            vm.MessageUntil = messageUntil;
            vm.DshRunning = dshRunning;
            vm.IntervalMin = cfg.IntervalMin;
            vm.LowBalance = (decimal)cfg.LowBalance;

            if (last != null && last.Ok)
            {
                vm.LastOk = last.T;
                vm.Available = last.Available;
                vm.HasData = last.Cur.Count > 0;
                vm.Error = null;
                string cur = PickCurrency(cfg, last);
                CurrencyBalance b;
                if (cur != null && last.Cur.TryGetValue(cur, out b))
                {
                    vm.Cur = cur;
                    vm.Sym = SymOf(cur);
                    vm.Balance = b.Total;
                    vm.Granted = b.Granted;
                    vm.Topped = b.Topped;
                    vm.Stats = hist.Compute(cur, b.Total, DateTime.Now);
                    vm.Topups = hist.RecentTopups(cur, 3);
                }
            }
            else if (last != null)
            {
                vm.Error = last.Error;
            }
            else
            {
                vm.Error = cfg.HasKey ? null : "未设置 API Key";
            }

            // 用历史里的最后一次成功数据兜底
            if (!vm.HasData && hist.Samples.Count > 0)
            {
                string cur = PickCurrencyFromHistory(cfg, hist);
                if (cur != null)
                {
                    var lastSample = hist.Samples[hist.Samples.Count - 1];
                    for (int i = hist.Samples.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(hist.Samples[i].C, cur, StringComparison.OrdinalIgnoreCase))
                        {
                            lastSample = hist.Samples[i];
                            break;
                        }
                    }
                    vm.Cur = cur;
                    vm.Sym = SymOf(cur);
                    vm.Balance = lastSample.Total;
                    vm.Granted = lastSample.Granted;
                    vm.Topped = lastSample.Topped;
                    vm.LastOk = lastSample.T;
                    vm.Stats = hist.Compute(cur, lastSample.Total, DateTime.Now);
                    vm.Topups = hist.RecentTopups(cur, 3);
                }
            }

            vm.Budget = (decimal)cfg.MonthlyBudget;
            vm.MonthSpend = vm.Stats.Month;

            vm.PeakNow = Pricing.IsPeak(DateTime.Now);
            bool nextPeak;
            TimeSpan span = Pricing.UntilNextChange(DateTime.Now, out nextPeak);
            vm.PeakChip = vm.PeakNow ? "高峰 2x" : "闲时 5折";
            vm.PeakHint = (vm.PeakNow ? "距闲时 " : "距高峰 ") + Fmt.Span(span);

            if (vm.Budget > 0m && vm.HasData)
            {
                decimal remaining = vm.Budget - vm.MonthSpend;
                if (remaining <= 0m)
                {
                    vm.OverBudget = true;
                    vm.DailyAllowance = 0m;
                }
                else
                {
                    DateTime now = DateTime.Now;
                    int daysLeft = DateTime.DaysInMonth(now.Year, now.Month) - now.Day + 1;
                    vm.DailyAllowance = remaining / Math.Max(1, daysLeft);
                }
            }
            if (vm.Stats.BurnPerDay > 0m && vm.Balance > 0m && vm.Stats.DaysLeft > 0 && vm.Stats.DaysLeft < 3650)
                vm.RunOutDate = DateTime.Today.AddDays(vm.Stats.DaysLeft);

            if (!vm.KeySet) vm.Mood = Mood.Offline;
            else if (!vm.HasData) vm.Mood = Mood.Offline;
            else if (vm.Error != null) vm.Mood = Mood.Offline;
            else if (vm.Balance <= 0) vm.Mood = Mood.Sad;
            else if (vm.LowBalance > 0 && vm.Balance < vm.LowBalance) vm.Mood = Mood.Worried;
            else if (vm.Stats.Today <= 0) vm.Mood = Mood.Calm;
            else vm.Mood = Mood.Happy;

            return vm;
        }

        public static string SymOf(string cur)
        {
            return string.Equals(cur, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : "¥";
        }

        static string PickCurrency(AppConfig cfg, BalanceResult r)
        {
            if (cfg.Currency == "CNY" && r.Cur.ContainsKey("CNY")) return "CNY";
            if (cfg.Currency == "USD" && r.Cur.ContainsKey("USD")) return "USD";
            if (r.Cur.ContainsKey("CNY")) return "CNY";
            if (r.Cur.ContainsKey("USD")) return "USD";
            foreach (var k in r.Cur.Keys) return k;
            return null;
        }

        static string PickCurrencyFromHistory(AppConfig cfg, HistoryStore h)
        {
            if (cfg.Currency == "CNY") return "CNY";
            if (cfg.Currency == "USD") return "USD";
            bool hasCny = false, hasUsd = false;
            foreach (var s in h.Samples)
            {
                if (s.C == "CNY") hasCny = true;
                if (s.C == "USD") hasUsd = true;
            }
            if (hasCny) return "CNY";
            if (hasUsd) return "USD";
            return null;
        }
    }
}
