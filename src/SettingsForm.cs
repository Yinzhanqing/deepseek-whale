using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    public class SettingsForm : Form
    {
        static readonly Color Cbg = Color.FromArgb(15, 23, 42);
        static readonly Color Cfield = Color.FromArgb(9, 16, 30);
        static readonly Color Cfg = Color.FromArgb(230, 238, 250);
        static readonly Color Cdim = Color.FromArgb(142, 162, 194);
        static readonly Color Caccent = Color.FromArgb(77, 107, 254);

        public AppConfig Result;

        readonly AppConfig cfg;
        readonly bool firstRun;
        TextBox keyBox;
        CheckBox showKey;
        Label keyHint;
        DarkCombo interval;
        DarkCombo currency;
        TextBox threshold;
        TextBox budget;
        CheckBox followDsh;
        CheckBox hideDsh;
        CheckBox autoStart;
        Button test;
        Label testLabel;
        Button ok;
        DarkCombo skin;
        Button cancel;

        public SettingsForm(AppConfig cfg, bool firstRun)
        {
            this.cfg = cfg.Clone();
            this.firstRun = firstRun;

            Text = firstRun ? "欢迎使用 DeepSeek 小鲸鱼" : "设置 · DeepSeek 小鲸鱼";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Cbg;
            ForeColor = Cfg;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(380, 432);

            var title = MakeLabel(firstRun ? "设置你的 API Key" : "设置", 20, 12, 340, 28, 14f, FontStyle.Bold, Cfg);
            Controls.Add(title);
            if (firstRun)
            {
                var sub = MakeLabel("Key 只会加密保存在这台电脑上，不会发送到别处。", 20, 42, 340, 20, 8f, FontStyle.Regular, Cdim);
                Controls.Add(sub);
            }

            Controls.Add(MakeLabel("API Key", 20, 68, 200, 18, 8f, FontStyle.Regular, Cdim));
            keyBox = new TextBox();
            keyBox.Bounds = new Rectangle(20, 86, 340, 26);
            keyBox.BackColor = Cfield;
            keyBox.ForeColor = Cfg;
            keyBox.BorderStyle = BorderStyle.FixedSingle;
            keyBox.Font = new Font("Consolas", 10f);
            keyBox.UseSystemPasswordChar = true;
            string existing = cfg.GetKey();
            if (!string.IsNullOrEmpty(existing)) keyBox.Text = existing;
            Controls.Add(keyBox);

            showKey = new CheckBox();
            showKey.Text = "显示 Key";
            showKey.Bounds = new Rectangle(20, 116, 100, 22);
            showKey.ForeColor = Cdim;
            showKey.FlatStyle = FlatStyle.Flat;
            showKey.CheckedChanged += delegate { keyBox.UseSystemPasswordChar = !showKey.Checked; };
            Controls.Add(showKey);

            keyHint = MakeLabel(cfg.HasKey ? "已保存 · 尾号 " + cfg.KeyHint : "尚未保存",
                200, 118, 160, 20, 7.5f, FontStyle.Regular, Cdim);
            keyHint.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(keyHint);

            Controls.Add(MakeLabel("刷新间隔", 20, 150, 100, 18, 8f, FontStyle.Regular, Cdim));
            interval = new DarkCombo();
            interval.Bounds = new Rectangle(20, 168, 105, 26);
            interval.Font = new Font("Microsoft YaHei UI", 9f);
            interval.Items = new string[] { "1 分钟", "5 分钟", "15 分钟", "30 分钟", "60 分钟" };
            interval.SelectedIndex = IntervalIndex(cfg.IntervalMin);
            Controls.Add(interval);

            Controls.Add(MakeLabel("显示币种", 140, 150, 100, 18, 8f, FontStyle.Regular, Cdim));
            currency = new DarkCombo();
            currency.Bounds = new Rectangle(140, 168, 105, 26);
            currency.Font = new Font("Microsoft YaHei UI", 9f);
            currency.Items = new string[] { "自动", "人民币 CNY", "美元 USD" };
            currency.SelectedIndex = cfg.Currency == "CNY" ? 1 : (cfg.Currency == "USD" ? 2 : 0);
            Controls.Add(currency);

            Controls.Add(MakeLabel("低余额提醒（0 关闭）", 260, 150, 110, 18, 7.2f, FontStyle.Regular, Cdim));
            threshold = MakeNumberBox(250, 168, 110, cfg.LowBalance);
            Controls.Add(threshold);

            Controls.Add(MakeLabel("每月预算", 20, 202, 100, 18, 8f, FontStyle.Regular, Cdim));
            budget = MakeNumberBox(20, 220, 110, cfg.MonthlyBudget);
            Controls.Add(budget);

            Controls.Add(MakeLabel("鲸鱼配色", 250, 202, 110, 18, 8f, FontStyle.Regular, Cdim));
            skin = new DarkCombo();
            skin.Bounds = new Rectangle(250, 220, 110, 26);
            skin.Font = new Font("Microsoft YaHei UI", 9f);
            skin.Items = new string[] { "深海蓝", "樱花粉", "薄荷绿" };
            skin.SelectedIndex = cfg.Skin == "sakura" ? 1 : (cfg.Skin == "mint" ? 2 : 0);
            Controls.Add(skin);

            Controls.Add(MakeLabel("0 表示不设预算，设了会在面板显示进度", 20, 250, 340, 18, 7.2f, FontStyle.Regular, Cdim));

            followDsh = MakeCheck("用 DeepSeek Harness 时自动出现", 20, 272, cfg.FollowDsh);
            Controls.Add(followDsh);
            hideDsh = MakeCheck("它关闭时自动隐藏（不退出）", 20, 296, cfg.HideWithDsh);
            Controls.Add(hideDsh);
            autoStart = MakeCheck("开机自动启动（登录后静默驻留托盘）", 20, 320, Startup.IsEnabled(Application.ExecutablePath));
            Controls.Add(autoStart);

            followDsh.CheckedChanged += delegate
            {
                hideDsh.Enabled = followDsh.Checked;
                hideDsh.ForeColor = followDsh.Checked ? Cdim : Color.FromArgb(80, 90, 108);
            };
            hideDsh.Enabled = followDsh.Checked;

            test = new Button();
            test.Text = "测试连接";
            test.Bounds = new Rectangle(20, 354, 90, 28);
            test.FlatStyle = FlatStyle.Flat;
            test.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 120);
            test.BackColor = Color.FromArgb(22, 33, 54);
            test.ForeColor = Cfg;
            test.Click += delegate { TestConnection(); };
            Controls.Add(test);

            var remind = new Button();
            remind.Text = "提醒…";
            remind.Bounds = new Rectangle(118, 354, 92, 28);
            remind.FlatStyle = FlatStyle.Flat;
            remind.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 120);
            remind.BackColor = Color.FromArgb(22, 33, 54);
            remind.ForeColor = Cfg;
            remind.Click += delegate
            {
                using (var rf = new ReminderForm(cfg))
                {
                    rf.TopMost = true;
                    rf.ShowDialog(this);
                }
            };
            Controls.Add(remind);

            testLabel = MakeLabel(firstRun ? "Key 只在本机加密保存" : "", 218, 350, 160, 40, 7.5f, FontStyle.Regular, Cdim);
            Controls.Add(testLabel);

            ok = new Button();
            ok.Text = "保存";
            ok.Bounds = new Rectangle(190, 392, 80, 30);
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.BackColor = Caccent;
            ok.ForeColor = Color.White;
            ok.Click += delegate { Save(); };
            Controls.Add(ok);

            cancel = new Button();
            cancel.Text = "取消";
            cancel.Bounds = new Rectangle(280, 392, 80, 30);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 120);
            cancel.BackColor = Color.FromArgb(22, 33, 54);
            cancel.ForeColor = Cfg;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref dark, 4);
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            float s = 1f;
            try
            {
                using (var g = CreateGraphics()) s = g.DpiX / 96f;
            }
            catch { s = 1f; }
            if (s < 0.9f) s = 1f;
            if (s > 1.01f) ScaleLayout(s);
            keyBox.Select(0, 0);
        }

        void ScaleLayout(float s)
        {
            ClientSize = new Size((int)Math.Round(ClientSize.Width * s), (int)Math.Round(ClientSize.Height * s));
            foreach (Control c in Controls)
            {
                c.Bounds = new Rectangle(
                    (int)Math.Round(c.Left * s),
                    (int)Math.Round(c.Top * s),
                    (int)Math.Round(c.Width * s),
                    (int)Math.Round(c.Height * s));
            }
        }

        TextBox MakeNumberBox(int x, int y, int w, double value)
        {
            var t = new TextBox();
            t.Bounds = new Rectangle(x, y, w, 26);
            t.BackColor = Cfield;
            t.ForeColor = Cfg;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.TextAlign = HorizontalAlignment.Right;
            t.Font = new Font("Consolas", 9.5f);
            t.Text = value.ToString("0.##", CultureInfo.InvariantCulture);
            return t;
        }

        static int IntervalIndex(int min)
        {
            if (min <= 1) return 0;
            if (min <= 5) return 1;
            if (min <= 15) return 2;
            if (min <= 30) return 3;
            return 4;
        }

        static int IntervalValue(int idx)
        {
            switch (idx)
            {
                case 0: return 1;
                case 1: return 5;
                case 2: return 15;
                case 3: return 30;
                default: return 60;
            }
        }

        static decimal ParseNum(string s, decimal def)
        {
            decimal v;
            if (decimal.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return def;
        }

        Label MakeLabel(string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
        {
            var l = new Label();
            l.Text = text;
            l.Bounds = new Rectangle(x, y, w, h);
            l.ForeColor = color;
            l.Font = new Font("Microsoft YaHei UI", size, style);
            l.AutoSize = false;
            l.BackColor = Color.Transparent;
            return l;
        }

        CheckBox MakeCheck(string text, int x, int y, bool value)
        {
            var c = new CheckBox();
            c.Text = text;
            c.Bounds = new Rectangle(x, y, 340, 22);
            c.ForeColor = Cdim;
            c.FlatStyle = FlatStyle.Flat;
            c.Checked = value;
            c.BackColor = Color.Transparent;
            return c;
        }

        void TestConnection()
        {
            string key = keyBox.Text.Trim();
            if (key.Length == 0)
            {
                testLabel.ForeColor = Color.FromArgb(246, 196, 83);
                testLabel.Text = "先填入 API Key";
                return;
            }
            test.Enabled = false;
            testLabel.ForeColor = Cdim;
            testLabel.Text = "正在连接 api.deepseek.com …";
            ThreadPool.QueueUserWorkItem(delegate
            {
                BalanceResult r = Api.Fetch(key, true);
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        test.Enabled = true;
                        if (r.Ok)
                        {
                            string summary = "连接成功";
                            foreach (string cur in r.Cur.Keys)
                            {
                                var cb = r.Cur[cur];
                                summary = "连接成功 · 余额 " + VmFactory.SymOf(cur) + Fmt.Number(cb.Total);
                                break;
                            }
                            testLabel.ForeColor = Color.FromArgb(53, 214, 164);
                            testLabel.Text = summary;
                        }
                        else
                        {
                            testLabel.ForeColor = Color.FromArgb(255, 123, 123);
                            testLabel.Text = "失败：" + Fmt.Truncate(r.Error, 40);
                        }
                    }));
                }
                catch { }
            });
        }

        void Save()
        {
            var c = cfg.Clone();
            c.IntervalMin = IntervalValue(interval.SelectedIndex);
            c.LowBalance = (double)Clamp(ParseNum(threshold.Text, 10m), 0m, 100000m);
            c.MonthlyBudget = (double)Clamp(ParseNum(budget.Text, 0m), 0m, 1000000m);
            c.Currency = currency.SelectedIndex == 1 ? "CNY" : (currency.SelectedIndex == 2 ? "USD" : "auto");
            c.Skin = skin.SelectedIndex == 1 ? "sakura" : (skin.SelectedIndex == 2 ? "mint" : "blue");
            c.FollowDsh = followDsh.Checked;
            c.HideWithDsh = hideDsh.Checked;
            c.SetKey(keyBox.Text.Trim());
            Result = c;

            bool wantAuto = autoStart.Checked;
            bool hasAuto = Startup.IsEnabled(Application.ExecutablePath);
            if (wantAuto && !hasAuto) Startup.Enable(Application.ExecutablePath);
            if (!wantAuto && hasAuto) Startup.Disable();

            DialogResult = DialogResult.OK;
            Close();
        }

        static decimal Clamp(decimal v, decimal lo, decimal hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }

    public class DarkCombo : Control
    {
        public string[] Items = new string[0];
        public int SelectedIndex = 0;

        bool hover;

        public DarkCombo()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundRect(r, 6f))
            {
                using (var b = new SolidBrush(Color.FromArgb(9, 16, 30))) g.FillPath(b, path);
                using (var p = new Pen(hover ? Color.FromArgb(110, 150, 240) : Color.FromArgb(48, 64, 98), 1f))
                    g.DrawPath(p, path);
            }
            string text = (Items != null && SelectedIndex >= 0 && SelectedIndex < Items.Length) ? Items[SelectedIndex] : "";
            TextRenderer.DrawText(g, text, Font, new Rectangle(10, 0, Width - 30, Height),
                Color.FromArgb(230, 238, 250), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            using (var p = new Pen(Color.FromArgb(140, 162, 194), 1.4f))
            {
                int cx = Width - 16, cy = Height / 2;
                g.DrawLine(p, cx - 4, cy - 2, cx, cy + 2);
                g.DrawLine(p, cx, cy + 2, cx + 4, cy - 2);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var m = new ContextMenuStrip();
            m.ShowImageMargin = false;
            m.Font = Font;
            m.BackColor = Color.FromArgb(16, 24, 42);
            m.ForeColor = Color.FromArgb(230, 238, 250);
            m.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            ((ToolStripProfessionalRenderer)m.Renderer).RoundedEdges = false;
            for (int i = 0; i < Items.Length; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(Items[i]);
                it.ForeColor = Color.FromArgb(230, 238, 250);
                if (i == SelectedIndex) it.Font = new Font(Font, FontStyle.Bold);
                it.Click += delegate
                {
                    SelectedIndex = idx;
                    Invalidate();
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                };
                m.Items.Add(it);
            }
            m.Show(this, new Point(0, Height + 2));
        }

        public event EventHandler SelectedIndexChanged;

        static GraphicsPath RoundRect(Rectangle r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
