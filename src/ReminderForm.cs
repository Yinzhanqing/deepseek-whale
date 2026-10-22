using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    public class ReminderForm : Form
    {
        static readonly Color Cbg = Color.FromArgb(15, 23, 42);
        static readonly Color Cfield = Color.FromArgb(9, 16, 30);
        static readonly Color Cfg = Color.FromArgb(230, 238, 250);
        static readonly Color Cdim = Color.FromArgb(142, 162, 194);
        static readonly Color Caccent = Color.FromArgb(77, 107, 254);

        readonly AppConfig cfg;
        CheckBox water;
        DarkCombo interval;
        DarkCombo startHour;
        DarkCombo endHour;
        DarkCombo dwell;
        CheckBox wander;
        TextBox lines;
        CheckBox autoDnd;
        CheckBox allowAlerts;
        Button ok;
        Button cancel;

        public ReminderForm(AppConfig cfg)
        {
            this.cfg = cfg;
            Text = "提醒与勿扰 · DeepSeek 小鲸鱼";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Cbg;
            ForeColor = Cfg;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(380, 412);

            Controls.Add(MakeLabel("提醒与勿扰", 20, 12, 240, 30, 14f, FontStyle.Bold, Cfg));
            Controls.Add(MakeLabel("小鲸鱼什么时候可以来找你", 20, 40, 340, 18, 7.5f, FontStyle.Regular, Cdim));

            water = MakeCheck("喝水提醒", 20, 72, 160, cfg.WaterReminder);
            Controls.Add(water);

            Controls.Add(MakeLabel("间隔", 20, 104, 60, 18, 8f, FontStyle.Regular, Cdim));
            interval = MakeCombo(86, 100, 92);
            interval.Items = new string[] { "30 分钟", "45 分钟", "60 分钟", "90 分钟", "120 分钟" };
            interval.SelectedIndex = IntervalIndex(cfg.WaterIntervalMin);

            Controls.Add(MakeLabel("生效时段", 190, 104, 60, 18, 8f, FontStyle.Regular, Cdim));
            startHour = MakeCombo(248, 100, 52);
            startHour.Items = HourItems();
            startHour.SelectedIndex = Math.Max(0, Math.Min(23, cfg.ReminderStartHour));
            endHour = MakeCombo(310, 100, 52);
            endHour.Items = HourItems();
            endHour.SelectedIndex = Math.Max(0, Math.Min(23, cfg.ReminderEndHour));
            Controls.Add(MakeLabel("–", 301, 104, 10, 18, 8f, FontStyle.Regular, Cdim));

            Controls.Add(MakeLabel("停留", 20, 138, 60, 18, 8f, FontStyle.Regular, Cdim));
            dwell = MakeCombo(86, 134, 92);
            dwell.Items = new string[] { "10 秒", "20 秒", "30 秒", "60 秒" };
            dwell.SelectedIndex = DwellIndex(cfg.DwellSeconds);

            wander = MakeCheck("自由溜达", 190, 138, 170, cfg.Wander);
            Controls.Add(wander);

            Controls.Add(MakeLabel("提醒内容（一行一条，随机使用）", 20, 172, 340, 18, 8f, FontStyle.Regular, Cdim));
            lines = new TextBox();
            lines.Bounds = new Rectangle(20, 192, 340, 84);
            lines.Multiline = true;
            lines.ScrollBars = ScrollBars.Vertical;
            lines.AcceptsReturn = true;
            lines.WordWrap = true;
            lines.BackColor = Cfield;
            lines.ForeColor = Cfg;
            lines.BorderStyle = BorderStyle.FixedSingle;
            lines.Font = new Font("Microsoft YaHei UI", 9f);
            lines.Text = (cfg.ReminderText ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            Controls.Add(lines);

            Controls.Add(MakeLabel("勿扰", 20, 288, 60, 18, 8f, FontStyle.Regular, Cdim));
            autoDnd = MakeCheck("全屏应用时自动勿扰", 20, 306, 340, cfg.DndAutoFullscreen);
            Controls.Add(autoDnd);
            allowAlerts = MakeCheck("勿扰时仍然提醒余额告警", 20, 330, 340, cfg.DndAllowAlerts);
            Controls.Add(allowAlerts);

            var hint = MakeLabel("勿扰模式可以在小鲸鱼 / 托盘右键菜单里随时开关", 20, 354, 340, 16, 7f, FontStyle.Regular, Color.FromArgb(96, 116, 148));
            Controls.Add(hint);

            ok = new Button();
            ok.Text = "保存";
            ok.Bounds = new Rectangle(190, 376, 80, 30);
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.BackColor = Caccent;
            ok.ForeColor = Color.White;
            ok.Click += delegate { Save(); };
            Controls.Add(ok);

            cancel = new Button();
            cancel.Text = "取消";
            cancel.Bounds = new Rectangle(280, 376, 80, 30);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 120);
            cancel.BackColor = Color.FromArgb(22, 33, 54);
            cancel.ForeColor = Cfg;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            water.CheckedChanged += delegate { UpdateEnabled(); };
            UpdateEnabled();
        }

        void UpdateEnabled()
        {
            bool on = water.Checked;
            interval.Enabled = on;
            startHour.Enabled = on;
            endHour.Enabled = on;
            dwell.Enabled = on;
            lines.Enabled = on;
        }

        static string[] HourItems()
        {
            var a = new string[24];
            for (int i = 0; i < 24; i++) a[i] = (i < 10 ? "0" : "") + i + ":00";
            return a;
        }

        static int IntervalIndex(int min)
        {
            if (min <= 30) return 0;
            if (min <= 45) return 1;
            if (min <= 60) return 2;
            if (min <= 90) return 3;
            return 4;
        }

        static int IntervalValue(int idx)
        {
            switch (idx)
            {
                case 0: return 30;
                case 1: return 45;
                case 2: return 60;
                case 3: return 90;
                default: return 120;
            }
        }

        static int DwellIndex(int sec)
        {
            if (sec <= 10) return 0;
            if (sec <= 20) return 1;
            if (sec <= 30) return 2;
            return 3;
        }

        static int DwellValue(int idx)
        {
            switch (idx)
            {
                case 0: return 10;
                case 1: return 20;
                case 2: return 30;
                default: return 60;
            }
        }

        void Save()
        {
            cfg.WaterReminder = water.Checked;
            cfg.WaterIntervalMin = IntervalValue(interval.SelectedIndex);
            cfg.ReminderStartHour = Math.Max(0, Math.Min(23, startHour.SelectedIndex));
            cfg.ReminderEndHour = Math.Max(0, Math.Min(23, endHour.SelectedIndex));
            cfg.DwellSeconds = DwellValue(dwell.SelectedIndex);
            cfg.Wander = wander.Checked;
            string txt = (lines.Text ?? "").Replace("\r\n", "\n").Trim();
            if (txt.Length == 0) txt = "该喝水啦～";
            cfg.ReminderText = txt;
            cfg.DndAutoFullscreen = autoDnd.Checked;
            cfg.DndAllowAlerts = allowAlerts.Checked;
            DialogResult = DialogResult.OK;
            Close();
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

        CheckBox MakeCheck(string text, int x, int y, int w, bool value)
        {
            var c = new CheckBox();
            c.Text = text;
            c.Bounds = new Rectangle(x, y, w, 22);
            c.ForeColor = Cdim;
            c.FlatStyle = FlatStyle.Flat;
            c.Checked = value;
            c.BackColor = Color.Transparent;
            return c;
        }

        DarkCombo MakeCombo(int x, int y, int w)
        {
            var c = new DarkCombo();
            c.Bounds = new Rectangle(x, y, w, 26);
            c.Font = new Font("Microsoft YaHei UI", 9f);
            Controls.Add(c);
            return c;
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
            if (s > 1.01f)
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
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
