using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    public static class DevRender
    {
        public static int Run(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                Application.EnableVisualStyles();
                RenderSettings(Path.Combine(dir, "settings.png"), true);
                RenderSettings(Path.Combine(dir, "settings_plain.png"), false);
                RenderReminder(Path.Combine(dir, "reminder.png"));
                var happy = MakeVm(42.8m, Mood.Happy, null, 200, true);
                var worried = MakeVm(8.2m, Mood.Worried, "余额偏低，记得充值～", 0, true);
                var sad = MakeVm(0m, Mood.Sad, "余额用完了", 0, true);
                var offline = MakeVm(42.8m, Mood.Offline, null, 0, false);
                var onboarding = EmptyVm(false);
                var empty = EmptyVm(true);

                SaveSheet(Path.Combine(dir, "sheet_compact.png"), new Vm[] { happy, worried, sad, offline });

                Save(Path.Combine(dir, "compact_happy.png"), 168f, 124f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawCompact(g, happy, 1.3, new PointF(-100, -100));
                });
                Save(Path.Combine(dir, "compact_worried.png"), 168f, 124f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawCompact(g, worried, 2.1, new PointF(-100, -100));
                });
                Save(Path.Combine(dir, "compact_offline.png"), 168f, 124f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawCompact(g, offline, 3.4, new PointF(-100, -100));
                });

                Save(Path.Combine(dir, "expanded_happy.png"), 300f, 378f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawExpanded(g, happy, 1.3, new PointF(-100, -100));
                });
                Save(Path.Combine(dir, "expanded_worried.png"), 300f, 378f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawExpanded(g, worried, 0.7, new PointF(240, 344));
                });
                Save(Path.Combine(dir, "expanded_onboarding.png"), 300f, 378f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawExpanded(g, onboarding, 1.3, new PointF(-100, -100));
                });
                Save(Path.Combine(dir, "expanded_empty.png"), 300f, 378f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawExpanded(g, empty, 1.3, new PointF(-100, -100));
                });

                var idle = IdleVm();
                Save(Path.Combine(dir, "expanded_idle.png"), 300f, 378f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawExpanded(g, idle, 1.3, new PointF(-100, -100));
                });

                var usageVm = MakeVm(42.8m, Mood.Happy, null, 200, true);                usageVm.Dsh = FakeDsh();
                usageVm.PeakNow = false;
                usageVm.PeakChip = "闲时 5折";
                usageVm.PeakHint = "距高峰 13 小时";
                usageVm.DailyAllowance = 8.8m;
                usageVm.RunOutDate = DateTime.Today.AddDays(18);
                Save(Path.Combine(dir, "expanded_usage.png"), 300f, 378f, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawExpanded(g, usageVm, 1.3, new PointF(-100, -100), null, true);
                });

                using (var bmp = new Bitmap(560, 560, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    bmp.SetResolution(96f, 96f);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        Renderer.DrawWhale(g, new RectangleF(40f, 40f, 480f, 440f), Mood.Happy, 1.3, false, 1f);
                    }
                    bmp.Save(Path.Combine(dir, "whale_sprite.png"));
                }

                var ringVm = MakeVm(42.8m, Mood.Happy, null, 200, true);
                Save(Path.Combine(dir, "mini.png"), Geom.MiniW, Geom.MiniH, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawMini(g, ringVm, 1.3, new PointF(-100, -100));
                });
                Save(Path.Combine(dir, "mini_react.png"), Geom.MiniW, Geom.MiniH, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawMini(g, ringVm, 1.3, new PointF(-100, -100), 0.35f);
                });
                Save(Path.Combine(dir, "compact_react.png"), Geom.CompactW, Geom.CompactH, 2f, 40f, delegate(Graphics g)
                {
                    Renderer.DrawCompact(g, ringVm, 1.3, new PointF(-100, -100), 0.35f);
                });

                SavePose(dir, "action_bubbles", ringVm, Pose(PetAction.Bubbles, 0.45f));
                SavePose(dir, "action_yawn", ringVm, Pose(PetAction.Yawn, 0.45f));
                SavePose(dir, "action_stretch", ringVm, Pose(PetAction.Stretch, 0.5f));
                SavePose(dir, "action_spin", ringVm, Pose(PetAction.Spin, 0.35f));
                SavePose(dir, "action_jump", ringVm, Pose(PetAction.Jump, 0.5f));
                SavePose(dir, "action_sleep", ringVm, new PetPose { Sleeping = true });
                SavePose(dir, "skin_sakura", ringVm, new PetPose { Skin = PetSkin.Sakura });
                SavePose(dir, "skin_mint", ringVm, new PetPose { Skin = PetSkin.Mint });
                SavePose(dir, "action_drag", ringVm, new PetPose { Dragging = true, DragTilt = 10f });
                SavePose(dir, "action_walk", ringVm, new PetPose { Walking = true, WalkPhase = 0.25f, Facing = -1 });

                Console.WriteLine("RENDER OK " + dir);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("RENDER FAIL " + ex);
                return 1;
            }
        }

        static void RenderSettings(string path, bool firstRun)
        {
            var cfg = new AppConfig();
            cfg.SetKey("sk-demo-key");
            cfg.LowBalance = 10;
            cfg.MonthlyBudget = 200;
            using (var f = new SettingsForm(cfg, firstRun))
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-5000, -5000);
                f.Show();
                Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(path);
                }
                f.Close();
            }
        }

        public static int RenderIcon(string path)
        {
            try
            {
                using (var bmp = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    bmp.SetResolution(96f, 96f);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = TextRenderingHint.AntiAlias;
                        g.Clear(Color.Transparent);
                        var r = new Rectangle(6, 6, 244, 244);
                        using (var p = RoundedRect(r, 56f))
                        {
                            using (var b = new LinearGradientBrush(r, Color.FromArgb(32, 48, 80), Color.FromArgb(9, 15, 27), 90f))
                                g.FillPath(b, p);
                            using (var pen = new Pen(Color.FromArgb(70, 120, 165, 255), 2f)) g.DrawPath(pen, p);
                        }
                        Renderer.DrawWhale(g, new RectangleF(30f, 34f, 196f, 196f), Mood.Calm, 2.0, false, 1f);
                    }
                    bmp.Save(path);
                }
                Console.WriteLine("ICON OK " + path);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ICON FAIL " + ex);
                return 1;
            }
        }

        static void RenderReminder(string path)
        {
            var cfg = new AppConfig();
            cfg.WaterIntervalMin = 60;
            cfg.DwellSeconds = 20;
            cfg.ReminderStartHour = 8;
            cfg.ReminderEndHour = 22;
            using (var f = new ReminderForm(cfg))
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-5000, -5000);
                f.Show();
                Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(path);
                }
                f.Close();
            }
        }

        static DshSnapshot FakeDsh()
        {
            var snap = new DshSnapshot();
            snap.Available = true;
            long now = Fmt.NowUnix();
            var cur = new DshSession
            {
                Id = "a",
                Title = "DeepSeek API 余额与消耗桌宠",
                Model = "deepseek-flash",
                CacheRead = 80820736,
                Uncached = 133045,
                Output = 383144,
                Total = 81336925,
                Turns = 4,
                Steps = 294,
                SurfaceTokens = 389215,
                ContextWindow = 1048576,
                Cost = 3.28m,
                LastPromptAt = now - 600
            };
            var b = new DshSession { Title = "Python 环境安装", CacheRead = 12100000, Uncached = 40000, Output = 90000, Total = 12230000, Turns = 6, Steps = 88, Cost = 0.42m, LastPromptAt = now - 5400 };
            var c = new DshSession { Title = "CTF 密码学练习", CacheRead = 9800000, Uncached = 30000, Output = 60000, Total = 9890000, Turns = 8, Steps = 120, Cost = 0.31m, LastPromptAt = now - 9000 };
            var d = new DshSession { Title = "周报整理", CacheRead = 4200000, Uncached = 20000, Output = 30000, Total = 4250000, Turns = 2, Steps = 20, Cost = 0.18m, LastPromptAt = now - 86400 };
            snap.Sessions.Add(cur);
            snap.Sessions.Add(b);
            snap.Sessions.Add(c);
            snap.Sessions.Add(d);
            snap.Current = cur;
            snap.TodayCost = 4.12m;
            snap.WeekCost = 18.2m;
            snap.MonthCost = 42m;
            snap.CacheSavings = 96.7m;
            snap.TodayTokens = 81336925;
            snap.TodaySessions = 5;
            return snap;
        }

        static Vm MakeVm(decimal targetBalance, Mood mood, string message, double budget, bool withTopups)
        {
            var h = new HistoryStore();
            h.Path = Path.Combine(Path.GetTempPath(), "dswhale-dev.json");
            var now = DateTime.Now;
            decimal bal = 300m;
            var list = new List<Sample>();
            for (int i = 45; i >= 0; i--)
            {
                double daily = 0.35 + 2.1 * Math.Abs(Math.Sin(i * 0.53)) + (i % 9 == 0 ? 1.4 : 0);
                bal -= (decimal)daily;
                long t;
                if (i == 0) t = Fmt.ToUnix(now);
                else t = Fmt.ToUnix(now.Date.AddDays(-i).AddHours(23).AddMinutes(10));
                if (i == 12 && withTopups)
                {
                    bal += 100m;
                    h.Topups.Add(new TopupEvent { T = t, C = "CNY", Amount = 100m });
                }
                var s = new Sample();
                s.T = t; s.C = "CNY"; s.Total = bal; s.Granted = 30m; s.Topped = Math.Max(0m, bal - 30m);
                list.Add(s);
            }
            decimal shift = targetBalance - bal;
            foreach (var s in list)
            {
                s.Total += shift;
                s.Granted = Math.Min(30m, Math.Max(0m, s.Total));
                s.Topped = Math.Max(0m, s.Total - s.Granted);
                h.Samples.Add(s);
            }
            if (withTopups && mood == Mood.Happy)
            {
                h.Topups.Clear();
                h.Topups.Add(new TopupEvent { T = Fmt.ToUnix(now.Date.AddDays(-12).AddHours(20)), C = "CNY", Amount = 100m });
                h.Topups.Add(new TopupEvent { T = Fmt.ToUnix(now.Date.AddDays(-27).AddHours(9)), C = "CNY", Amount = 50m });
            }

            var cfg = new AppConfig();
            cfg.SetKey("sk-dev-000000000000");
            cfg.LowBalance = 10;
            cfg.MonthlyBudget = budget;
            var last = new BalanceResult();
            last.T = Fmt.NowUnix();
            last.Available = mood != Mood.Sad;
            var cb = new CurrencyBalance();
            cb.Total = targetBalance;
            cb.Granted = Math.Min(30m, Math.Max(0m, targetBalance));
            cb.Topped = Math.Max(0m, targetBalance - cb.Granted);
            last.Cur["CNY"] = cb;
            if (mood == Mood.Offline) { last.Error = "连接不上 api.deepseek.com"; }
            var vm = VmFactory.Build(cfg, h, last, false, message,
                message == null ? 0 : Fmt.NowUnix() + 60, true);
            return vm;
        }

        static PetPose Pose(PetAction a, float t)
        {
            var p = new PetPose();
            p.Action = a;
            p.ActionT = t;
            return p;
        }

        static void SavePose(string dir, string name, Vm vm, PetPose pose)
        {
            Save(Path.Combine(dir, name + ".png"), Geom.CompactW, Geom.CompactH, 2f, 40f, delegate(Graphics g)
            {
                Renderer.DrawCompact(g, vm, 1.3, new PointF(-100, -100), -1f, pose);
            });
        }

        static Vm IdleVm()
        {
            var h = new HistoryStore();
            h.Path = Path.Combine(Path.GetTempPath(), "dswhale-idle.json");
            var now = DateTime.Now;
            for (int i = 5; i >= 0; i--)
            {
                var s = new Sample();
                s.T = Fmt.ToUnix(now.AddMinutes(-30 * i));
                s.C = "CNY";
                s.Total = 305.77m;
                s.Granted = 0m;
                s.Topped = 305.77m;
                h.Samples.Add(s);
            }
            var cfg = new AppConfig();
            cfg.SetKey("sk-dev-000000000000");
            cfg.LowBalance = 10;
            var last = new BalanceResult();
            last.T = Fmt.NowUnix();
            last.Available = true;
            last.Cur["CNY"] = new CurrencyBalance { Total = 305.77m, Topped = 305.77m };
            return VmFactory.Build(cfg, h, last, false, null, 0, true);
        }

        static Vm EmptyVm(bool withKey)
        {
            var cfg = new AppConfig();
            cfg.LowBalance = 10;
            if (withKey) cfg.SetKey("sk-dev-000000000000");
            return VmFactory.Build(cfg, new HistoryStore(), null, false, null, 0, false);
        }

        static void Save(string path, float w, float h, float scale, float margin, Action<Graphics> draw)
        {
            int cw = (int)(w * scale + margin * 2);
            int ch = (int)(h * scale + margin * 2);
            using (var bmp = new Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                bmp.SetResolution(96f, 96f);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.Clear(Color.Transparent);
                    PaintBackdrop(g, cw, ch);
                    var state = g.Save();
                    g.TranslateTransform(margin, margin);
                    g.ScaleTransform(scale, scale);
                    draw(g);
                    g.Restore(state);
                    bmp.Save(path);
                }
            }
        }

        static void SaveSheet(string path, Vm[] vms)
        {
            float s = 1.6f;
            int cellW = (int)(168 * s) + 28;
            int cellH = (int)(124 * s) + 28;
            int w = cellW * vms.Length;
            int h = cellH;
            using (var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                bmp.SetResolution(96f, 96f);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.Clear(Color.Transparent);
                    PaintBackdrop(g, w, h);
                    for (int i = 0; i < vms.Length; i++)
                    {
                        var st = g.Save();
                        g.TranslateTransform(i * cellW + 14, 14);
                        g.ScaleTransform(s, s);
                        Renderer.DrawCompact(g, vms[i], 1.3 + i * 1.1, new PointF(-1, -1));
                        g.Restore(st);
                    }
                    bmp.Save(path);
                }
            }
        }

        static void PaintBackdrop(Graphics g, int w, int h)
        {
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(255, 32, 44, 66), Color.FromArgb(255, 10, 14, 24), 90f))
                g.FillRectangle(b, 0, 0, w, h);
            using (var b = new PathGradientBrush(RoundedRect(new RectangleF(-w * 0.2f, -h * 0.3f, w * 0.9f, h * 0.9f), h * 0.45f)))
            {
                b.CenterColor = Color.FromArgb(50, 80, 130, 255);
                b.SurroundColors = new Color[] { Color.Transparent };
                g.FillRectangle(b, 0, 0, w, h);
            }
            using (var p = new Pen(Color.FromArgb(22, 255, 255, 255), 1f))
                g.DrawRectangle(p, 0.5f, 0.5f, w - 1f, h - 1f);
        }

        static GraphicsPath RoundedRect(RectangleF r, float rad)
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
