using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    public class PetWindow : Form
    {
        readonly TrayApp app;
        float scale = 1f;
        bool expanded;
        bool usageTab;
        bool cannonOpen;
        bool mini;
        bool clickThrough;
        Timer frameTimer;
        Timer pollTimer;
        Timer clickTimer;
        bool whaleClickPending;
        double reactStart = -1;
        Bitmap frame;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        PointF hover = new PointF(-999f, -999f);
        HitArea hoverHit = HitArea.None;
        bool dragging;
        bool dragMoved;
        Point dragStart;
        Point winStart;
        Rectangle compactRect;
        ContextMenuStrip menu;
        bool menuOpen;
        readonly PetPose pose = new PetPose();
        readonly Random rand = new Random();
        double nextActionAt;
        double actionStart;
        double actionDuration;
        double wakeUntil;
        double dropStart = -1;

        enum TripPhase { None, Out, Dwell, Back }
        TripPhase trip;
        double tripStart;
        double tripDur;
        double dwellUntil;
        double tripDwellSeconds;
        bool patrolMoving;
        Point patrolFrom;
        Point patrolTo;
        double patrolStart;
        double patrolDur;
        double patrolPauseUntil;
        Point tripFrom;
        Point tripTo;
        Point centerAnchor;
        string tripKind = "";
        Rectangle homeRect;
        double nextWaterAt;
        double nextWanderAt;
        BubbleWindow bubble;

        public PetWindow(TrayApp app)
        {
            this.app = app;
            mini = app.Cfg.Mini;
            pose.Skin = PetPose.ParseSkin(app.Cfg.Skin);
            nextActionAt = 8 + rand.NextDouble() * 4;
            nextWaterAt = clock.Elapsed.TotalSeconds + 60.0 * Math.Max(15, app.Cfg.WaterIntervalMin);
            nextWanderAt = 120 + rand.NextDouble() * 120;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(12, 18, 30);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            Text = "DeepSeek 小鲸鱼";

            menu = BuildMenu();

            frameTimer = new Timer();
            frameTimer.Interval = 60;
            frameTimer.Tick += delegate { OnFrame(); };
            frameTimer.Start();

            pollTimer = new Timer();
            pollTimer.Interval = 40;
            pollTimer.Tick += delegate { OnPoll(); };
            pollTimer.Start();

            clickTimer = new Timer();
            clickTimer.Interval = 300;
            clickTimer.Tick += delegate
            {
                clickTimer.Stop();
                if (whaleClickPending)
                {
                    whaleClickPending = false;
                    DoReact();
                }
            };

            var h = Handle; // force creation so BeginInvoke works while hidden
            RebuildFrame();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                using (var g = CreateGraphics())
                {
                    scale = g.DpiX / 96f;
                }
            }
            catch { scale = 1f; }
            if (scale < 0.75f) scale = 1f;
            InitializeBounds();
            RebuildFrame();
            RegisterHotkeys();
        }

        void RegisterHotkeys()
        {
            try
            {
                showHotkeyId = TryRegister(1, new int[][]
                {
                    new int[] { MOD_CONTROL | MOD_ALT, (int)Keys.D },
                    new int[] { MOD_CONTROL | MOD_ALT | MOD_SHIFT, (int)Keys.D },
                    new int[] { MOD_CONTROL | MOD_ALT, (int)Keys.W },
                    new int[] { MOD_CONTROL | MOD_ALT | MOD_SHIFT, (int)Keys.W }
                }, out showHotkeyLabel);
                miniHotkeyId = TryRegister(2, new int[][]
                {
                    new int[] { MOD_CONTROL | MOD_ALT, (int)Keys.M },
                    new int[] { MOD_CONTROL | MOD_ALT | MOD_SHIFT, (int)Keys.M },
                    new int[] { MOD_CONTROL | MOD_ALT, (int)Keys.F9 },
                    new int[] { MOD_CONTROL | MOD_ALT | MOD_SHIFT, (int)Keys.F9 }
                }, out miniHotkeyLabel);
                Log.Info("hotkeys show=" + showHotkeyLabel + " mini=" + miniHotkeyLabel);
                if (MenuItem_Hotkeys != null)
                {
                    string t = "";
                    if (!string.IsNullOrEmpty(showHotkeyLabel)) t = showHotkeyLabel + " 显示/隐藏";
                    if (!string.IsNullOrEmpty(miniHotkeyLabel)) t += (t.Length > 0 ? " · " : "") + miniHotkeyLabel + " 迷你";
                    MenuItem_Hotkeys.Text = t;
                    MenuItem_Hotkeys.Visible = t.Length > 0;
                }
            }
            catch (Exception ex) { Log.Warn("hotkey register failed: " + ex.Message); }
        }

        int showHotkeyId;
        int miniHotkeyId;
        string showHotkeyLabel = "";
        string miniHotkeyLabel = "";

        int TryRegister(int id, int[][] combos, out string label)
        {
            label = "";
            foreach (int[] c in combos)
            {
                if (RegisterHotKey(Handle, id, c[0] | MOD_NOREPEAT, c[1]))
                {
                    label = ModLabel(c[0]) + ((Keys)c[1]).ToString();
                    return id;
                }
            }
            return -1;
        }

        static string ModLabel(int mods)
        {
            string s = "";
            if ((mods & MOD_CONTROL) != 0) s += "Ctrl+";
            if ((mods & MOD_ALT) != 0) s += "Alt+";
            if ((mods & MOD_SHIFT) != 0) s += "Shift+";
            return s;
        }

        const int MOD_ALT = 0x0001;
        const int MOD_CONTROL = 0x0002;
        const int MOD_SHIFT = 0x0004;
        const int MOD_NOREPEAT = 0x4000;
        const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) app.ToggleShow();
                else if (id == 2) ToggleMini();
                return;
            }
            base.WndProc(ref m);
        }

        void InitializeBounds()
        {
            Size sz = ScaledSize();
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = app.Cfg.X >= 0 ? app.Cfg.X : wa.Right - sz.Width - 28;
            int y = app.Cfg.Y >= 0 ? app.Cfg.Y : wa.Bottom - sz.Height - 28;
            Bounds = ClampToScreen(new Rectangle(x, y, sz.Width, sz.Height));
            compactRect = Bounds;
        }

        Size ScaledSize()
        {
            float w, h;
            if (expanded) { w = Geom.ExpandedW; h = Geom.ExpandedH; }
            else if (mini) { w = Geom.MiniW; h = Geom.MiniH; }
            else { w = Geom.CompactW; h = Geom.CompactH; }
            return new Size((int)Math.Round(w * scale), (int)Math.Round(h * scale));
        }

        Rectangle ClampToScreen(Rectangle r)
        {
            Screen s = Screen.FromRectangle(r);
            Rectangle wa = s.WorkingArea;
            if (r.Width > wa.Width) r.Width = wa.Width;
            if (r.Height > wa.Height) r.Height = wa.Height;
            if (r.Right > wa.Right) r.X = wa.Right - r.Width;
            if (r.Bottom > wa.Bottom) r.Y = wa.Bottom - r.Height;
            if (r.Left < wa.Left) r.X = wa.Left;
            if (r.Top < wa.Top) r.Y = wa.Top;
            return r;
        }

        // ---------- 显示控制 ----------

        public void ShowPet()
        {
            Log.Info("ShowPet bounds=" + Bounds + " scale=" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " screen=" + Screen.PrimaryScreen.Bounds);
            if (!Visible)
            {
                Show();
                ClickThrough(false);
            }
            TopMostAgain();
            Present();
        }

        public void HidePet()
        {
            CancelTrip(true);
            if (bubble != null) bubble.HideBubble();
            if (Visible) Hide();
        }

        public bool IsExpanded { get { return expanded; } }

        public void ToggleExpanded()
        {
            SetExpanded(!expanded);
        }

        public void SetExpanded(bool on)
        {
            if (on == expanded) return;
            if (on) CancelTrip(false);
            if (on && bubble != null) bubble.HideBubble();
            if (on)
            {
                pose.Action = PetAction.None;
                compactRect = Bounds;
                int w = (int)Math.Round(Geom.ExpandedW * scale);
                int h = (int)Math.Round(Geom.ExpandedH * scale);
                int x = Bounds.Right - w;
                int y = Bounds.Bottom - h;
                Bounds = ClampToScreen(new Rectangle(x, y, w, h));
                expanded = true;
            }
            else
            {
                var r = compactRect;
                if (r.Width <= 0)
                {
                    Size sz = ScaledSize();
                    r = new Rectangle(Bounds.Right - sz.Width, Bounds.Bottom - sz.Height, sz.Width, sz.Height);
                }
                Bounds = ClampToScreen(r);
                expanded = false;
            }
            RebuildFrame();
            Present();
        }

        public void ToggleMini()
        {
            CancelTrip(false);
            if (bubble != null) bubble.HideBubble();
            if (expanded) expanded = false;
            pose.Action = PetAction.None;
            mini = !mini;
            app.Cfg.Mini = mini;
            app.SaveConfig();
            Size sz = ScaledSize();
            int right = Bounds.Right;
            int bottom = Bounds.Bottom;
            var r = ClampToScreen(new Rectangle(right - sz.Width, bottom - sz.Height, sz.Width, sz.Height));
            Bounds = r;
            compactRect = r;
            RebuildFrame();
            Present();
        }

        public bool IsMini { get { return mini; } }

        public void TopMostAgain()
        {
            if (!IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void RefreshNow()
        {
            app.RefreshNow(true);
        }

        public void ShowUsageTab()
        {
            usageTab = true;
            if (expanded) Present();
        }

        public void Redraw()
        {
            Present();
        }

        public void Say(string text, double seconds)
        {
            if (!Visible || expanded || string.IsNullOrEmpty(text)) return;
            if (bubble == null) bubble = new BubbleWindow();
            bubble.Say(text, seconds, this);
        }

        public void CaptureAfter(int ms, string path)
        {
            var t = new Timer();
            t.Interval = ms;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                SaveShot(path);
            };
            t.Start();
        }

        void SaveShot(string path)
        {
            Present();
            try
            {
                if (frame != null) frame.Save(path);
            }
            catch (Exception ex) { Log.Warn("shot frame failed: " + ex.Message); }
            try
            {
                using (var cap = new Bitmap(Width, Height))
                {
                    using (var cg = Graphics.FromImage(cap))
                        cg.CopyFromScreen(Left, Top, 0, 0, new Size(Width, Height));
                    cap.Save(path + ".screen.png");
                }
            }
            catch (Exception ex) { Log.Warn("shot screen failed: " + ex.Message); }
            Log.Info("shot saved " + path);
        }

        // ---------- 渲染 ----------

        void RebuildFrame()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            if (frame != null) frame.Dispose();
            frame = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            frame.SetResolution(96f, 96f);
        }

        void OnFrame()
        {
            if (!Visible) return;
            if (app.Vm.Message != null && app.Vm.MessageUntil <= Fmt.NowUnix())
                app.ClearMessage();
            UpdatePose();
            Present();
        }

        void UpdatePose()
        {
            double now = clock.Elapsed.TotalSeconds;
            pose.Skin = PetPose.ParseSkin(app.Cfg.Skin);

            if (dropStart >= 0)
            {
                double dp = (now - dropStart) / 0.45;
                if (dp >= 1)
                {
                    dropStart = -1;
                    pose.DropT = -1f;
                }
                else pose.DropT = (float)dp;
            }

            if (trip != TripPhase.None)
            {
                UpdateTrip(now);
                return;
            }

            if (app.DndActive && trip != TripPhase.None) CancelTrip(true);
            if (trip != TripPhase.None)
            {
                UpdateTrip(now);
                return;
            }

            int hour = DateTime.Now.Hour;
            bool night = hour >= 23 || hour < 7;
            bool sleepyMood = app.Vm.Mood == Mood.Happy || app.Vm.Mood == Mood.Calm || app.Vm.Mood == Mood.Offline;
            bool canSleep = night && sleepyMood && !expanded && !dragging && now > wakeUntil;
            pose.Sleeping = canSleep;
            if (app.DndActive)
            {
                pose.Action = PetAction.None;
                pose.Walking = false;
                return;
            }
            if (canSleep)
            {
                pose.Action = PetAction.None;
                return;
            }

            if (pose.Action != PetAction.None)
            {
                double p = (now - actionStart) / actionDuration;
                if (p >= 1)
                {
                    pose.Action = PetAction.None;
                    pose.ActionT = 0f;
                    nextActionAt = now + 7 + rand.NextDouble() * 7;
                }
                else pose.ActionT = (float)p;
                return;
            }

            if (!expanded && !dragging && now >= nextActionAt) StartAction(now);

            if (app.Cfg.WaterReminder && now >= nextWaterAt && CanTrip(now) && InReminderWindow(hour))
                StartWaterTrip();

            if (now >= nextWanderAt)
            {
                nextWanderAt = now + 150 + rand.NextDouble() * 180;
                if (app.Cfg.Wander && CanTrip(now) && IdleSeconds() > 90 && rand.NextDouble() < 0.6)
                    StartStroll();
            }
        }

        bool InReminderWindow(int hour)
        {
            int s = app.Cfg.ReminderStartHour;
            int e = app.Cfg.ReminderEndHour;
            if (s == e) return true;
            if (s < e) return hour >= s && hour < e;
            return hour >= s || hour < e;
        }

        bool CanTrip(double now)
        {
            return trip == TripPhase.None && !expanded && !dragging && !menuOpen && Visible
                && !pose.Sleeping && now > wakeUntil;
        }

        void UpdateTrip(double now)
        {
            if (trip == TripPhase.Out || trip == TripPhase.Back)
            {
                double p = (now - tripStart) / tripDur;
                if (p >= 1)
                {
                    Bounds = ClampToScreen(new Rectangle(tripTo.X, tripTo.Y, Width, Height));
                    pose.Walking = false;
                    if (trip == TripPhase.Out)
                    {
                        BeginDwell(now);
                    }
                    else
                    {
                        trip = TripPhase.None;
                        compactRect = homeRect;
                        nextWanderAt = now + 180 + rand.NextDouble() * 240;
                    }
                    return;
                }
                float e = Ease01((float)p);
                int x = (int)Math.Round(tripFrom.X + (tripTo.X - tripFrom.X) * (double)e);
                int y = (int)Math.Round(tripFrom.Y + (tripTo.Y - tripFrom.Y) * (double)e);
                Bounds = ClampToScreen(new Rectangle(x, y, Width, Height));
                pose.Walking = true;
                pose.WalkPhase = (float)((now * 7.5) % 1.0);
                pose.Facing = tripTo.X >= tripFrom.X ? 1 : -1;
            }
            else if (trip == TripPhase.Dwell)
            {
                UpdateDwell(now);
            }
        }

        void BeginDwell(double now)
        {
            trip = TripPhase.Dwell;
            centerAnchor = Bounds.Location;
            dwellUntil = now + tripDwellSeconds;
            patrolMoving = false;
            patrolPauseUntil = now + 1.2;
            pose.Walking = false;
            if (tripKind == "water")
            {
                Say(PickReminderLine(), 8);
                Log.Info("water reminder arrived");
            }
            Log.Info("dwell " + tripKind + " " + tripDwellSeconds + "s");
        }

        void UpdateDwell(double now)
        {
            if (patrolMoving)
            {
                double p = (now - patrolStart) / patrolDur;
                if (p >= 1)
                {
                    Bounds = ClampToScreen(new Rectangle(patrolTo.X, patrolTo.Y, Width, Height));
                    patrolMoving = false;
                    pose.Walking = false;
                    patrolPauseUntil = now + 1.4 + rand.NextDouble() * 1.4;
                }
                else
                {
                    float e = Ease01((float)p);
                    int x = (int)Math.Round(patrolFrom.X + (patrolTo.X - patrolFrom.X) * (double)e);
                    int y = (int)Math.Round(patrolFrom.Y + (patrolTo.Y - patrolFrom.Y) * (double)e);
                    Bounds = ClampToScreen(new Rectangle(x, y, Width, Height));
                    pose.Walking = true;
                    pose.WalkPhase = (float)((now * 7.5) % 1.0);
                }
                return;
            }
            if (now >= dwellUntil)
            {
                StartBack(now);
                return;
            }
            if (tripKind == "water" && now >= patrolPauseUntil)
                StartPatrol(now);
        }

        void StartPatrol(double now)
        {
            float dx = (float)((rand.NextDouble() * 2 - 1) * 150 * scale);
            float dy = (float)((rand.NextDouble() * 2 - 1) * 80 * scale);
            Rectangle target = ClampToScreen(new Rectangle(centerAnchor.X + (int)dx, centerAnchor.Y + (int)dy, Width, Height));
            double dist = Math.Sqrt((target.X - Bounds.X) * (double)(target.X - Bounds.X)
                + (target.Y - Bounds.Y) * (double)(target.Y - Bounds.Y));
            if (dist < 60 * scale)
            {
                patrolPauseUntil = now + 0.8;
                return;
            }
            patrolFrom = Bounds.Location;
            patrolTo = target.Location;
            patrolStart = now;
            patrolDur = Math.Max(0.35, Math.Min(1.2, dist / (380.0 * scale)));
            patrolMoving = true;
            pose.Facing = patrolTo.X >= patrolFrom.X ? 1 : -1;
            Log.Info("water patrol");
        }

        void AcknowledgeWater()
        {
            if (trip != TripPhase.Out && trip != TripPhase.Dwell) return;
            Say("好嘞，回去啦～", 2.5);
            StartBack(clock.Elapsed.TotalSeconds);
            Log.Info("water acknowledged");
        }

        string PickReminderLine()
        {
            string raw = app.Cfg.ReminderText ?? "";
            string[] parts = raw.Split('\n');
            var list = new System.Collections.Generic.List<string>();
            foreach (string s in parts)
            {
                string t = (s ?? "").Trim();
                if (t.Length > 0) list.Add(t);
            }
            if (list.Count == 0) return "该喝水啦～";
            return list[rand.Next(list.Count)];
        }

        static float Ease01(float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        void StartTrip(Point to, string kind, double dwellSec)
        {
            double now = clock.Elapsed.TotalSeconds;
            homeRect = compactRect.Width > 0 ? compactRect : Bounds;
            tripFrom = Bounds.Location;
            Rectangle target = ClampToScreen(new Rectangle(to.X, to.Y, Width, Height));
            tripTo = target.Location;
            tripKind = kind;
            tripDwellSeconds = dwellSec;
            double dist = Math.Sqrt((tripTo.X - tripFrom.X) * (double)(tripTo.X - tripFrom.X)
                + (tripTo.Y - tripFrom.Y) * (double)(tripTo.Y - tripFrom.Y));
            tripDur = Math.Max(0.7, Math.Min(4.2, dist / (520.0 * scale)));
            tripStart = now;
            trip = TripPhase.Out;
            pose.Action = PetAction.None;
            pose.Sleeping = false;
            wakeUntil = now + 15;
            Log.Info("trip start " + kind + " -> " + tripTo);
        }

        void StartBack(double now)
        {
            tripFrom = Bounds.Location;
            tripTo = homeRect.Location;
            double dist = Math.Sqrt((tripTo.X - tripFrom.X) * (double)(tripTo.X - tripFrom.X)
                + (tripTo.Y - tripFrom.Y) * (double)(tripTo.Y - tripFrom.Y));
            tripDur = Math.Max(0.7, Math.Min(4.2, dist / (520.0 * scale)));
            tripStart = now;
            trip = TripPhase.Back;
            pose.Walking = true;
            pose.Facing = tripTo.X >= tripFrom.X ? 1 : -1;
            Log.Info("trip back " + tripKind);
        }

        void StartWaterTrip()
        {
            Rectangle wa = Screen.FromRectangle(Bounds).WorkingArea;
            int x = wa.Left + (wa.Width - Width) / 2;
            int y = wa.Top + (int)(wa.Height * 0.42) - Height / 2;
            nextWaterAt = clock.Elapsed.TotalSeconds + 60.0 * Math.Max(15, app.Cfg.WaterIntervalMin);
            StartTrip(new Point(x, y), "water", Math.Max(5, app.Cfg.DwellSeconds));
        }

        void StartStroll()
        {
            int dx = (int)((rand.NextDouble() * 2 - 1) * 220 * scale);
            int dy = (int)((rand.NextDouble() * 2 - 1) * 90 * scale);
            Point home = compactRect.Width > 0 ? compactRect.Location : Bounds.Location;
            nextWanderAt = clock.Elapsed.TotalSeconds + 240 + rand.NextDouble() * 240;
            StartTrip(new Point(home.X + dx, home.Y + dy), "stroll", 2.5 + rand.NextDouble() * 2.0);
        }

        public void EnterDnd()
        {
            CancelTrip(true);
            if (bubble != null) bubble.HideBubble();
            pose.Action = PetAction.None;
            pose.Walking = false;
        }

        public void ResetReminderSchedule()
        {
            nextWaterAt = clock.Elapsed.TotalSeconds + 60.0 * Math.Max(15, app.Cfg.WaterIntervalMin);
            nextWanderAt = clock.Elapsed.TotalSeconds + 120;
        }

        public float WindowScale
        {
            get { return scale; }
        }

        public Point MouthPoint()
        {
            float fx = pose.Facing >= 0 ? 0.77f : 0.23f;
            float fy = mini ? 0.55f : 0.46f;
            return new Point(Bounds.X + (int)(Width * fx), Bounds.Y + (int)(Height * fy));
        }

        public void AimAt(Point screen)
        {
            if (!Visible) return;
            int f = screen.X >= Bounds.X + Width / 2 ? 1 : -1;
            if (pose.Facing != f)
            {
                pose.Facing = f;
                Present();
            }
        }

        public void BringToTop()
        {
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void EnterCannonMode()
        {
            if (!Visible || cannonOpen) return;
            if (expanded) SetExpanded(false);
            CancelTrip(true);
            if (bubble != null) bubble.HideBubble();
            cannonOpen = true;
            var form = new CannonTargetForm(this, delegate(Point? p)
            {
                cannonOpen = false;
                if (p.HasValue) StartCannonShot(p.Value);
            });
            form.Show();
        }

        void StartCannonShot(Point target)
        {
            double now = clock.Elapsed.TotalSeconds;
            pose.Action = PetAction.Shoot;
            actionStart = now;
            actionDuration = 1.35;
            pose.ActionT = 0;
            pose.Sleeping = false;
            wakeUntil = now + 10;
            pose.Facing = target.X >= Bounds.X + Width / 2 ? 1 : -1;
            Present();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(130);
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (Visible) WaterCannon.Fire(this, target);
                    }));
                }
                catch { }
            });
        }

        public void DemoMenu()
        {
            ShowMenu();
        }

        public void DemoCannon()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            Point p = new Point(Bounds.X - (int)(320 * scale), Bounds.Y + Height / 2);
            p.X = Math.Max(vs.Left + 80, p.X);
            StartCannonShot(p);
        }

        public void TriggerWater()
        {
            if (expanded) SetExpanded(false);
            if (trip != TripPhase.None) return;
            StartWaterTrip();
        }

        public void TriggerStroll()
        {
            if (expanded) SetExpanded(false);
            if (trip != TripPhase.None) return;
            StartStroll();
        }

        void CancelTrip(bool goHome)
        {
            if (trip == TripPhase.None) return;
            trip = TripPhase.None;
            pose.Walking = false;
            if (goHome && homeRect.Width > 0)
            {
                Bounds = ClampToScreen(homeRect);
                compactRect = homeRect;
            }
            else
            {
                compactRect = Bounds;
            }
        }

        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        static double IdleSeconds()
        {
            var li = new LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(li);
            if (!GetLastInputInfo(ref li)) return 0;
            return (uint)(Environment.TickCount - (int)li.dwTime) / 1000.0;
        }

        void StartAction(double now)
        {
            int r = rand.Next(100);
            PetAction a;
            double dur;
            if (r < 35) { a = PetAction.Bubbles; dur = 2.2; }
            else if (r < 55) { a = PetAction.Yawn; dur = 1.8; }
            else if (r < 75) { a = PetAction.Stretch; dur = 2.0; }
            else if (r < 85) { a = PetAction.Spin; dur = 1.8; }
            else { a = PetAction.Jump; dur = 1.2; }
            pose.Action = a;
            pose.ActionT = 0f;
            actionStart = now;
            actionDuration = dur;
            Log.Info("idle action " + a);
        }

        void Present()
        {
            if (!IsHandleCreated || !Visible || Width <= 0 || Height <= 0) return;
            if (frame == null || frame.Width != Width || frame.Height != Height) RebuildFrame();
            if (frame == null) return;

            double t = clock.Elapsed.TotalSeconds;
            try
            {
                using (var g = Graphics.FromImage(frame))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.ScaleTransform(scale, scale);
                    float react = ReactProgress();
                    if (expanded) Renderer.DrawExpanded(g, app.Vm, t, hover, pose, usageTab);
                    else if (mini) Renderer.DrawMini(g, app.Vm, t, hover, react, pose);
                    else Renderer.DrawCompact(g, app.Vm, t, hover, react, pose);
                }
                Blit();
            }
            catch (Exception ex)
            {
                Log.Warn("present failed: " + ex.Message);
            }
        }

        float ReactProgress()
        {
            if (reactStart < 0) return -1f;
            double p = (clock.Elapsed.TotalSeconds - reactStart) / 2.0;
            if (p >= 1.0)
            {
                reactStart = -1;
                return -1f;
            }
            return (float)p;
        }

        void DoReact()
        {
            reactStart = clock.Elapsed.TotalSeconds;
            wakeUntil = clock.Elapsed.TotalSeconds + 8;
            pose.Sleeping = false;
            string[] lines = { "我在呢～", "摸头杀！", "别摸啦，会掉鳞的", "今天也省着点用哦", "余额健康，放心写", "嘿嘿～" };
            app.Say(lines[(int)(DateTime.Now.Ticks % lines.Length)], 3.2);
        }

        bool blitWarned;
        bool frameSaved;
        public static string DebugFramePath;
        void Blit()
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                hBmp = frame.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBmp);
                var dst = new Native.POINT(Left, Top);
                var srcPoint = new Native.POINT(0, 0);
                var size = new Native.SIZE(Width, Height);
                var blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                bool ok = Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref srcPoint, 0, ref blend, (int)Native.ULW_ALPHA);
                if (!ok && !blitWarned)
                {
                    blitWarned = true;
                    Log.Warn("UpdateLayeredWindow failed: " + Marshal.GetLastWin32Error());
                }
                if (!frameSaved && !string.IsNullOrEmpty(DebugFramePath))
                {
                    frameSaved = true;
                    try
                    {
                        frame.Save(DebugFramePath);
                        using (var back = Image.FromHbitmap(hBmp))
                        {
                            back.Save(DebugFramePath + ".hbitmap.png");
                        }
                        Log.Info("frame saved to " + DebugFramePath);
                        var capTimer = new Timer();
                        capTimer.Interval = 700;
                        capTimer.Tick += delegate
                        {
                            capTimer.Stop();
                            capTimer.Dispose();
                            try
                            {
                                using (var cap = new Bitmap(Width, Height))
                                {
                                    using (var cg = Graphics.FromImage(cap))
                                        cg.CopyFromScreen(Left, Top, 0, 0, new Size(Width, Height));
                                    cap.Save(DebugFramePath + ".screen.png");
                                }
                                Log.Info("screen capture saved");
                            }
                            catch (Exception ex) { Log.Warn("screen capture failed: " + ex.Message); }
                        };
                        capTimer.Start();
                    }
                    catch (Exception ex) { Log.Warn("frame save failed: " + ex.Message); }
                }
            }
            finally
            {
                if (old != IntPtr.Zero) Native.SelectObject(memDc, old);
                if (hBmp != IntPtr.Zero) Native.DeleteObject(hBmp);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // ---------- 鼠标 / 命中 ----------

        void OnPoll()
        {
            if (!Visible || menuOpen || dragging) return;
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return;
            bool inside = Bounds.Contains(p.X, p.Y);
            var lp = new PointF((p.X - Left) / scale, (p.Y - Top) / scale);
            var vm = app.Vm;
            HitArea hit = inside ? Renderer.HitTest(lp, expanded, mini, vm.KeySet, vm.HasData) : HitArea.None;

            ClickThrough(hit == HitArea.None);
            if (hit != hoverHit || inside != hoverIsInside)
            {
                hoverHit = hit;
                hoverIsInside = inside;
                hover = inside ? lp : new PointF(-999f, -999f);
                if (hit == HitArea.None) Cursor = Cursors.Default;
                else Cursor = Cursors.Hand;
                Present();
            }
        }

        bool hoverIsInside;

        void ClickThrough(bool on)
        {
            if (on == clickThrough) return;
            clickThrough = on;
            int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            int n = on ? (ex | Native.WS_EX_TRANSPARENT) : (ex & ~Native.WS_EX_TRANSPARENT);
            if (n != ex) Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, n);
        }

        HitArea HitAt(Point client)
        {
            var lp = new PointF(client.X / scale, client.Y / scale);
            return Renderer.HitTest(lp, expanded, mini, app.Vm.KeySet, app.Vm.HasData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            var hit = HitAt(e.Location);
            if (hit == HitArea.None) return;
            dragging = true;
            dragMoved = false;
            pose.Dragging = false;
            pose.DragTilt = 0f;
            pose.Action = PetAction.None;
            wakeUntil = clock.Elapsed.TotalSeconds + 12;
            dragStart = Cursor.Position;
            winStart = Location;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            Point p = Cursor.Position;
            int dx = p.X - dragStart.X;
            int dy = p.Y - dragStart.Y;
            if (!dragMoved && (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)) dragMoved = true;
            if (dragMoved)
            {
                if (trip != TripPhase.None) CancelTrip(false);
                pose.Dragging = true;
                pose.DragTilt = Math.Max(-20f, Math.Min(20f, dx * 0.6f));
                if (!app.Cfg.Locked)
                    Bounds = ClampToScreen(new Rectangle(winStart.X + dx, winStart.Y + dy, Width, Height));
                Present();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false;
            Capture = false;
            pose.Dragging = false;
            pose.DragTilt = 0f;
            nextActionAt = clock.Elapsed.TotalSeconds + 5;
            if (dragMoved)
            {
                dropStart = clock.Elapsed.TotalSeconds;
                pose.DropT = 0f;
                if (!expanded)
                {
                    compactRect = Bounds;
                    app.SaveWindowPos(Bounds.X, Bounds.Y);
                }
                return;
            }
            if ((trip == TripPhase.Out || trip == TripPhase.Dwell) && tripKind == "water")
            {
                AcknowledgeWater();
                return;
            }
            if (trip == TripPhase.Back) return;
            if (trip != TripPhase.None) CancelTrip(false);
            HandleClick(HitAt(e.Location), e.Location);
        }

        void HandleClick(HitArea hit, Point client)
        {
            switch (hit)
            {
                case HitArea.Body:
                    OnWhaleClick();
                    break;
                case HitArea.Pill:
                    ToggleExpanded();
                    break;
                case HitArea.Close:
                    SetExpanded(false);
                    break;
                case HitArea.Refresh:
                    app.RefreshNow(true);
                    break;
                case HitArea.Settings:
                    app.OpenSettings();
                    break;
                case HitArea.Setup:
                    app.PrimaryAction();
                    break;
                case HitArea.Card:
                    HandleUsageCardClick(client);
                    break;
                case HitArea.TabBalance:
                    usageTab = false;
                    Present();
                    break;
                case HitArea.TabUsage:
                    usageTab = true;
                    app.RefreshDsh();
                    Present();
                    break;
            }
        }

        void OnWhaleClick()
        {
            if (whaleClickPending)
            {
                whaleClickPending = false;
                clickTimer.Stop();
                ToggleExpanded();
                return;
            }
            whaleClickPending = true;
            clickTimer.Start();
        }

        void HandleUsageCardClick(Point client)
        {
            if (!usageTab || app.Vm == null || app.Vm.Dsh == null) return;
            var dsh = app.Vm.Dsh;
            var lp = new PointF(client.X / scale, client.Y / scale);
            if (dsh.Pinned && lp.Y >= 202f && lp.Y <= 218f && lp.X >= 186f)
            {
                app.PinDshSession(null);
                return;
            }
            if (lp.Y < 214f || lp.Y > 264f) return;
            int idx = (int)((lp.Y - 218f) / 16f);
            if (idx < 0 || idx > 2) return;
            DshSession target = null;
            int n = 0;
            foreach (var s in dsh.Sessions)
            {
                if (s.Total <= 0 || s == dsh.Current) continue;
                if (n == idx) { target = s; break; }
                n++;
            }
            if (target != null) app.PinDshSession(target.Id);
        }

        void ShowMenu()
        {
            if (menu == null) return;
            MenuItem_Expand.Text = expanded ? "收起详情" : "展开详情";
            MenuItem_Lock.Checked = app.Cfg.Locked;
            MenuItem_Auto.Checked = Startup.IsEnabled(Application.ExecutablePath);
            MenuItem_Mini.Checked = mini;
            MenuItem_Water.Checked = app.Cfg.WaterReminder;
            MenuItem_Wander.Checked = app.Cfg.Wander;
            if (MenuHeader != null)
            {
                string title = "DeepSeek 小鲸鱼";
                if (app.Vm != null && app.Vm.HasData)
                    title += "   " + app.Vm.Sym + Fmt.Number(app.Vm.Balance);
                MenuHeader.Text = title;
            }
            menuOpen = true;
            try
            {
                menu.Show(Cursor.Position);
            }
            finally
            {
                menuOpen = false;
            }
        }

        ToolStripMenuItem MenuItem_Expand;
        ToolStripMenuItem MenuItem_Lock;
        ToolStripMenuItem MenuItem_Auto;
        ToolStripMenuItem MenuItem_Mini;
        ToolStripMenuItem MenuItem_Hotkeys;
        ToolStripMenuItem MenuItem_Water;
        ToolStripMenuItem MenuItem_Wander;
        ToolStripLabel MenuHeader;

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            m.ShowImageMargin = false;
            m.ShowItemToolTips = true;
            m.Font = new Font("Microsoft YaHei UI", 9f);
            m.BackColor = Color.FromArgb(16, 24, 42);
            m.ForeColor = Color.FromArgb(230, 238, 250);
            m.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            ((ToolStripProfessionalRenderer)m.Renderer).RoundedEdges = false;

            MenuHeader = new ToolStripLabel("DeepSeek 小鲸鱼");
            MenuHeader.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
            MenuHeader.ForeColor = Color.FromArgb(150, 172, 205);
            MenuHeader.Margin = new Padding(7, 5, 7, 2);
            m.Items.Add(MenuHeader);
            m.Items.Add(new ToolStripSeparator());

            MenuItem_Expand = Item("展开详情", delegate { ToggleExpanded(); });
            m.Items.Add(MenuItem_Expand);
            MenuItem_Mini = Item("迷你模式", delegate { ToggleMini(); });
            m.Items.Add(MenuItem_Mini);
            m.Items.Add(new ToolStripSeparator());

            var cannon = Item("水炮模式", delegate { app.StartCannon(); });
            cannon.ToolTipText = "冲走桌面图标（视觉效果，不会改文件）";
            m.Items.Add(cannon);
            m.Items.Add(Item("提醒我喝水", delegate { TriggerWater(); }));
            m.Items.Add(Item("让它溜达一下", delegate { TriggerStroll(); }));
            m.Items.Add(new ToolStripSeparator());

            var remind = new ToolStripMenuItem("提醒与勿扰");
            remind.ForeColor = Color.FromArgb(230, 238, 250);
            StyleDrop(remind.DropDown);
            MenuItem_Water = Item("喝水提醒", delegate
            {
                app.Cfg.WaterReminder = !app.Cfg.WaterReminder;
                app.SaveConfig();
                if (app.Cfg.WaterReminder)
                    nextWaterAt = clock.Elapsed.TotalSeconds + 60.0 * Math.Max(15, app.Cfg.WaterIntervalMin);
            });
            MenuItem_Water.Checked = app.Cfg.WaterReminder;
            MenuItem_Wander = Item("自由溜达", delegate
            {
                app.Cfg.Wander = !app.Cfg.Wander;
                app.SaveConfig();
            });
            MenuItem_Wander.Checked = app.Cfg.Wander;
            remind.DropDownItems.Add(MenuItem_Water);
            remind.DropDownItems.Add(MenuItem_Wander);
            remind.DropDownItems.Add(new ToolStripSeparator());
            remind.DropDownItems.Add(app.CreateDndMenu());
            remind.DropDownItems.Add(new ToolStripSeparator());
            remind.DropDownItems.Add(Item("提醒设置…", delegate { app.OpenReminderSettings(); }));
            m.Items.Add(remind);

            m.Items.Add(Item("隐藏到托盘", delegate { app.HideToTray(); }));

            var more = new ToolStripMenuItem("更多");
            more.ForeColor = Color.FromArgb(230, 238, 250);
            StyleDrop(more.DropDown);
            more.DropDownItems.Add(Item("立即刷新", delegate { app.RefreshNow(true); }));
            MenuItem_Lock = Item("锁定位置", delegate
            {
                app.Cfg.Locked = !app.Cfg.Locked;
                app.SaveConfig();
                MenuItem_Lock.Checked = app.Cfg.Locked;
            });
            MenuItem_Lock.Checked = app.Cfg.Locked;
            more.DropDownItems.Add(MenuItem_Lock);
            MenuItem_Auto = Item("开机自启", delegate
            {
                if (Startup.IsEnabled(Application.ExecutablePath)) Startup.Disable();
                else Startup.Enable(Application.ExecutablePath);
                MenuItem_Auto.Checked = Startup.IsEnabled(Application.ExecutablePath);
            });
            MenuItem_Auto.Checked = Startup.IsEnabled(Application.ExecutablePath);
            more.DropDownItems.Add(MenuItem_Auto);
            m.Items.Add(more);

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item("设置…", delegate { app.OpenSettings(); }));
            var quit = Item("退出", delegate { app.Quit(); });
            quit.ForeColor = Color.FromArgb(255, 150, 150);
            m.Items.Add(quit);

            m.Items.Add(new ToolStripSeparator());
            MenuItem_Hotkeys = new ToolStripMenuItem("");
            MenuItem_Hotkeys.Enabled = false;
            MenuItem_Hotkeys.ForeColor = Color.FromArgb(110, 132, 165);
            MenuItem_Hotkeys.Font = new Font("Microsoft YaHei UI", 7f);
            MenuItem_Hotkeys.Visible = false;
            m.Items.Add(MenuItem_Hotkeys);
            return m;
        }

        ToolStripMenuItem Item(string text, EventHandler handler)
        {
            var it = new ToolStripMenuItem(text);
            it.ForeColor = Color.FromArgb(230, 238, 250);
            it.Click += handler;
            return it;
        }

        void StyleDrop(ToolStripDropDown d)
        {
            d.BackColor = Color.FromArgb(16, 24, 42);
            d.ForeColor = Color.FromArgb(230, 238, 250);
            d.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            ((ToolStripProfessionalRenderer)d.Renderer).RoundedEdges = false;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (showHotkeyId > 0) UnregisterHotKey(Handle, showHotkeyId);
                    if (miniHotkeyId > 0) UnregisterHotKey(Handle, miniHotkeyId);
                }
                catch { }
                if (frameTimer != null) frameTimer.Dispose();
                if (pollTimer != null) pollTimer.Dispose();
                if (clickTimer != null) clickTimer.Dispose();
                if (bubble != null) bubble.Dispose();
                if (frame != null) frame.Dispose();
                if (menu != null) menu.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(16, 24, 42); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(16, 24, 42); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(16, 24, 42); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(16, 24, 42); } }
        public override Color MenuBorder { get { return Color.FromArgb(52, 70, 104); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(70, 110, 200); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(38, 58, 100); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(38, 58, 100); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(38, 58, 100); } }
        public override Color SeparatorDark { get { return Color.FromArgb(40, 52, 78); } }
        public override Color SeparatorLight { get { return Color.FromArgb(40, 52, 78); } }
        public override Color CheckBackground { get { return Color.FromArgb(40, 60, 110); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(50, 80, 150); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(50, 80, 150); } }
    }
}
