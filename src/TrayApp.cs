using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DeepSeekWhale
{
    public class TrayApp : ApplicationContext
    {
        public AppConfig Cfg;
        public HistoryStore Hist;
        public PetWindow Pet;
        public Vm Vm { get; private set; }

        readonly Cli cli;
        readonly string dataDir;
        readonly string configPath;
        readonly string historyPath;
        BalanceResult last;
        bool refreshing;
        NotifyIcon tray;
        readonly Dictionary<Mood, Icon> moodIcons = new Dictionary<Mood, Icon>();
        readonly List<IntPtr> iconHandles = new List<IntPtr>();
        Timer refreshTimer;
        Timer dshTimer;
        Timer startupTimer;
        Timer dndTimer;
        Timer dshUsageTimer;
        bool dndActive;
        bool dshBusy;
        DshSnapshot dsh;
        Icon dndIcon;
        bool dshWas;
        bool dshRunning;
        bool hiddenByUser;
        bool quitting;
        string message;
        long messageUntil;
        EventWaitHandle showEvent;
        Thread showThread;
        long lastTopupNotify;

        public TrayApp(Cli cli)
        {
            this.cli = cli;
            dataDir = string.IsNullOrEmpty(cli.DataDir) ? Paths.DefaultDataDir : cli.DataDir;
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            Log.Init(dataDir);
            configPath = Path.Combine(dataDir, "config.json");
            historyPath = Path.Combine(dataDir, "history.json");

            Cfg = AppConfig.Load(configPath);
            if (!string.IsNullOrEmpty(cli.SaveFrame)) PetWindow.DebugFramePath = cli.SaveFrame;
            if (cli.TestMode && !string.IsNullOrEmpty(cli.Key)) Cfg.SetKey(cli.Key);
            if (!string.IsNullOrEmpty(cli.ApiBase)) Api.BaseUrl = cli.ApiBase;
            if (!Cfg.HasKey && cli.TestMode && !string.IsNullOrEmpty(cli.Key)) Cfg.SetKey(cli.Key);

            Hist = HistoryStore.Load(historyPath);
            Hist.Path = historyPath;

            dshRunning = DshRunning();
            dshWas = dshRunning;
            if (cli.NoDsh) { dshRunning = false; dshWas = false; }
            Vm = VmFactory.Build(Cfg, Hist, null, false, null, 0, dshRunning);

            Pet = new PetWindow(this);
            BuildTray();

            refreshTimer = new Timer();
            refreshTimer.Tick += delegate { RefreshNow(false); };
            ResetRefreshTimer();

            dshTimer = new Timer();
            dshTimer.Interval = 4000;
            dshTimer.Tick += delegate { CheckDsh(); };
            if (!cli.NoDsh) dshTimer.Start();

            dndTimer = new Timer();
            dndTimer.Interval = 2500;
            dndTimer.Tick += delegate { CheckDnd(); };
            dndTimer.Start();
            CheckDnd();

            dshUsageTimer = new Timer();
            dshUsageTimer.Interval = 20000;
            dshUsageTimer.Tick += delegate { RefreshDsh(); };
            dshUsageTimer.Start();

            startupTimer = new Timer();
            startupTimer.Interval = 500;
            startupTimer.Tick += delegate
            {
                startupTimer.Stop();
                OnStartup();
            };
            startupTimer.Start();

            if (cli.ExitAfterMs > 0)
            {
                var t = new Timer();
                t.Interval = cli.ExitAfterMs;
                t.Tick += delegate { t.Stop(); Quit(); };
                t.Start();
            }

            if (showEvent == null)
            {
                try { showEvent = EventWaitHandle.OpenExisting(@"Local\DeepSeekWhale.Show"); }
                catch { }
            }
            if (showEvent != null)
            {
                showThread = new Thread(delegate()
                {
                    while (true)
                    {
                        try
                        {
                            showEvent.WaitOne();
                            try { Pet.BeginInvoke(new Action(delegate { ShowPet(); })); }
                            catch { }
                        }
                        catch { break; }
                    }
                });
                showThread.IsBackground = true;
                showThread.Start();
            }
        }

        void OnStartup()
        {
            if (!Cfg.HasKey && !cli.AutoStart)
            {
                ShowPet();
                OpenSettings(true);
                return;
            }
            if (cli.AutoStart || Cfg.FollowDsh)
            {
                if (dshRunning) ShowPet();
                else Pet.HidePet();
            }
            else
            {
                ShowPet();
            }
            if (cli.Expanded) Pet.SetExpanded(true);
            if (cli.UsageTab) Pet.ShowUsageTab();
            if (!string.IsNullOrEmpty(cli.Shot)) Pet.CaptureAfter(2600, cli.Shot);
            if (cli.DemoWater || cli.DemoWalk)
            {                var demo = new Timer();
                demo.Interval = 1500;
                demo.Tick += delegate
                {
                    demo.Stop();
                    demo.Dispose();
                    if (cli.DemoWater) Pet.TriggerWater();
                    else Pet.TriggerStroll();
                    if (!string.IsNullOrEmpty(cli.Shot)) Pet.CaptureAfter(3800, cli.Shot);
                };
                demo.Start();
            }
            if (cli.DemoCannon)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(1800);
                    try
                    {
                        Pet.BeginInvoke(new Action(delegate { Pet.DemoCannon(); }));
                    }
                    catch { }
                });
            }
            if (cli.DemoMenu)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(1500);
                    try
                    {
                        Pet.BeginInvoke(new Action(delegate { Pet.DemoMenu(); }));
                    }
                    catch { }
                });
            }            if (cli.DemoTarget)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(1600);
                    try
                    {
                        Pet.BeginInvoke(new Action(delegate { Pet.EnterCannonMode(); }));
                    }
                    catch { }
                });
            }
            if (Cfg.HasKey) RefreshNow(false);
            RefreshDsh();
        }

        // ---------- 配置 ----------

        public void SaveConfig()
        {
            Cfg.Save(configPath);
        }

        public void SaveWindowPos(int x, int y)
        {
            Cfg.X = x;
            Cfg.Y = y;
            SaveConfig();
        }

        void ResetRefreshTimer()
        {
            if (refreshTimer == null) return;
            refreshTimer.Stop();
            refreshTimer.Interval = Math.Max(1, Cfg.IntervalMin) * 60000;
            refreshTimer.Start();
        }

        // ---------- 数据 ----------

        void RebuildVm()
        {
            Vm = VmFactory.Build(Cfg, Hist, last, refreshing, message, messageUntil, dshRunning);
            Vm.Dnd = dndActive;
            Vm.Dsh = dsh;
            if (Pet != null) Pet.Redraw();
        }

        public void RefreshDsh()
        {
            if (dshBusy) return;
            dshBusy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                var s = DshUsage.Scan();
                try
                {
                    Pet.BeginInvoke(new Action(delegate
                    {
                        dshBusy = false;
                        bool first = dsh == null;
                        if (!string.IsNullOrEmpty(Cfg.ViewSessionId))
                        {
                            s.Pinned = false;
                            foreach (var x in s.Sessions)
                            {
                                if (x.Total > 0 && x.Id == Cfg.ViewSessionId)
                                {
                                    s.Current = x;
                                    s.Pinned = true;
                                    break;
                                }
                            }
                        }
                        dsh = s;
                        if (first)
                            Log.Info("DSH usage sessions=" + s.Sessions.Count + " today=" + s.TodayCost + " err=" + (s.Error ?? "-"));
                        RebuildVm();
                        MaybePeakAdvice();
                    }));
                }
                catch { dshBusy = false; }
            });
        }

        public void ClearMessage()
        {
            if (message == null) return;
            message = null;
            messageUntil = 0;
            RebuildVm();
        }

        public void SetMessage(string text, int seconds)
        {
            message = text;
            messageUntil = Fmt.NowUnix() + seconds;
            RebuildVm();
        }

        public void RefreshNow(bool manual)
        {
            if (refreshing) return;
            if (!Cfg.HasKey)
            {
                if (manual) OpenSettings(true);
                return;
            }
            string key = Cfg.GetKey();
            if (string.IsNullOrEmpty(key))
            {
                if (manual) OpenSettings(true);
                return;
            }
            refreshing = true;
            if (manual) SetMessage("正在刷新…", 3);
            RebuildVm();
            RefreshDsh();
            ThreadPool.QueueUserWorkItem(delegate
            {
                BalanceResult r = Api.Fetch(key, Cfg.UseSystemProxy);
                try
                {
                    Pet.BeginInvoke(new Action(delegate { OnFetched(r); }));
                }
                catch
                {
                    refreshing = false;
                }
            });
        }

        void OnFetched(BalanceResult r)
        {
            refreshing = false;
            message = null;
            messageUntil = 0;
            if (r != null && r.Ok)
            {
                last = r;
                foreach (string cur in r.Cur.Keys)
                {
                    TopupEvent e = Hist.Add(r, cur);
                    if (e != null && e.Amount >= 0.01m)
                    {
                        long now = Fmt.NowUnix();
                        if (now - lastTopupNotify > 60)
                        {
                            lastTopupNotify = now;
                            string sym = VmFactory.SymOf(cur);
                            Say("充值到账 +" + sym + Fmt.Number(e.Amount), 7);
                        }
                    }
                }
            }
            else if (r != null)
            {
                last = r;
            }
            RebuildVm();
            UpdateTray();
            CheckSpike();
            CheckLowBalance();
        }

        void MaybePeakAdvice()
        {
            if (Vm == null || !Vm.PeakNow) return;
            if (dsh == null || dsh.Current == null) return;
            long now = Fmt.NowUnix();
            if (now - dsh.Current.LastPromptAt > 900) return;
            long todayStart = Fmt.ToUnix(DateTime.Today.ToUniversalTime());
            if (Cfg.LastPeakNotify >= todayStart) return;
            Cfg.LastPeakNotify = now;
            SaveConfig();
            Say("现在是高峰，单价 2 倍；" + Vm.PeakHint + "，不急的话等等再跑～", 9);
        }

        void CheckSpike()
        {
            if (Vm == null || !Vm.HasData || Vm.Stats.SampleCount < 3) return;
            decimal today = Vm.Stats.Today;
            if (today < 2m) return;
            decimal prev = 0m;
            for (int i = 22; i <= 28; i++) prev += Vm.Stats.Daily[i];
            decimal avg = prev / 7m;
            if (avg < 0.5m) return;
            if (today < avg * 3m && today < avg + 10m) return;
            long now = Fmt.NowUnix();
            if (now - Cfg.LastSpikeNotify < 6 * 3600) return;
            Cfg.LastSpikeNotify = now;
            SaveConfig();
            string msg = "今天消耗有点快";
            if (dsh != null && dsh.Current != null && !string.IsNullOrEmpty(dsh.Current.Title))
                msg = "今天消耗有点快，「" + Fmt.Truncate(dsh.Current.Title, 10) + "」占大头";
            SayAlert(msg + "～", 9);
        }

        void CheckLowBalance()
        {
            if (Vm == null || !Vm.HasData) return;
            decimal threshold = (decimal)Cfg.LowBalance;
            if (threshold <= 0) return;
            if (Vm.Balance >= threshold) return;
            long now = Fmt.NowUnix();
            if (now - Cfg.LastLowNotify < 12 * 3600) return;
            Cfg.LastLowNotify = now;
            SaveConfig();
            SayAlert("余额 " + Vm.Sym + Fmt.Number(Vm.Balance) + "，记得充值～", 9);
        }

        // ---------- 托盘 ----------

        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Text = "DeepSeek 小鲸鱼";
            BuildMoodIcons();
            dndIcon = CreateIcon(Mood.Offline, Color.FromArgb(180, 150, 235));
            tray.Icon = moodIcons[Mood.Calm];

            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.BackColor = Color.FromArgb(16, 24, 42);
            menu.ForeColor = Color.FromArgb(230, 238, 250);
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            ((ToolStripProfessionalRenderer)menu.Renderer).RoundedEdges = false;

            var show = new ToolStripMenuItem("显示小鲸鱼");
            show.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            show.ForeColor = Color.FromArgb(230, 238, 250);
            show.Click += delegate { ShowPet(); };
            menu.Items.Add(show);

            var detail = new ToolStripMenuItem("展开详情");
            detail.ForeColor = Color.FromArgb(230, 238, 250);
            detail.Click += delegate
            {
                ShowPet();
                Pet.SetExpanded(true);
            };
            menu.Items.Add(detail);

            var mini = new ToolStripMenuItem("迷你模式");
            mini.ForeColor = Color.FromArgb(230, 238, 250);
            mini.Click += delegate
            {
                ShowPet();
                Pet.ToggleMini();
            };
            menu.Items.Add(mini);

            var refresh = new ToolStripMenuItem("立即刷新");
            refresh.ForeColor = Color.FromArgb(230, 238, 250);
            refresh.Click += delegate { RefreshNow(true); };
            menu.Items.Add(refresh);

            var cannon = new ToolStripMenuItem("水炮模式…");
            cannon.ForeColor = Color.FromArgb(230, 238, 250);
            cannon.Click += delegate { StartCannon(); };
            menu.Items.Add(cannon);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(CreateDndMenu());

            var remind = new ToolStripMenuItem("提醒与勿扰…");
            remind.ForeColor = Color.FromArgb(230, 238, 250);
            remind.Click += delegate { OpenReminderSettings(); };
            menu.Items.Add(remind);

            var settings = new ToolStripMenuItem("设置…");
            settings.ForeColor = Color.FromArgb(230, 238, 250);
            settings.Click += delegate { OpenSettings(false); };
            menu.Items.Add(settings);

            var quit = new ToolStripMenuItem("退出");
            quit.ForeColor = Color.FromArgb(255, 150, 150);
            quit.Click += delegate { Quit(); };
            menu.Items.Add(quit);

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowPet(); };
            tray.Visible = true;
        }

        void BuildMoodIcons()
        {
            AddMoodIcon(Mood.Happy, Color.FromArgb(53, 214, 164));
            AddMoodIcon(Mood.Calm, Color.FromArgb(110, 170, 220));
            AddMoodIcon(Mood.Worried, Color.FromArgb(246, 196, 83));
            AddMoodIcon(Mood.Sad, Color.FromArgb(255, 123, 123));
            AddMoodIcon(Mood.Offline, Color.FromArgb(130, 140, 155));
        }

        void AddMoodIcon(Mood mood, Color dot)
        {
            moodIcons[mood] = CreateIcon(mood, dot);
        }

        Icon CreateIcon(Mood mood, Color dot)
        {
            using (var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                bmp.SetResolution(96f, 96f);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Renderer.DrawWhale(g, new RectangleF(1f, 0f, 30f, 30f), mood, 2.0, false, 1f);
                    using (var b = new SolidBrush(Color.FromArgb(230, 10, 16, 28))) g.FillEllipse(b, 20f, 20f, 11f, 11f);
                    using (var b = new SolidBrush(dot)) g.FillEllipse(b, 22f, 22f, 7f, 7f);
                }
                IntPtr h = bmp.GetHicon();
                iconHandles.Add(h);
                return Icon.FromHandle(h);
            }
        }

        void UpdateTray()
        {
            if (tray == null) return;
            try
            {
                string text;
                if (dndActive)
                    text = "DeepSeek 小鲸鱼 · " + DndStatusText();
                else if (Vm != null && Vm.HasData)
                    text = "DeepSeek " + Vm.Sym + Fmt.Number(Vm.Balance) + " · 今日 " + Renderer.Sign(Vm.Stats.Today, Vm.Sym);
                else if (Vm != null && Vm.Error != null)
                    text = "DeepSeek 小鲸鱼 · " + Fmt.Truncate(Vm.Error, 20);
                else
                    text = "DeepSeek 小鲸鱼";
                tray.Text = Fmt.Truncate(text, 60);
                Icon ic;
                if (dndActive) tray.Icon = dndIcon;
                else if (Vm != null && moodIcons.TryGetValue(Vm.Mood, out ic)) tray.Icon = ic;
            }
            catch { }
        }

        public void OpenReminderSettings()
        {
            using (var f = new ReminderForm(Cfg))
            {
                f.TopMost = true;
                if (f.ShowDialog() == DialogResult.OK)
                {
                    SaveConfig();
                    if (Pet != null) Pet.ResetReminderSchedule();
                    CheckDnd();
                }
            }
        }

        public ToolStripMenuItem CreateDndMenu()
        {
            var m = new ToolStripMenuItem("勿扰模式");
            m.ForeColor = Color.FromArgb(230, 238, 250);
            m.DropDown.BackColor = Color.FromArgb(16, 24, 42);
            m.DropDown.ForeColor = Color.FromArgb(230, 238, 250);
            m.DropDown.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            ((ToolStripProfessionalRenderer)m.DropDown.Renderer).RoundedEdges = false;
            AddDndItem(m, "30 分钟", delegate { DndFor(30); });
            AddDndItem(m, "1 小时", delegate { DndFor(60); });
            AddDndItem(m, "2 小时", delegate { DndFor(120); });
            AddDndItem(m, "到明早 7 点", delegate { DndUntilMorning(); });
            AddDndItem(m, "一直勿扰", delegate { DndForever(); });
            m.DropDownItems.Add(new ToolStripSeparator());
            AddDndItem(m, "关闭勿扰", delegate { DndOff(); });
            m.DropDownOpening += delegate
            {
                m.Checked = dndActive;
                m.Text = dndActive
                    ? "勿扰模式（" + DndStatusText().Replace("勿扰中 · ", "") + "）"
                    : "勿扰模式";
            };
            return m;
        }

        void AddDndItem(ToolStripMenuItem parent, string text, EventHandler handler)
        {
            var it = new ToolStripMenuItem(text);
            it.ForeColor = Color.FromArgb(230, 238, 250);
            it.Click += handler;
            parent.DropDownItems.Add(it);
        }

        public void StartCannon()
        {
            if (Pet == null || !Pet.Visible) return;
            Pet.SetExpanded(false);
            Pet.EnterCannonMode();
        }

        public void PinDshSession(string id)
        {
            Cfg.ViewSessionId = id ?? "";
            SaveConfig();
            if (dsh == null) return;
            if (!string.IsNullOrEmpty(id))
            {
                foreach (var x in dsh.Sessions)
                {
                    if (x.Total > 0 && x.Id == id)
                    {
                        dsh.Current = x;
                        dsh.Pinned = true;
                        RebuildVm();
                        return;
                    }
                }
            }
            dsh.Pinned = false;
            dsh.Current = null;
            foreach (var x in dsh.Sessions)
            {
                if (x.Total > 0) { dsh.Current = x; break; }
            }
            RebuildVm();
        }

        public void Say(string text, double seconds)
        {
            if (dndActive) return;
            if (Pet != null && Pet.Visible && !Pet.IsExpanded) Pet.Say(text, seconds);
            else Balloon("DeepSeek 小鲸鱼", text);
        }

        public void SayAlert(string text, double seconds)
        {
            if (dndActive)
            {
                if (Cfg.DndAllowAlerts) Balloon("余额提醒", text);
                return;
            }
            Say(text, seconds);
        }

        public bool DndActive { get { return dndActive; } }

        public string DndStatusText()
        {
            long now = Fmt.NowUnix();
            if (Cfg.Dnd) return "勿扰中 · 一直";
            if (Cfg.DndUntil > now)
            {
                int m = (int)Math.Ceiling((Cfg.DndUntil - now) / 60.0);
                return "勿扰中 · 还剩 " + m + " 分钟";
            }
            if (Cfg.DndAutoFullscreen && IsFullscreenApp()) return "勿扰中 · 全屏";
            return "未开启";
        }

        void CheckDnd()
        {
            long now = Fmt.NowUnix();
            if (Cfg.DndUntil > 0 && Cfg.DndUntil <= now)
            {
                Cfg.DndUntil = 0;
                SaveConfig();
            }
            bool timed = Cfg.DndUntil > now;
            bool full = Cfg.DndAutoFullscreen && IsFullscreenApp();
            bool active = Cfg.Dnd || timed || full;
            if (active == dndActive) return;
            dndActive = active;
            Log.Info("DND " + (active ? "on" : "off") + " manual=" + Cfg.Dnd + " timed=" + timed + " full=" + full);
            RebuildVm();
            if (active)
            {
                if (Pet != null) Pet.EnterDnd();
                if (!full) Balloon("勿扰模式", "先安静一会儿，有事再叫我");
            }
            UpdateTray();
        }

        public void DndFor(int minutes)
        {
            Cfg.Dnd = false;
            Cfg.DndUntil = Fmt.NowUnix() + minutes * 60L;
            SaveConfig();
            CheckDnd();
        }

        public void DndUntilMorning()
        {
            DateTime now = DateTime.Now;
            DateTime target = now.Hour < 7 ? now.Date.AddHours(7) : now.Date.AddDays(1).AddHours(7);
            Cfg.Dnd = false;
            Cfg.DndUntil = Fmt.ToUnix(target.ToUniversalTime());
            SaveConfig();
            CheckDnd();
        }

        public void DndForever()
        {
            Cfg.Dnd = true;
            Cfg.DndUntil = 0;
            SaveConfig();
            CheckDnd();
        }

        public void DndOff()
        {
            Cfg.Dnd = false;
            Cfg.DndUntil = 0;
            SaveConfig();
            CheckDnd();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);

        static bool IsFullscreenApp()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return false;
            var cls = new StringBuilder(128);
            GetClassName(h, cls, cls.Capacity);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            RECT r;
            if (!GetWindowRect(h, out r)) return false;
            Screen s = Screen.FromHandle(h);
            Rectangle b = s.Bounds;
            return r.Left <= b.Left + 2 && r.Top <= b.Top + 2 && r.Right >= b.Right - 2 && r.Bottom >= b.Bottom - 2;
        }

        void Balloon(string title, string text)
        {
            if (tray == null) return;
            try
            {
                tray.BalloonTipTitle = title;
                tray.BalloonTipText = Fmt.Truncate(text, 200);
                tray.BalloonTipIcon = ToolTipIcon.Info;
                tray.ShowBalloonTip(6000);
            }
            catch { }
        }

        // ---------- 显示 / 隐藏 ----------

        public void ShowPet()
        {
            hiddenByUser = false;
            if (Pet == null) return;
            Pet.ShowPet();
            UpdateTray();
        }

        public void HideToTray()
        {
            hiddenByUser = true;
            if (Pet != null) Pet.HidePet();
        }

        public void ToggleShow()
        {
            if (Pet == null) return;
            if (Pet.Visible) HideToTray();
            else ShowPet();
        }

        public void PrimaryAction()
        {
            if (!Cfg.HasKey) OpenSettings(true);
            else RefreshNow(true);
        }

        // ---------- 设置 ----------

        public void OpenSettings(bool firstRun = false)
        {
            using (var f = new SettingsForm(Cfg, firstRun))
            {
                f.TopMost = true;
                if (Pet != null) Pet.TopMostAgain();
                var r = f.ShowDialog();
                if (r == DialogResult.OK)
                {
                    string oldKey = Cfg.GetKey();
                    Cfg = f.Result;
                    SaveConfig();
                    ResetRefreshTimer();
                    RebuildVm();
                    UpdateTray();
                    ShowPet();
                    if (Pet != null) Pet.ResetReminderSchedule();
                    CheckDnd();
                    string newKey = Cfg.GetKey();
                    if (oldKey != newKey || firstRun) RefreshNow(false);
                }
            }
        }

        // ---------- DSH ----------

        void CheckDsh()
        {
            bool now = DshRunning();
            if (now == dshRunning) return;
            dshRunning = now;
            dshWas = now;
            RebuildVm();
            if (!Cfg.FollowDsh) return;
            if (now)
            {
                if (!hiddenByUser) ShowPet();
            }
            else if (Cfg.HideWithDsh)
            {
                if (Pet != null) Pet.HidePet();
            }
        }

        static bool DshRunning()
        {
            int self = Process.GetCurrentProcess().Id;
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return false; }
            foreach (Process p in all)
            {
                try
                {
                    if (p.Id == self) continue;
                    string n = p.ProcessName;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.IndexOf("DeepSeek", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (n.IndexOf("Whale", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    return true;
                }
                catch { }
                finally { p.Dispose(); }
            }
            return false;
        }

        // ---------- 退出 ----------

        public void Quit()
        {
            if (quitting) return;
            quitting = true;
            try { SaveConfig(); } catch { }
            try
            {
                if (tray != null)
                {
                    tray.Visible = false;
                    tray.Dispose();
                    tray = null;
                }
            }
            catch { }
            try
            {
                foreach (var ic in moodIcons.Values) ic.Dispose();
                foreach (var h in iconHandles) DestroyIcon(h);
                moodIcons.Clear();
                iconHandles.Clear();
            }
            catch { }
            try { if (Pet != null) Pet.HidePet(); } catch { }
            ExitThread();
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        protected override void Dispose(bool disposing)
        {
            if (disposing) Quit();
            base.Dispose(disposing);
        }
    }
}
