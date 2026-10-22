using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace DeepSeekWhale
{
    public enum HitArea
    {
        None,
        Body,
        Pill,
        Card,
        Close,
        Refresh,
        Settings,
        Setup,
        TabBalance,
        TabUsage
    }

    public static class Geom
    {
        public const float CompactW = 168f;
        public const float CompactH = 124f;
        public const float MiniW = 112f;
        public const float MiniH = 104f;
        public const float ExpandedW = 300f;
        public const float ExpandedH = 378f;

        public static readonly RectangleF WhaleBox = new RectangleF(40f, 0f, 88f, 84f);
        public static readonly RectangleF ShadowBox = new RectangleF(44f, 76f, 80f, 8f);
        public static readonly RectangleF Pill = new RectangleF(6f, 88f, 156f, 32f);
        public static readonly RectangleF MiniWhaleBox = new RectangleF(12f, 0f, 88f, 80f);
        public static readonly RectangleF MiniText = new RectangleF(0f, 82f, 112f, 20f);
        public static readonly RectangleF Card = new RectangleF(6f, 4f, 288f, 370f);

        public static readonly RectangleF CloseBtn = new RectangleF(256f, 10f, 28f, 28f);
        public static readonly RectangleF TabBalance = new RectangleF(146f, 12f, 46f, 24f);
        public static readonly RectangleF TabUsage = new RectangleF(196f, 12f, 46f, 24f);
        public static readonly RectangleF RefreshBtn = new RectangleF(22f, 334f, 56f, 24f);
        public static readonly RectangleF SettingsBtn = new RectangleF(86f, 334f, 36f, 24f);
        public static readonly RectangleF SetupBtn = new RectangleF(84f, 238f, 132f, 30f);
    }

    public static class Palette
    {
        public static readonly Color Accent = Color.FromArgb(91, 140, 255);
        public static readonly Color Accent2 = Color.FromArgb(56, 210, 255);
        public static readonly Color Text = Color.FromArgb(233, 240, 250);
        public static readonly Color Dim = Color.FromArgb(142, 162, 194);
        public static readonly Color Dimmer = Color.FromArgb(96, 116, 148);
        public static readonly Color Green = Color.FromArgb(53, 214, 164);
        public static readonly Color Red = Color.FromArgb(255, 123, 123);
        public static readonly Color Amber = Color.FromArgb(246, 196, 83);
        public static readonly Color CardTop = Color.FromArgb(255, 22, 36, 64);
        public static readonly Color CardBottom = Color.FromArgb(255, 9, 16, 30);
        public static readonly Color Stroke = Color.FromArgb(70, 92, 140, 214);
        public static readonly Color Divider = Color.FromArgb(22, 255, 255, 255);
    }

    public static class Renderer
    {
        static readonly FontFamily Yahei;
        static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();
        static readonly StringFormat SFLeft;
        static readonly StringFormat SFRight;
        static readonly StringFormat SFCenter;

        static Renderer()
        {
            Yahei = FindFamily("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");
            SFLeft = new StringFormat(StringFormat.GenericTypographic);
            SFLeft.FormatFlags = StringFormatFlags.NoWrap;
            SFLeft.Trimming = StringTrimming.EllipsisCharacter;
            SFRight = new StringFormat(StringFormat.GenericTypographic);
            SFRight.FormatFlags = StringFormatFlags.NoWrap;
            SFRight.Alignment = StringAlignment.Far;
            SFRight.Trimming = StringTrimming.EllipsisCharacter;
            SFCenter = new StringFormat(StringFormat.GenericTypographic);
            SFCenter.Alignment = StringAlignment.Center;
            SFCenter.LineAlignment = StringAlignment.Center;
            SFCenter.FormatFlags = StringFormatFlags.NoWrap;
        }

        static FontFamily FindFamily(params string[] names)
        {
            foreach (string n in names)
            {
                try
                {
                    var f = new FontFamily(n);
                    return f;
                }
                catch { }
            }
            return FontFamily.GenericSansSerif;
        }

        public static Font F(float size, FontStyle style)
        {
            string key = size.ToString(CultureInfo.InvariantCulture) + "|" + (int)style;
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;
            f = new Font(Yahei, size, style, GraphicsUnit.Point);
            FontCache[key] = f;
            return f;
        }

        // ---------- 命中测试 ----------

        public static HitArea HitTest(PointF p, bool expanded, bool mini, bool keySet, bool hasData)
        {
            if (!expanded)
            {
                if (mini)
                {
                    if (Geom.MiniText.Contains(p)) return HitArea.Pill;
                    if (Geom.MiniWhaleBox.Contains(p)) return HitArea.Body;
                    return HitArea.None;
                }
                if (Geom.Pill.Contains(p)) return HitArea.Pill;
                if (Geom.WhaleBox.Contains(p)) return HitArea.Body;
                return HitArea.None;
            }
            if (Geom.TabBalance.Contains(p)) return HitArea.TabBalance;
            if (Geom.TabUsage.Contains(p)) return HitArea.TabUsage;
            if (Geom.CloseBtn.Contains(p)) return HitArea.Close;
            if (Geom.RefreshBtn.Contains(p)) return HitArea.Refresh;
            if (Geom.SettingsBtn.Contains(p)) return HitArea.Settings;
            if (!keySet && Geom.SetupBtn.Contains(p)) return HitArea.Setup;
            if (keySet && !hasData && Geom.SetupBtn.Contains(p)) return HitArea.Setup;
            if (Geom.Card.Contains(p)) return HitArea.Card;
            return HitArea.None;
        }

        // ---------- 紧凑形态 ----------

        public static void DrawCompact(Graphics g, Vm vm, double t, PointF hover, float react = -1f, PetPose pose = null)
        {
            bool overWhale = Geom.WhaleBox.Contains(hover);
            DrawBudgetRing(g, Geom.WhaleBox, vm, pose);
            DrawWhaleShadow(g, Geom.ShadowBox, 1f);
            DrawWhale(g, Geom.WhaleBox, vm.Mood, t, overWhale, 1f, react, pose);
            DrawPill(g, vm, t, Geom.Pill.Contains(hover));
        }

        public static void DrawMini(Graphics g, Vm vm, double t, PointF hover, float react = -1f, PetPose pose = null)
        {
            DrawBudgetRing(g, Geom.MiniWhaleBox, vm, pose);
            DrawWhaleShadow(g, new RectangleF(24f, 74f, 64f, 8f), 1f);
            DrawWhale(g, Geom.MiniWhaleBox, vm.Mood, t, Geom.MiniWhaleBox.Contains(hover), 1f, react, pose);
            string s;
            Color c;
            if (!vm.KeySet) { s = "设置 Key"; c = Palette.Accent; }
            else if (!vm.HasData) { s = vm.Error != null ? "离线" : "连接中"; c = Palette.Red; }
            else { s = vm.Sym + Fmt.Number(vm.Balance); c = vm.Balance <= 0 ? Palette.Red : Palette.Text; }
            using (var bp = Rounded(new RectangleF(10f, 81f, 92f, 20f), 10f))
            using (var bb = new SolidBrush(Color.FromArgb(130, 8, 13, 24)))
                g.FillPath(bb, bp);
            using (var b = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                g.DrawString(s, F(10f, FontStyle.Bold), b, new RectangleF(1.5f, 83.5f, 112f, 18f), SFCenter);
            using (var b = new SolidBrush(c))
                g.DrawString(s, F(10f, FontStyle.Bold), b, new RectangleF(0f, 82f, 112f, 18f), SFCenter);
        }

        static void DrawPill(Graphics g, Vm vm, double t, bool hover)
        {
            var r = Geom.Pill;
            var path = Rounded(r, 16f);
            using (var bg = new LinearGradientBrush(new PointF(r.Left, r.Top), new PointF(r.Left, r.Bottom),
                Color.FromArgb(253, 18, 29, 53), Color.FromArgb(255, 9, 16, 32)))
            {
                g.FillPath(bg, path);
            }
            Color stroke = hover ? Color.FromArgb(150, 110, 150, 240) : Color.FromArgb(80, 76, 104, 164);
            if (vm.Mood == Mood.Worried) stroke = Color.FromArgb(120, 246, 196, 83);
            if (vm.Mood == Mood.Sad) stroke = Color.FromArgb(120, 255, 123, 123);
            using (var pen = new Pen(stroke, 1f)) g.DrawPath(pen, path);

            string line1 = vm.KeySet ? vm.Sym + Fmt.Number(vm.Balance) : "还没设置 Key";
            Color c1 = vm.KeySet ? Palette.Text : Palette.Dim;
            DrawText(g, line1, F(11.5f, FontStyle.Bold), c1, r.Left + 13f, r.Top + 2f, r.Width - 44f, TextAlign.Left);

            string sub;
            Color subColor = Palette.Dim;
            if (!string.IsNullOrEmpty(vm.Message) && vm.MessageUntil > Fmt.NowUnix())
            {
                sub = vm.Message;
                subColor = vm.Mood == Mood.Worried || vm.Mood == Mood.Sad ? Palette.Amber : Palette.Accent2;
            }
            else if (vm.Refreshing)
            {
                sub = "正在读取余额…";
                subColor = Palette.Dim;
            }
            else if (!vm.KeySet)
            {
                sub = "点这里设置";
                subColor = Palette.Accent;
            }
            else if (vm.Error != null)
            {
                sub = "离线 · " + Fmt.Ago(vm.LastOk);
                subColor = Palette.Red;
            }
            else if (!vm.HasData)
            {
                sub = "还没有数据";
            }
            else
            {
                sub = "今日 " + Sign(vm.Stats.Today, vm.Sym);
                if (vm.Budget > 0)
                {
                    int pct = (int)Math.Min(999d, Math.Round((double)(vm.Stats.Month / vm.Budget) * 100.0));
                    sub += " · 预算 " + pct + "%";
                }
                subColor = vm.Stats.Today > 0 ? Palette.Red : Palette.Dim;
            }
            DrawText(g, sub, F(7.2f, FontStyle.Regular), subColor, r.Left + 13f, r.Top + 19f, r.Width - 42f, TextAlign.Left);

            // 状态点 / 勿扰月亮
            if (vm.Dnd)
            {
                DrawCrescent(g, r.Right - 25f, r.Top + 13f, 11f, Color.FromArgb(210, 190, 150, 240));
            }
            else
            {
                Color dot;
                switch (vm.Mood)
                {
                    case Mood.Happy: dot = Palette.Green; break;
                    case Mood.Calm: dot = Color.FromArgb(110, 160, 200); break;
                    case Mood.Worried: dot = Palette.Amber; break;
                    case Mood.Sad: dot = Palette.Red; break;
                    default: dot = Color.FromArgb(120, 130, 145); break;
                }
                using (var b = new SolidBrush(Color.FromArgb(45, dot))) g.FillEllipse(b, r.Right - 24f, r.Top + 15f, 9f, 9f);
                using (var b = new SolidBrush(dot)) g.FillEllipse(b, r.Right - 22f, r.Top + 17f, 5f, 5f);
            }
        }

        static void DrawCrescent(Graphics g, float x, float y, float s, Color color)
        {
            using (var path = new GraphicsPath(FillMode.Alternate))
            {
                path.AddEllipse(x, y, s, s);
                path.AddEllipse(x + s * 0.38f, y - s * 0.10f, s * 0.92f, s * 0.92f);
                using (var b = new SolidBrush(color)) g.FillPath(b, path);
            }
        }

        // ---------- 展开形态 ----------

        public static void DrawExpanded(Graphics g, Vm vm, double t, PointF hover, PetPose pose = null, bool usageTab = false)
        {
            DrawCard(g, Geom.Card);

            // 顶部
            DrawWhale(g, new RectangleF(16f, 12f, 34f, 34f), vm.KeySet ? vm.Mood : Mood.Calm, t, false, 1f, -1f, pose);
            DrawText(g, "DeepSeek", F(9f, FontStyle.Regular), Palette.Dim, 56f, 18f, 86f, TextAlign.Left);

            DrawTab(g, Geom.TabBalance, "余额", !usageTab, Geom.TabBalance.Contains(hover));
            DrawTab(g, Geom.TabUsage, "用量", usageTab, Geom.TabUsage.Contains(hover));

            bool closeHot = Geom.CloseBtn.Contains(hover);
            if (closeHot)
            {
                using (var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                    g.FillEllipse(b, Geom.CloseBtn.X + 4f, Geom.CloseBtn.Y + 4f, 20f, 20f);
            }
            using (var pen = new Pen(closeHot ? Palette.Text : Palette.Dimmer, 1.4f))
            {
                float ix = Geom.CloseBtn.X + 14f, iy = Geom.CloseBtn.Y + 14f;
                g.DrawLine(pen, ix - 4.5f, iy - 4.5f, ix + 4.5f, iy + 4.5f);
                g.DrawLine(pen, ix + 4.5f, iy - 4.5f, ix - 4.5f, iy + 4.5f);
            }

            if (usageTab)
            {
                DrawUsageTab(g, vm, t, hover, pose);
                return;
            }

            if (!vm.KeySet)
            {
                DrawOnboarding(g, vm, hover, pose);
                return;
            }

            if (!vm.HasData)
            {
                DrawProblem(g, vm, hover, pose);
                return;
            }

            // 余额
            string bal = vm.HasData ? vm.Sym + Fmt.Number(vm.Balance) : "--";
            DrawText(g, bal, F(25f, FontStyle.Bold), Palette.Text, 20f, 42f, 190f, TextAlign.Left);

            if (vm.HasData)
            {
                var chip = new RectangleF(218f, 50f, 62f, 22f);
                Color chipColor = StratoColor(vm);
                var cp = Rounded(chip, 11f);
                using (var b = new SolidBrush(Color.FromArgb(38, chipColor))) g.FillPath(b, cp);
                DrawingTextC(g, vm.MoodText, F(8f, FontStyle.Regular), chipColor, chip);
            }

            string subLine;
            if (vm.HasData)
            {
                subLine = "赠金 " + vm.Sym + Fmt.Number(vm.Granted) + "  ·  充值 " + vm.Sym + Fmt.Number(vm.Topped);
                if (vm.Budget > 0)
                {
                    int pct = (int)Math.Min(999d, Math.Round((double)(vm.Stats.Month / vm.Budget) * 100.0));
                    subLine += "  ·  本月 " + pct + "%";
                }
            }
            else
            {
                subLine = vm.Error == null ? "正在连接…" : vm.Error;
            }
            DrawText(g, subLine, F(7.5f, FontStyle.Regular), vm.Error != null ? Palette.Red : Palette.Dim, 22f, 84f, 256f, TextAlign.Left);

            float y = 96f;
            if (vm.Budget > 0 && vm.HasData)
            {
                var track = new RectangleF(22f, y + 4f, 256f, 4f);
                var tp = Rounded(track, 2f);
                using (var b = new SolidBrush(Color.FromArgb(40, 120, 150, 210))) g.FillPath(b, tp);
                double frac = (double)(vm.Stats.Month / vm.Budget);
                if (frac > 1) frac = 1;
                if (frac > 0)
                {
                    var fill = new RectangleF(track.X, track.Y, (float)(track.Width * frac), track.Height);
                    var fp = Rounded(fill, 2f);
                    using (var b = new LinearGradientBrush(track, Palette.Accent, frac > 0.9 ? Palette.Red : Palette.Accent2, 0f))
                        g.FillPath(b, fp);
                }
                y += 14f;
            }

            Divider(g, 20f, 280f, y + 8f);

            // 统计
            float sy = y + 16f;
            bool measured = vm.HasData && vm.Stats.SampleCount >= 2;
            DrawStat(g, 20f, sy, "今日消耗", measured ? Sign(vm.Stats.Today, vm.Sym) : "--",
                measured && vm.Stats.Today > 0 ? Palette.Red : (measured ? Palette.Text : Palette.Dimmer));
            DrawStat(g, 86f, sy, "近 7 天", measured ? Sign(vm.Stats.Week, vm.Sym) : "--",
                measured ? Palette.Text : Palette.Dimmer);
            DrawStat(g, 152f, sy, "日均", measured ? Sign(vm.Stats.BurnPerDay, vm.Sym) : "--",
                measured ? Palette.Text : Palette.Dimmer);
            string daysText;
            Color daysColor;
            if (!vm.HasData) { daysText = "--"; daysColor = Palette.Dimmer; }
            else if (vm.Balance <= 0) { daysText = "0 天"; daysColor = Palette.Red; }
            else if (!measured) { daysText = "--"; daysColor = Palette.Dimmer; }
            else if (vm.Stats.BurnPerDay <= 0 || vm.Stats.DaysLeft < 0) { daysText = "长期"; daysColor = Palette.Green; }
            else
            {
                daysText = Fmt.Days(vm.Stats.DaysLeft);
                daysColor = vm.Stats.DaysLeft < 7 ? Palette.Red : (vm.Stats.DaysLeft < 30 ? Palette.Amber : Palette.Green);
            }
            DrawStat(g, 218f, sy, "预计可用", daysText, daysColor);

            Divider(g, 20f, 280f, sy + 50f);

            // 图表
            float cy = sy + 58f;
            DrawText(g, "近 30 天消耗", F(8f, FontStyle.Regular), Palette.Dim, 22f, cy, 120f, TextAlign.Left);
            if (vm.HasData && vm.Stats.MaxDaily > 0)
                DrawText(g, "峰值 " + vm.Sym + Fmt.Number(vm.Stats.MaxDaily), F(7.2f, FontStyle.Regular), Palette.Dimmer, 180f, cy + 1f, 98f, TextAlign.Right);
            DrawChart(g, new RectangleF(22f, cy + 16f, 256f, 62f), vm);

            DrawText(g, "30 天前", F(6.8f, FontStyle.Regular), Palette.Dimmer, 22f, cy + 82f, 60f, TextAlign.Left);
            DrawText(g, "今天", F(6.8f, FontStyle.Regular), Palette.Dimmer, 218f, cy + 82f, 60f, TextAlign.Right);

            Divider(g, 20f, 280f, cy + 96f);

            // 充值
            float ty = cy + 104f;
            if (vm.Topups.Count == 0) ty = cy + 116f;
            DrawText(g, "最近充值", F(8f, FontStyle.Regular), Palette.Dim, 22f, ty, 80f, TextAlign.Left);
            if (vm.Topups.Count == 0)
            {
                DrawText(g, "还没有记录，充值后会自动识别", F(7.2f, FontStyle.Regular), Palette.Dimmer, 90f, ty + 1f, 190f, TextAlign.Left);
            }
            else
            {
                for (int i = 0; i < Math.Min(2, vm.Topups.Count); i++)
                {
                    var e = vm.Topups[i];
                    DrawText(g, Fmt.ShortDate(e.T), F(7.2f, FontStyle.Regular), Palette.Dimmer, 22f, ty + 12f + i * 13f, 40f, TextAlign.Left);
                    DrawText(g, "+" + vm.Sym + Fmt.Number(e.Amount), F(7.5f, FontStyle.Regular), Palette.Green, 62f, ty + 12f + i * 13f, 90f, TextAlign.Left);
                }
            }

            // 底部
            Divider(g, 20f, 280f, 330f);
            string updated = vm.HasData
                ? "更新于 " + Fmt.Clock(vm.LastOk) + " · " + (vm.PeakNow ? "高峰" : "闲时")
                : "还没成功读取过";
            DrawText(g, updated, F(7.2f, FontStyle.Regular), Palette.Dimmer, 140f, 341f, 138f, TextAlign.Right);
            DrawTextButton(g, "立即刷新", Geom.RefreshBtn, Geom.RefreshBtn.Contains(hover), vm.Refreshing);
            DrawTextButton(g, "设置", Geom.SettingsBtn, Geom.SettingsBtn.Contains(hover), false);
        }

        static void DrawTab(Graphics g, RectangleF r, string text, bool active, bool hover)
        {
            var p = Rounded(r, 11f);
            if (active)
            {
                using (var b = new SolidBrush(Color.FromArgb(46, Palette.Accent))) g.FillPath(b, p);
            }
            else if (hover)
            {
                using (var b = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillPath(b, p);
            }
            DrawingTextC(g, text, F(7.6f, active ? FontStyle.Bold : FontStyle.Regular),
                active ? Palette.Text : Palette.Dim, r);
        }

        static void DrawChip(Graphics g, RectangleF r, string text, Color color)
        {
            var p = Rounded(r, 9f);
            using (var b = new SolidBrush(Color.FromArgb(34, color))) g.FillPath(b, p);
            DrawingTextC(g, text, F(6.8f, FontStyle.Regular), color, r);
        }

        static string Yuan(decimal v)
        {
            return "¥" + Fmt.Number(v);
        }

        static string YuanCost(decimal v)
        {
            if (v >= 0.01m) return "¥" + v.ToString("0.00");
            if (v > 0m) return "¥" + v.ToString("0.0000");
            return "¥0.00";
        }

        static string When(DateTime d)
        {
            if (d == DateTime.MinValue) return "--";
            return d.Year == DateTime.Today.Year ? d.ToString("M/d") : d.ToString("yyyy/M/d");
        }

        static void DrawUsageTab(Graphics g, Vm vm, double t, PointF hover, PetPose pose)
        {
            var dsh = vm.Dsh;
            if (dsh == null)
            {
                DrawUsageEmpty(g, vm, hover, pose, "正在读取 DSH 用量…", "每 20 秒读一次本地会话缓存");
                return;
            }
            if (!dsh.Available || dsh.Current == null)
            {
                DrawUsageEmpty(g, vm, hover, pose,
                    dsh.Available ? "还没有 DSH 用量" : "没有找到 DSH 会话数据",
                    dsh.Error ?? "在装了 DeepSeek Harness 的机器上才会显示");
                return;
            }

            var cur = dsh.Current;
            DrawText(g, dsh.Pinned ? "当前查看" : "最近活跃", F(7f, FontStyle.Regular), Palette.Dimmer, 22f, 50f, 54f, TextAlign.Left);
            DrawText(g, Fmt.Truncate(string.IsNullOrEmpty(cur.Title) ? "未命名会话" : cur.Title, 20),
                F(7.6f, FontStyle.Regular), Palette.Dim, 78f, 49f, 200f, TextAlign.Left);

            DrawText(g, YuanCost(cur.Cost), F(18f, FontStyle.Bold), Palette.Text, 20f, 62f, 150f, TextAlign.Left);

            DrawText(g, Fmt.Tokens(cur.Total) + " tokens  ·  " + cur.Turns + " 轮  ·  " + cur.Steps + " 步",
                F(7f, FontStyle.Regular), Palette.Dim, 22f, 90f, 256f, TextAlign.Left);

            // 缓存、峰谷信息全部左对齐，避免右侧竖着堆一列
            DrawText(g, cur.CacheHitPct > 0 ? "缓存 " + cur.CacheHitPct.ToString("0.0") + "%" : "-- 缓存",
                F(6.8f, FontStyle.Regular), Palette.Green, 22f, 102f, 70f, TextAlign.Left);
            DrawText(g, "·", F(6.8f, FontStyle.Regular), Palette.Dimmer, 92f, 102f, 8f, TextAlign.Left);
            DrawText(g, vm.PeakChip, F(6.8f, FontStyle.Regular), vm.PeakNow ? Palette.Amber : Palette.Green,
                100f, 102f, 58f, TextAlign.Left);
            if (!string.IsNullOrEmpty(vm.PeakHint))
            {
                DrawText(g, "·", F(6.8f, FontStyle.Regular), Palette.Dimmer, 158f, 102f, 8f, TextAlign.Left);
                DrawText(g, vm.PeakHint, F(6.8f, FontStyle.Regular), Palette.Dimmer, 166f, 102f, 112f, TextAlign.Left);
            }

            if (cur.SurfaceTokens > 0 && cur.ContextWindow > 0)
            {
                var track = new RectangleF(22f, 118f, 180f, 4f);
                var tp = Rounded(track, 2f);
                using (var b = new SolidBrush(Color.FromArgb(40, 120, 150, 210))) g.FillPath(b, tp);
                double frac = Math.Min(1.0, cur.ContextPct / 100.0);
                if (frac > 0)
                {
                    Color cc = cur.ContextPct >= 80 ? Palette.Red : (cur.ContextPct >= 60 ? Palette.Amber : Palette.Accent2);
                    var fp = Rounded(new RectangleF(track.X, track.Y, (float)(track.Width * frac), track.Height), 2f);
                    using (var b = new SolidBrush(cc)) g.FillPath(b, fp);
                }
                DrawText(g, "上下文 " + cur.ContextPct.ToString("0") + "%", F(6.6f, FontStyle.Regular), Palette.Dimmer,
                    22f, 126f, 100f, TextAlign.Left);
            }
            if (cur.Turns > 0)
            {
                DrawText(g, "≈" + YuanCost(cur.Cost / cur.Turns) + "/轮", F(6.6f, FontStyle.Regular), Palette.Dimmer,
                    160f, 126f, 118f, TextAlign.Right);
            }

            Divider(g, 20f, 280f, 144f);
            DrawStat(g, 20f, 152f, "今日 DSH", "¥" + Fmt.Compact(dsh.TodayCost), dsh.TodayCost > 0 ? Palette.Red : Palette.Text);
            DrawStat(g, 106f, 152f, "近 7 天", "¥" + Fmt.Compact(dsh.WeekCost), Palette.Text);
            DrawStat(g, 192f, 152f, "缓存节省", "¥" + Fmt.Compact(dsh.CacheSavings), Palette.Green);
            DrawText(g, "缓存节省按「未命中价 − 命中价」估算", F(6.2f, FontStyle.Regular), Palette.Dimmer,
                22f, 180f, 256f, TextAlign.Left);

            Divider(g, 20f, 280f, 198f);
            DrawText(g, "其他会话", F(7.6f, FontStyle.Regular), Palette.Dim, 22f, 206f, 100f, TextAlign.Left);
            DrawText(g, dsh.Pinned ? "恢复最近" : "点一下切换", F(6.2f, FontStyle.Regular),
                dsh.Pinned ? Palette.Accent : Palette.Dimmer, 196f, 207f, 82f, TextAlign.Right);
            int shown = 0;
            for (int i = 0; i < dsh.Sessions.Count && shown < 3; i++)
            {
                var s = dsh.Sessions[i];
                if (s.Total <= 0 || s == cur) continue;
                float ry = 218f + shown * 16f;
                var row = new RectangleF(18f, ry - 2f, 264f, 15f);
                if (row.Contains(hover))
                {
                   using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                        g.FillPath(b, Rounded(row, 6f));
                }
                DrawText(g, Fmt.Truncate(string.IsNullOrEmpty(s.Title) ? "未命名会话" : s.Title, 18),
                    F(7.2f, FontStyle.Regular), Palette.Dim, 22f, ry, 168f, TextAlign.Left);
                DrawText(g, YuanCost(s.Cost), F(7.2f, FontStyle.Regular), Palette.Dimmer, 200f, ry, 78f, TextAlign.Right);
                shown++;
            }
            if (shown == 0)
                DrawText(g, "没有其他会话", F(7f, FontStyle.Regular), Palette.Dimmer, 22f, 218f, 200f, TextAlign.Left);

            Divider(g, 20f, 280f, 264f);
            DrawText(g, "预算节奏", F(7.6f, FontStyle.Regular), Palette.Dim, 22f, 272f, 100f, TextAlign.Left);
            if (vm.Budget > 0)
            {
                int pct = (int)Math.Min(999d, Math.Round((double)(vm.MonthSpend / vm.Budget) * 100.0));
                DrawText(g, "本月余额消耗 " + Yuan(vm.MonthSpend) + " / " + Yuan(vm.Budget) + "  ·  " + pct + "%",
                    F(7.2f, FontStyle.Regular), vm.OverBudget ? Palette.Red : Palette.Dim, 22f, 286f, 256f, TextAlign.Left);
                string line2;
                Color c2 = Palette.Dimmer;
                if (vm.OverBudget)
                {
                    line2 = "已经超出预算 " + Yuan(vm.MonthSpend - vm.Budget);
                    c2 = Palette.Red;
                }
                else if (vm.RunOutDate != DateTime.MinValue)
                {
                    line2 = "每天可花 " + Yuan(vm.DailyAllowance) + "  ·  余额预计 " + When(vm.RunOutDate) + " 用完";
                }
                else
                {
                    line2 = "每天可花 " + Yuan(vm.DailyAllowance);
                }
                DrawText(g, line2, F(7f, FontStyle.Regular), c2, 22f, 300f, 256f, TextAlign.Left);
            }
            else
            {
                DrawText(g, "本月 DSH " + Yuan(dsh.MonthCost) + "  ·  设个月预算可看每日额度",
                    F(7.2f, FontStyle.Regular), Palette.Dim, 22f, 286f, 256f, TextAlign.Left);
                DrawText(g, vm.RunOutDate != DateTime.MinValue
                        ? "按余额速度，余额预计 " + When(vm.RunOutDate) + " 用完"
                        : "余额充足，暂时算不出用完日期",
                    F(7f, FontStyle.Regular), Palette.Dimmer, 22f, 300f, 256f, TextAlign.Left);
            }

            if (vm.HasData && (vm.Stats.Today > 0 || dsh.TodayCost > 0))
            {
                decimal diff = Math.Abs(vm.Stats.Today - dsh.TodayCost);
                bool big = diff > Math.Max(2m, Math.Max(vm.Stats.Today, dsh.TodayCost) * 0.4m);
                string recon = "今日对照  余额 -" + Fmt.Number(vm.Stats.Today)
                    + "  ·  DSH 估算 " + Fmt.Number(dsh.TodayCost)
                    + "  ·  差 " + Fmt.Number(diff);
                DrawText(g, recon, F(6.3f, FontStyle.Regular), big ? Palette.Amber : Palette.Dimmer, 22f, 316f, 256f, TextAlign.Left);
            }
            else
            {
                DrawText(g, "今日对照  还没有可对比的余额/DSH 数据", F(6.6f, FontStyle.Regular), Palette.Dimmer,
                    22f, 316f, 256f, TextAlign.Left);
            }

            Divider(g, 20f, 280f, 330f);
            DrawText(g, "DSH 本地会话 · 估算", F(7f, FontStyle.Regular), Palette.Dimmer, 140f, 341f, 138f, TextAlign.Right);
            DrawTextButton(g, "立即刷新", Geom.RefreshBtn, Geom.RefreshBtn.Contains(hover), vm.Refreshing);
            DrawTextButton(g, "设置", Geom.SettingsBtn, Geom.SettingsBtn.Contains(hover), false);
        }

        static void DrawUsageEmpty(Graphics g, Vm vm, PointF hover, PetPose pose, string title, string detail)
        {
            DrawWhale(g, new RectangleF(92f, 64f, 116f, 108f), Mood.Offline, 1.05, false, 1f, -1f, pose);
            DrawingTextC(g, title, F(11f, FontStyle.Bold), Palette.Text, new RectangleF(20f, 190f, 260f, 22f));
            DrawingTextC(g, Fmt.Truncate(detail, 32), F(7.4f, FontStyle.Regular), Palette.Dim,
                new RectangleF(20f, 214f, 260f, 18f));
            DrawingTextC(g, "只读本地缓存，不会上传会话内容", F(6.8f, FontStyle.Regular), Palette.Dimmer,
                new RectangleF(20f, 232f, 260f, 16f));
            Divider(g, 20f, 280f, 330f);
            DrawText(g, "DSH 本地会话 · 估算", F(7f, FontStyle.Regular), Palette.Dimmer, 140f, 341f, 138f, TextAlign.Right);
            DrawTextButton(g, "立即刷新", Geom.RefreshBtn, Geom.RefreshBtn.Contains(hover), vm.Refreshing);
            DrawTextButton(g, "设置", Geom.SettingsBtn, Geom.SettingsBtn.Contains(hover), false);
        }

        static void DrawOnboarding(Graphics g, Vm vm, PointF hover, PetPose pose)
        {
            DrawWhale(g, new RectangleF(92f, 58f, 116f, 108f), Mood.Calm, 1.1, false, 1f, -1f, pose);
            DrawingTextC(g, "先把 API Key 填进来", F(12f, FontStyle.Bold), Palette.Text, new RectangleF(20f, 188f, 260f, 24f));
            DrawingTextC(g, "只保存在这台电脑上，用 Windows 加密", F(7.5f, FontStyle.Regular), Palette.Dim, new RectangleF(20f, 210f, 260f, 18f));

            bool hot = Geom.SetupBtn.Contains(hover);
            var bp = Rounded(Geom.SetupBtn, 10f);
            using (var b = new LinearGradientBrush(Geom.SetupBtn, hot ? Color.FromArgb(255, 108, 152, 255) : Palette.Accent,
                hot ? Color.FromArgb(255, 70, 110, 235) : Color.FromArgb(255, 62, 100, 220), 0f))
                g.FillPath(b, bp);
            DrawingTextC(g, "去设置", F(9f, FontStyle.Bold), Color.White, Geom.SetupBtn);

            DrawingTextC(g, "platform.deepseek.com  →  API keys", F(6.8f, FontStyle.Regular), Palette.Dimmer,
                new RectangleF(20f, 274f, 260f, 16f));

            DrawText(g, "· 每 5 分钟自动刷新余额", F(7f, FontStyle.Regular), Palette.Dimmer, 74f, 306f, 170f, TextAlign.Left);
            DrawText(g, "· 充值到账会自动识别", F(7f, FontStyle.Regular), Palette.Dimmer, 74f, 322f, 170f, TextAlign.Left);
            DrawText(g, "· 消耗与历史只存本机", F(7f, FontStyle.Regular), Palette.Dimmer, 74f, 338f, 170f, TextAlign.Left);
        }

        static void DrawProblem(Graphics g, Vm vm, PointF hover, PetPose pose)
        {
            DrawWhale(g, new RectangleF(92f, 62f, 116f, 108f), Mood.Offline, 1.1, false, 1f, -1f, pose);
            string title = vm.Refreshing ? "正在读取余额…" : "暂时拿不到余额";
            DrawingTextC(g, title, F(11.5f, FontStyle.Bold), Palette.Text, new RectangleF(20f, 190f, 260f, 24f));
            string detail = vm.Error != null ? vm.Error : "正在连接 api.deepseek.com";
            DrawingTextC(g, Fmt.Truncate(detail, 34), F(7.5f, FontStyle.Regular),
                vm.Error != null ? Palette.Red : Palette.Dim, new RectangleF(20f, 214f, 260f, 18f));

            bool hot = Geom.SetupBtn.Contains(hover);
            var bp = Rounded(Geom.SetupBtn, 10f);
            using (var b = new LinearGradientBrush(Geom.SetupBtn, hot ? Color.FromArgb(255, 108, 152, 255) : Palette.Accent,
                hot ? Color.FromArgb(255, 70, 110, 235) : Color.FromArgb(255, 62, 100, 220), 0f))
                g.FillPath(b, bp);
            DrawingTextC(g, vm.Refreshing ? "读取中…" : "重试", F(9f, FontStyle.Bold), Color.White, Geom.SetupBtn);

            DrawingTextC(g, "检查网络连接，或在设置里确认 Key 是否有效", F(6.8f, FontStyle.Regular), Palette.Dimmer,
                new RectangleF(20f, 280f, 260f, 16f));
        }

        // ---------- 卡片 ----------

        static void DrawCard(Graphics g, RectangleF r)
        {
            for (int i = 4; i >= 1; i--)
            {
                var sr = new RectangleF(r.X - i, r.Y + 2f - i * 0.5f, r.Width + i * 2f, r.Height + i * 2f);
                var sp = Rounded(sr, 20f + i);
                using (var b = new SolidBrush(Color.FromArgb(9, 0, 0, 0))) g.FillPath(b, sp);
            }

            var path = Rounded(r, 20f);
            using (var b = new LinearGradientBrush(new PointF(r.Left, r.Top), new PointF(r.Right, r.Bottom), Palette.CardTop, Palette.CardBottom))
                g.FillPath(b, path);
            using (var p = new Pen(Palette.Stroke, 1f)) g.DrawPath(p, path);

            // 左上角柔光
            using (var b = new PathGradientBrush(Rounded(new RectangleF(r.X - 30f, r.Y - 40f, 220f, 160f), 80f)))
            {
                b.CenterColor = Color.FromArgb(26, 70, 120, 240);
                b.SurroundColors = new Color[] { Color.Transparent };
                g.FillRectangle(b, new RectangleF(r.X + 2f, r.Y + 2f, r.Width - 4f, r.Height - 4f));
            }
        }

        static void DrawStat(Graphics g, float x, float y, string label, string value, Color color)
        {
            DrawText(g, label, F(7f, FontStyle.Regular), Palette.Dimmer, x, y, 62f, TextAlign.Left);
            DrawText(g, value, F(10f, FontStyle.Bold), color, x, y + 13f, 64f, TextAlign.Left);
        }

        static void DrawChart(Graphics g, RectangleF r, Vm vm)
        {
            using (var pen = new Pen(Color.FromArgb(26, 140, 170, 220), 1f))
                g.DrawLine(pen, r.Left, r.Bottom, r.Right, r.Bottom);
            using (var pen = new Pen(Color.FromArgb(16, 140, 170, 220), 1f))
            {
                g.DrawLine(pen, r.Left, r.Top + r.Height * 0.5f, r.Right, r.Top + r.Height * 0.5f);
                g.DrawLine(pen, r.Left, r.Top, r.Right, r.Top);
            }

            decimal max = vm.Stats.MaxDaily;
            if (max <= 0)
            {
                DrawingTextC(g, "用起来之后，这里会长出消耗曲线", F(7.2f, FontStyle.Regular), Palette.Dimmer,
                    new RectangleF(r.Left, r.Top + 20f, r.Width, 20f));
                return;
            }
            float gap = 2.1f;
            float bw = (r.Width - gap * 29f) / 30f;
            if (bw < 2f) bw = 2f;
            for (int i = 0; i < 30; i++)
            {
                decimal v = vm.Stats.Daily[29 - i]; // i=0 → 今天
                float h = (float)(v / max) * (r.Height - 3f);
                if (v > 0 && h < 2f) h = 2f;
                float x = r.Right - (i + 1) * (bw + gap) + gap;
                var bar = new RectangleF(x, r.Bottom - h, bw, h);
                Color top;
                if (i == 0) top = Palette.Accent2;
                else top = Color.FromArgb(220, 91, 140, 255);
                if (v <= 0)
                {
                    using (var b = new SolidBrush(Color.FromArgb(16, 140, 165, 210)))
                        g.FillRectangle(b, new RectangleF(x, r.Bottom - 1.5f, bw, 1.5f));
                    continue;
                }
                var path = Rounded(bar, Math.Min(2.2f, bw / 2f));
                using (var b = new LinearGradientBrush(new PointF(bar.Left, bar.Top), new PointF(bar.Left, bar.Bottom),
                    Color.FromArgb(250, top), Color.FromArgb(170, top.R / 2, top.G / 2, top.B)))
                {
                    g.FillPath(b, path);
                }
                path.Dispose();
            }

            if (vm.Stats.MaxDaily <= 0)
            {
                DrawingTextC(g, "用起来之后，这里会长出消耗曲线", F(7.2f, FontStyle.Regular), Palette.Dimmer,
                    new RectangleF(r.Left, r.Top + 20f, r.Width, 20f));
            }
        }

        static void Divider(Graphics g, float x1, float x2, float y)
        {
            using (var p = new Pen(Palette.Divider, 1f)) g.DrawLine(p, x1, y, x2, y);
        }

        static void DrawTextButton(Graphics g, string text, RectangleF r, bool hot, bool busy)
        {
            Color bg = hot ? Color.FromArgb(34, 140, 175, 255) : Color.FromArgb(14, 140, 175, 255);
            using (var b = new SolidBrush(bg))
                g.FillPath(b, Rounded(new RectangleF(r.X, r.Y + 1f, r.Width, r.Height - 2f), 7f));
            Color c = busy ? Palette.Dimmer : (hot ? Palette.Text : Palette.Accent);
            DrawingTextC(g, busy ? "刷新中…" : text, F(7.6f, FontStyle.Regular), c, r);
        }

        // ---------- 小鲸鱼 ----------

        static void SkinColors(PetSkin skin, bool calm, out Color top, out Color bot, out Color belly, out Color fin, out Color tail)
        {
            if (skin == PetSkin.Sakura)
            {
                top = Color.FromArgb(255, 255, 168, 196); bot = Color.FromArgb(255, 226, 92, 138);
                belly = Color.FromArgb(235, 255, 240, 246); fin = Color.FromArgb(255, 236, 110, 150); tail = Color.FromArgb(255, 250, 145, 178);
            }
            else if (skin == PetSkin.Mint)
            {
                top = Color.FromArgb(255, 126, 226, 196); bot = Color.FromArgb(255, 42, 170, 140);
                belly = Color.FromArgb(235, 232, 255, 248); fin = Color.FromArgb(255, 60, 190, 160); tail = Color.FromArgb(255, 110, 214, 184);
            }
            else if (calm)
            {
                top = Color.FromArgb(255, 96, 138, 240); bot = Color.FromArgb(255, 58, 88, 210);
                belly = Color.FromArgb(220, 214, 230, 252); fin = Color.FromArgb(255, 64, 96, 200); tail = Color.FromArgb(255, 80, 118, 226);
            }
            else
            {
                top = Color.FromArgb(255, 104, 152, 255); bot = Color.FromArgb(255, 52, 82, 232);
                belly = Color.FromArgb(235, 224, 238, 255); fin = Color.FromArgb(255, 64, 104, 226); tail = Color.FromArgb(255, 88, 132, 250);
            }
        }

        static void ApplyPose(Graphics g, PetPose pose)
        {
            if (pose == null) return;
            float cx = 50f, cy = 52f;
            g.TranslateTransform(cx, cy);
            if (pose.Action == PetAction.Stretch)
            {
                float k = (float)Math.Sin(pose.ActionT * Math.PI);
                g.ScaleTransform(1f + 0.10f * k, 1f - 0.07f * k);
            }
            else if (pose.Action == PetAction.Spin)
            {
                g.RotateTransform(360f * Ease(pose.ActionT));
            }
            else if (pose.Action == PetAction.Jump)
            {
                g.TranslateTransform(0f, -20f * (float)Math.Sin(pose.ActionT * Math.PI));
            }
            else if (pose.Action == PetAction.Yawn)
            {
                g.RotateTransform(-4f * (float)Math.Sin(pose.ActionT * Math.PI));
            }
            else if (pose.Action == PetAction.Shoot)
            {
                float p = pose.ActionT;
                float puff = (float)Math.Sin(Math.Min(1f, p * 3f) * Math.PI * 0.5f);
                float recoil = p < 0.35f ? 0f : (float)Math.Sin(Math.Min(1f, (p - 0.35f) / 0.65f) * Math.PI);
                g.ScaleTransform(1f + 0.09f * puff, 1f - 0.05f * puff);
                g.RotateTransform(-4f * recoil);
            }
            if (pose.Dragging) g.RotateTransform(pose.DragTilt);
            if (pose.Walking)
                g.RotateTransform(6f * (float)Math.Sin(pose.WalkPhase * Math.PI * 2.0) * (pose.Facing < 0 ? -1f : 1f));
            if (pose.Facing < 0) g.ScaleTransform(-1f, 1f);
            if (pose.DropT >= 0f && pose.DropT <= 1f)
            {
                float p = pose.DropT;
                g.TranslateTransform(0f, -14f * (float)Math.Sin(p * Math.PI));
                float sy = 1f - 0.08f * (float)Math.Sin(p * Math.PI * 2f);
                g.ScaleTransform(1f + (1f - sy) * 0.5f, sy);
            }
            g.TranslateTransform(-cx, -cy);
        }

        static float Ease(float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        static void DrawNightCap(Graphics g)
        {
            var cap = new GraphicsPath();
            cap.AddBezier(40f, 23f, 46f, 9f, 62f, 5f, 74f, 12f);
            cap.AddBezier(74f, 12f, 80f, 15f, 82f, 20f, 78f, 24f);
            cap.AddLine(78f, 24f, 44f, 24f);
            cap.CloseFigure();
            using (var b = new LinearGradientBrush(new PointF(45, 8), new PointF(70, 24), Color.FromArgb(255, 108, 128, 255), Color.FromArgb(255, 64, 84, 200)))
                g.FillPath(b, cap);
            using (var p = new Pen(Color.FromArgb(255, 240, 244, 255), 1.2f)) g.DrawPath(p, cap);
            using (var b = new SolidBrush(Color.FromArgb(255, 240, 244, 255))) g.FillRectangle(b, 42f, 20f, 36f, 3f);
            using (var b = new SolidBrush(Color.FromArgb(255, 255, 214, 120))) g.FillEllipse(b, 74f, 8f, 7f, 7f);
            using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, 76f, 10f, 2.4f, 2.4f);
            cap.Dispose();
        }

        public static void DrawWhaleShadow(Graphics g, RectangleF r, float opacity)
        {
            for (int i = 3; i >= 0; i--)
            {
                float pad = i * 2.2f;
                var rr = new RectangleF(r.X - pad, r.Y - pad * 0.35f, r.Width + pad * 2f, r.Height + pad * 0.7f);
                using (var b = new SolidBrush(Color.FromArgb((int)((4 + i * 3) * opacity), 0, 0, 0)))
                    g.FillEllipse(b, rr);
            }
        }

        public static void DrawWhale(Graphics g, RectangleF box, Mood mood, double t, bool hover, float opacity, float react = -1f, PetPose pose = null)
        {
            if (pose != null && pose.React >= 0f) react = pose.React;
            bool sleeping = pose != null && pose.Sleeping;
            float scale = Math.Min(box.Width, box.Height) / 100f;
            float bob = 0f;
            if (box.Height > 50f) bob = (float)Math.Sin(t * (sleeping ? 0.8 : 1.6)) * (sleeping ? 1.3f : 2.2f);
            if (pose != null && pose.Walking)
                bob -= (float)Math.Abs(Math.Sin(pose.WalkPhase * Math.PI * 2.0)) * 3.2f;
            if (react >= 0f && react <= 1f) bob -= (float)Math.Sin(react * Math.PI) * 3.2f;
            float dx = box.X + (box.Width - 100f * scale) / 2f;
            float dy = box.Y + (box.Height - 100f * scale) / 2f + bob;
            if (hover && box.Height > 50f) scale *= 1.02f;

            var state = g.Save();
            g.TranslateTransform(dx, dy);
            g.ScaleTransform(scale, scale);
            ApplyPose(g, pose);

            Color top, bot, belly, fin, tail;
            if (mood == Mood.Happy || mood == Mood.Calm)
            {
                SkinColors(pose != null ? pose.Skin : PetSkin.DeepBlue, mood == Mood.Calm, out top, out bot, out belly, out fin, out tail);
            }
            else
            {
                switch (mood)
                {
                    case Mood.Worried:
                        top = Color.FromArgb(255, 128, 142, 216); bot = Color.FromArgb(255, 78, 92, 160);
                        belly = Color.FromArgb(210, 208, 218, 240); fin = Color.FromArgb(255, 84, 96, 170); tail = Color.FromArgb(255, 104, 118, 190);
                        break;
                    case Mood.Sad:
                        top = Color.FromArgb(255, 122, 132, 154); bot = Color.FromArgb(255, 74, 82, 100);
                        belly = Color.FromArgb(190, 190, 198, 214); fin = Color.FromArgb(255, 82, 90, 110); tail = Color.FromArgb(255, 100, 110, 132);
                        break;
                    default:
                        top = Color.FromArgb(255, 112, 120, 136); bot = Color.FromArgb(255, 68, 74, 88);
                        belly = Color.FromArgb(170, 180, 188, 202); fin = Color.FromArgb(255, 78, 84, 98); tail = Color.FromArgb(255, 92, 100, 116);
                        break;
                }
            }

            bool blink = mood != Mood.Offline && mood != Mood.Sad && ((t + 1.7) % 4.3) < 0.13;

            // 走路的小脚和尘土
            if (pose != null && pose.Walking) DrawFeet(g, pose.WalkPhase, fin);

            // 尾巴
            using (var b = new LinearGradientBrush(new PointF(0, 20), new PointF(0, 95), tail, fin))
            {
                var t1 = new GraphicsPath();
                t1.AddBezier(31f, 52f, 20f, 48f, 12f, 38f, 8f, 27f);
                t1.AddBezier(8f, 27f, 6f, 21f, 13f, 18f, 17f, 24f);
                t1.AddBezier(17f, 24f, 21f, 32f, 25f, 38f, 32f, 43f);
                t1.CloseFigure();
                var t2 = new GraphicsPath();
                t2.AddBezier(32f, 60f, 24f, 66f, 18f, 75f, 16f, 84f);
                t2.AddBezier(16f, 84f, 15f, 90f, 22f, 91f, 25f, 85f);
                t2.AddBezier(25f, 85f, 28f, 77f, 31f, 70f, 35f, 65f);
                t2.CloseFigure();
                g.FillPath(b, t1);
                g.FillPath(b, t2);
                t1.Dispose();
                t2.Dispose();
            }

            // 身体
            var body = BodyPath();
            using (var b = new LinearGradientBrush(new PointF(40, 20), new PointF(60, 88), top, bot))
                g.FillPath(b, body);
            using (var p = new Pen(Color.FromArgb(70, 255, 255, 255), 1.1f)) g.DrawPath(p, body);

            // 肚皮
            var clip = g.Clip;
            g.SetClip(body, CombineMode.Intersect);
            using (var b = new SolidBrush(belly)) g.FillEllipse(b, 31f, 49f, 55f, 31f);
            using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, 46f, 30f, 34f, 12f);
            g.Clip = clip;

            // 胸鳍
            var fp = new GraphicsPath();
            fp.AddBezier(43f, 68f, 37f, 74f, 32f, 80f, 30f, 87f);
            fp.AddBezier(30f, 87f, 38f, 87f, 47f, 80f, 52f, 71f);
            fp.AddBezier(52f, 71f, 48f, 70f, 45f, 69f, 43f, 68f);
            fp.CloseFigure();
            using (var b = new SolidBrush(fin)) g.FillPath(b, fp);
            fp.Dispose();

            // 眼睛
            float eyeX = 77f, eyeY = 46f;
            bool yawning = pose != null && pose.Action == PetAction.Yawn && pose.ActionT > 0.12f && pose.ActionT < 0.9f;
            bool stretching = pose != null && pose.Action == PetAction.Stretch && pose.ActionT > 0.2f && pose.ActionT < 0.8f;
            bool wide = pose != null && pose.Dragging;
            if (sleeping || yawning || stretching)
            {
                using (var p = new Pen(Color.FromArgb(255, 18, 26, 44), 2.2f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, eyeX - 5.5f, eyeY - 3f, 11f, 8f, 15f, 150f);
                }
            }
            else if (mood == Mood.Sad || mood == Mood.Offline || blink)
            {
                using (var p = new Pen(Color.FromArgb(255, 18, 26, 44), 2.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, eyeX - 5f, eyeY, eyeX + 5f, eyeY);
                }
            }
            else if (wide)
            {
                using (var b = new SolidBrush(Color.FromArgb(255, 14, 22, 40)))
                    g.FillEllipse(b, eyeX - 6.4f, eyeY - 6.8f, 12.8f, 13.6f);
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                    g.FillEllipse(b, eyeX + 0.4f, eyeY - 4.6f, 4.2f, 4.2f);
                using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    g.FillEllipse(b, eyeX - 4.6f, eyeY + 1.2f, 2.2f, 2.2f);
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(255, 14, 22, 40)))
                    g.FillEllipse(b, eyeX - 5.4f, eyeY - 5.6f, 10.8f, 11.2f);
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                    g.FillEllipse(b, eyeX + 0.6f, eyeY - 3.8f, 3.4f, 3.4f);
                using (var b = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                    g.FillEllipse(b, eyeX - 3.4f, eyeY + 1.6f, 1.8f, 1.8f);
            }

            // 腮红 / 嘴 / 眉毛 / 汗
            if (yawning)
            {
                float k = (float)Math.Sin(pose.ActionT * Math.PI);
                using (var b = new SolidBrush(Color.FromArgb(255, 30, 22, 40)))
                    g.FillEllipse(b, 80f, 51f, 12f, 6f + 12f * k);
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 140, 152)))
                    g.FillEllipse(b, 83f, 54f + 5f * k, 6f, 1.5f + 2.5f * k);
            }
            else if (sleeping)
            {
                using (var p = new Pen(Color.FromArgb(200, 30, 40, 60), 1.4f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, 84f, 55f, 8f, 5f, 20f, 110f);
                }
            }
            else if (wide)
            {
                using (var b = new SolidBrush(Color.FromArgb(255, 30, 20, 38)))
                    g.FillEllipse(b, 85f, 56f, 5f, 6f);
            }
            else if (mood == Mood.Happy || mood == Mood.Calm)
            {
                using (var b = new SolidBrush(Color.FromArgb(55, 255, 128, 150)))
                    g.FillEllipse(b, 83f, 55f, 10f, 5.5f);
                using (var p = new Pen(Color.FromArgb(255, 20, 30, 50), 1.5f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    if (mood == Mood.Happy)
                        g.DrawArc(p, 84f, 53f, 10f, 8f, 20f, 100f);
                    else
                        g.DrawArc(p, 85f, 55f, 8f, 5f, 25f, 75f);
                }
            }
            else if (mood == Mood.Worried)
            {
                using (var p = new Pen(Color.FromArgb(230, 28, 34, 52), 1.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, 70f, 36f, 80f, 39f);
                    g.DrawArc(p, 83f, 55f, 9f, 3f, 200f, 110f);
                }
                using (var b = new SolidBrush(Color.FromArgb(200, 120, 190, 255)))
                    g.FillEllipse(b, 91f, 36f, 5f, 7f);
            }
            else if (mood == Mood.Sad)
            {
                using (var p = new Pen(Color.FromArgb(230, 40, 46, 64), 1.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, 83f, 59f, 9f, 5f, 200f, 100f);
                }
            }
            else if (mood == Mood.Offline)
            {
                using (var b = new SolidBrush(Color.FromArgb(190, 230, 240, 255)))
                {
                    g.DrawString("z", F(9f, FontStyle.Bold), b, 88f, 16f);
                    g.DrawString("z", F(6.5f, FontStyle.Bold), b, 96f, 10f);
                }
            }

            // 睡帽 / Zzz
            if (sleeping)
            {
                DrawNightCap(g);
                DrawSleepZ(g, t);
            }

            // 喷水
            if ((mood == Mood.Happy || mood == Mood.Calm) && !sleeping)
            {
                double phase = (t % 4.6) / 4.6;
                if (phase < 0.45)
                {
                    float k = (float)(phase / 0.45);
                    int a = (int)(210 * (1 - k));
                    if (a > 0)
                    {
                        using (var b = new SolidBrush(Color.FromArgb(a, 190, 225, 255)))
                        {
                            g.FillEllipse(b, 61f, 16f - k * 12f, 4.4f, 4.4f);
                            g.FillEllipse(b, 68f, 11f - k * 15f, 3.2f, 3.2f);
                            g.FillEllipse(b, 55f, 19f - k * 9f, 2.6f, 2.6f);
                        }
                    }
                }
            }

            // 待机动作：吐泡泡
            if (pose != null && pose.Action == PetAction.Bubbles)
                DrawBubbles(g, pose.ActionT);

            // 点击反应：爱心
            if (react >= 0f && react <= 1f && (mood == Mood.Happy || mood == Mood.Calm || mood == Mood.Worried))
            {
                for (int i = 0; i < 3; i++)
                {
                    float hx = 50f + i * 18f;
                    float hy = 2f + (i % 2) * 4f - react * 3f;
                    int a = (int)(235 * (1f - react));
                    if (a > 0) DrawHeart(g, hx, hy, i == 1 ? 18f : 15f, a);
                }
                using (var b = new SolidBrush(Color.FromArgb((int)(90 * (1f - react)), 255, 128, 150)))
                    g.FillEllipse(b, 83f, 55f, 10f, 5.5f);
            }

            g.Restore(state);
        }

        static void DrawBudgetRing(Graphics g, RectangleF box, Vm vm, PetPose pose)
        {
            if (vm.Budget <= 0 || !vm.HasData) return;
            double frac = (double)(vm.Stats.Month / vm.Budget);
            if (frac < 0) frac = 0;
            if (frac > 1) frac = 1;
            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height / 2f;
            float r = Math.Min(box.Width, box.Height) / 2f - 4f;
            if (r <= 0) return;
            var rc = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
            using (var p = new Pen(Color.FromArgb(34, 150, 180, 235), 2.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, rc, -90f, 359.9f);
            }
            if (frac <= 0) return;
            Color col = frac > 0.9 ? Palette.Red : (frac > 0.75 ? Palette.Amber : SkinAccent(pose != null ? pose.Skin : PetSkin.DeepBlue));
            using (var p = new Pen(col, 3.2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, rc, -90f, (float)(360.0 * frac));
            }
        }

        static Color SkinAccent(PetSkin skin)
        {
            if (skin == PetSkin.Sakura) return Color.FromArgb(255, 140, 180);
            if (skin == PetSkin.Mint) return Color.FromArgb(70, 205, 165);
            return Palette.Accent;
        }

        static void DrawFeet(Graphics g, float phase, Color fin)
        {
            float s = (float)Math.Sin(phase * Math.PI * 2.0);
            using (var dust = new SolidBrush(Color.FromArgb(48, 200, 220, 255)))
            {
                g.FillEllipse(dust, 14f, 86f, 5f, 5f);
                g.FillEllipse(dust, 8f, 91f, 3.5f, 3.5f);
                g.FillEllipse(dust, 21f, 93f, 3f, 3f);
            }
            using (var b = new SolidBrush(fin))
            {
                g.FillEllipse(b, 28f, 87f - Math.Max(0f, s) * 4.5f, 15f, 9.5f);
                g.FillEllipse(b, 50f, 87f - Math.Max(0f, -s) * 4.5f, 15f, 9.5f);
            }
            using (var p = new Pen(Color.FromArgb(90, 255, 255, 255), 0.8f))
            {
                g.DrawEllipse(p, 28f, 87f - Math.Max(0f, s) * 4.5f, 15f, 9.5f);
                g.DrawEllipse(p, 50f, 87f - Math.Max(0f, -s) * 4.5f, 15f, 9.5f);
            }
        }

        static void DrawSleepZ(Graphics g, double t)
        {
            float phase = (float)((t % 3.2) / 3.2);
            for (int i = 0; i < 3; i++)
            {
                float a = phase + i * 0.22f;
                if (a > 1f) a -= 1f;
                int alpha = (int)(190 * Math.Sin(a * Math.PI));
                if (alpha <= 0) continue;
                using (var b = new SolidBrush(Color.FromArgb(alpha, 200, 220, 255)))
                    g.DrawString("z", F(8f + i * 2f, FontStyle.Bold), b, 84f + i * 7f, 15f - i * 7f);
            }
        }

        static void DrawBubbles(Graphics g, float p)
        {
            for (int i = 0; i < 7; i++)
            {
                float bp = p * 1.25f - i * 0.07f;
                if (bp < 0f || bp > 1f) continue;
                float by = 24f - bp * 52f;
                float bx = 62f + (float)Math.Sin(i * 1.3f + bp * 8f) * 7f;
                float r = 2.6f + (i % 3) * 1.4f;
                int a = (int)(230 * (1f - bp));
                using (var b = new SolidBrush(Color.FromArgb(a, 220, 242, 255))) g.FillEllipse(b, bx, by, r, r);
                using (var pen = new Pen(Color.FromArgb(Math.Min(255, a), 255, 255, 255), 0.7f)) g.DrawEllipse(pen, bx, by, r, r);
                using (var b = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
                    g.FillEllipse(b, bx + r * 0.25f, by + r * 0.2f, r * 0.28f, r * 0.28f);
            }
        }

        static void DrawHeart(Graphics g, float x, float y, float size, int alpha)
        {
            float k = size / 20f;
            var p = new GraphicsPath();
            p.AddBezier(x + 10f * k, y + 17f * k, x + 4f * k, y + 12f * k, x + 1f * k, y + 9f * k, x + 1f * k, y + 6f * k);
            p.AddBezier(x + 1f * k, y + 6f * k, x + 1f * k, y + 2f * k, x + 5f * k, y, x + 10f * k, y + 4f * k);
            p.AddBezier(x + 10f * k, y + 4f * k, x + 14f * k, y, x + 18f * k, y + 2f * k, x + 19f * k, y + 6f * k);
            p.AddBezier(x + 19f * k, y + 6f * k, x + 19f * k, y + 10f * k, x + 15f * k, y + 14f * k, x + 10f * k, y + 17f * k);
            p.CloseFigure();
            using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 108, 140))) g.FillPath(b, p);
            p.Dispose();
        }

        static GraphicsPath BodyPath()
        {
            var body = new GraphicsPath();
            body.AddBezier(24f, 54f, 24f, 33f, 42f, 21f, 62f, 21f);
            body.AddBezier(62f, 21f, 84f, 21f, 96f, 35f, 97f, 51f);
            body.AddBezier(97f, 51f, 98f, 65f, 90f, 77f, 76f, 82f);
            body.AddBezier(76f, 82f, 60f, 87f, 38f, 84f, 29f, 75f);
            body.AddBezier(29f, 75f, 24.5f, 70f, 23f, 61f, 24f, 54f);
            body.CloseFigure();
            return body;
        }

        // ---------- 基础工具 ----------

        public static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            if (rad <= 0) { p.AddRectangle(r); return p; }
            float d = rad * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static Color StratoColor(Vm vm)
        {
            switch (vm.Mood)
            {
                case Mood.Happy: return Palette.Green;
                case Mood.Calm: return Color.FromArgb(110, 170, 220);
                case Mood.Worried: return Palette.Amber;
                case Mood.Sad: return Palette.Red;
                default: return Color.FromArgb(150, 160, 175);
            }
        }

        public static string Sign(decimal v, string sym)
        {
            if (v <= 0) return sym + "0.00";
            return "-" + sym + Fmt.Number(v);
        }

        enum TextAlign { Left, Right, Center }

        static void DrawText(Graphics g, string s, Font f, Color c, float x, float y, float w, TextAlign align)
        {
            using (var b = new SolidBrush(c))
            {
                var r = new RectangleF(x, y, Math.Max(1f, w), 200f);
                g.DrawString(s, f, b, r, align == TextAlign.Right ? SFRight : SFLeft);
            }
        }

        static void DrawingTextC(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (var b = new SolidBrush(c))
                g.DrawString(s, f, b, r, SFCenter);
        }
    }
}
