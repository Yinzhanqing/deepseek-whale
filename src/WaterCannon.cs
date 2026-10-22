using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    /// <summary>水炮模式：全屏瞄准 + 水柱 + 图标水泡飞走 + 原地一滩浅蓝水洼。</summary>
    public static class WaterCannon
    {
        static readonly List<PuddleForm> Puddles = new List<PuddleForm>();

        public static void Fire(PetWindow pet, Point target)
        {
            Point mouth = pet.MouthPoint();
            Bitmap bubble = CaptureBubble(target, pet.WindowScale);
            var effect = new CannonEffectForm(mouth, target, bubble, pet);
            effect.Show();
            effect.TopMostAgain();
            pet.BringToTop();
        }

        public static void AddPuddle(Point target, float scale)
        {
            while (Puddles.Count > 0 && Puddles[0].IsDisposed) Puddles.RemoveAt(0);
            var p = new PuddleForm(target, scale);
            p.Show();
            Puddles.Add(p);
            while (Puddles.Count > 6)
            {
                var old = Puddles[0];
                Puddles.RemoveAt(0);
                try { old.Close(); } catch { }
            }
        }

        static Bitmap CaptureBubble(Point target, float scale)
        {
            int s = Math.Max(48, (int)(70 * scale));
            var src = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(src))
                g.CopyFromScreen(target.X - s / 2, target.Y - s / 2, 0, 0, new Size(s, s));

            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var path = Rounded(new RectangleF(2f, 2f, s - 4f, s - 4f), s * 0.18f);
                g.SetClip(path);
                g.DrawImage(src, 0, 0);
                g.ResetClip();
                using (var tint = new SolidBrush(Color.FromArgb(34, 70, 180, 230))) g.FillPath(tint, path);
                using (var pen = new Pen(Color.FromArgb(210, 205, 245, 255), Math.Max(1.6f, 2f * scale))) g.DrawPath(pen, path);
                using (var b = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
                    g.FillEllipse(b, s * 0.16f, s * 0.12f, s * 0.26f, s * 0.09f);
                path.Dispose();
            }
            src.Dispose();
            return bmp;
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Max(2f, rad * 2f);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>全屏变暗 + 泡泡准星。</summary>
    public class CannonTargetForm : Form
    {
        readonly PetWindow pet;
        readonly Action<Point?> done;
        ReticleForm reticle;
        IntPtr prevForeground;
        bool finished;
        long lastAim;

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

        public CannonTargetForm(PetWindow pet, Action<Point?> done)
        {
            this.pet = pet;
            this.done = done;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            BackColor = Color.Black;
            Opacity = 0.27;
            TopMost = true;
            KeyPreview = true;
            Cursor = Cursors.Default;
            DoubleBuffered = true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            prevForeground = GetForegroundWindow();
            reticle = new ReticleForm(pet.WindowScale);
            reticle.MoveTo(Cursor.Position);
            reticle.Show();
            reticle.TopMostAgain();
            Activate();
            Focus();
            Cursor.Hide();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = Cursor.Position;
            if (reticle != null) reticle.MoveTo(p);
            long now = Environment.TickCount;
            if (now - lastAim > 45)
            {
                lastAim = now;
                pet.AimAt(p);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Point p = Cursor.Position;
                if (pet != null && pet.Bounds.Contains(p)) Finish(null);
                else Finish(p);
            }
            else if (e.Button == MouseButtons.Right) Finish(null);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Finish(null);
        }

        void Finish(Point? p)
        {
            if (finished) return;
            finished = true;
            Cursor.Show();
            if (reticle != null)
            {
                reticle.Close();
                reticle = null;
            }
            Hide();
            Close();
            try
            {
                if (prevForeground != IntPtr.Zero) SetForegroundWindow(prevForeground);
            }
            catch { }
            if (done != null) done(p);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Cursor.Show();
            base.OnFormClosed(e);
            if (!finished)
            {
                finished = true;
                if (done != null) done(null);
            }
        }
    }

    /// <summary>跟着鼠标的泡泡准星。</summary>
    public class ReticleForm : Form
    {
        readonly float scale;
        float angle;
        Timer timer;
        Bitmap frame;

        public ReticleForm(float scale)
        {
            this.scale = Math.Max(1f, scale);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Width = (int)(220 * scale);
            Height = (int)(150 * scale);
            timer = new Timer();
            timer.Interval = 40;
            timer.Tick += delegate
            {
                angle += 6f;
                Present();
            };
            timer.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void MoveTo(Point cursor)
        {
            Bounds = new Rectangle(cursor.X - (int)(110 * scale), cursor.Y - (int)(70 * scale), Width, Height);
        }

        public void TopMostAgain()
        {
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        void Present()
        {
            if (!Visible) return;
            if (frame == null || frame.Width != Width || frame.Height != Height)
            {
                if (frame != null) frame.Dispose();
                frame = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                frame.SetResolution(96, 96);
            }
            using (var g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.ScaleTransform(scale, scale);

                float cx = 110f, cy = 70f;
                using (var b = new SolidBrush(Color.FromArgb(26, 120, 220, 255)))
                    g.FillEllipse(b, cx - 34f, cy - 34f, 68f, 68f);
                using (var pen = new Pen(Color.FromArgb(220, 190, 245, 255), 1.6f))
                    g.DrawEllipse(pen, cx - 30f, cy - 30f, 60f, 60f);
                using (var pen = new Pen(Color.FromArgb(150, 140, 220, 255), 1.2f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(angle);
                    g.DrawEllipse(pen, -38f, -38f, 76f, 76f);
                    g.ResetTransform();
                    g.ScaleTransform(scale, scale);
                }
                using (var pen = new Pen(Color.FromArgb(220, 200, 248, 255), 1.4f))
                {
                    g.DrawLine(pen, cx - 40f, cy, cx - 14f, cy);
                    g.DrawLine(pen, cx + 14f, cy, cx + 40f, cy);
                    g.DrawLine(pen, cx, cy - 40f, cx, cy - 14f);
                    g.DrawLine(pen, cx, cy + 14f, cx, cy + 40f);
                }
                using (var b = new SolidBrush(Color.FromArgb(235, 225, 252, 255)))
                    g.FillEllipse(b, cx - 3f, cy - 3f, 6f, 6f);

                var label = new RectangleF(20f, 112f, 180f, 24f);
                using (var b = new SolidBrush(Color.FromArgb(205, 8, 18, 32))) g.FillPath(b, Rounded(label, 12f));
                using (var p = new Pen(Color.FromArgb(90, 130, 210, 245), 1f)) g.DrawPath(p, Rounded(label, 12f));
                using (var f = new Font("Microsoft YaHei UI", 8f))
                using (var b = new SolidBrush(Color.FromArgb(225, 230, 245, 255)))
                {
                    var sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("点一下发射 · 右键取消", f, b, label, sf);
                    sf.Dispose();
                }
            }
            Blit();
        }

        void Blit()
        {
            if (!Visible || frame == null) return;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                hBmp = frame.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBmp);
                var dst = new Native.POINT(Left, Top);
                var src = new Native.POINT(0, 0);
                var size = new Native.SIZE(Width, Height);
                var blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, (int)Native.ULW_ALPHA);
            }
            finally
            {
                if (old != IntPtr.Zero) Native.SelectObject(memDc, old);
                if (hBmp != IntPtr.Zero) Native.DeleteObject(hBmp);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) timer.Dispose();
                if (frame != null) frame.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>水柱、水花和装着图标的水泡。</summary>
    public class CannonEffectForm : Form
    {
        readonly Point mouth;
        readonly Point target;
        readonly Bitmap bubble;
        readonly PetWindow pet;
        readonly float scale;
        readonly DateTime start = DateTime.UtcNow;
        readonly double impactAt = 0.42;
        readonly Timer timer;
        Bitmap frame;
        bool puddleDone;

        public CannonEffectForm(Point mouth, Point target, Bitmap bubble, PetWindow pet)
        {
            this.mouth = mouth;
            this.target = target;
            this.bubble = bubble;
            this.pet = pet;
            this.scale = Math.Max(1f, pet.WindowScale);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            int margin = (int)(150 * scale);
            int l = Math.Min(mouth.X, target.X) - margin;
            int t = Math.Min(mouth.Y, target.Y) - margin;
            int r = Math.Max(mouth.X, target.X) + margin;
            int b = Math.Max(mouth.Y, target.Y) + margin;
            Rectangle vs = SystemInformation.VirtualScreen;
            l = Math.Max(vs.Left, l);
            t = Math.Max(vs.Top, t);
            r = Math.Min(vs.Right, r);
            b = Math.Min(vs.Bottom, b);
            Bounds = new Rectangle(l, t, Math.Max(80, r - l), Math.Max(80, b - t));

            timer = new Timer();
            timer.Interval = 33;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        public void TopMostAgain()
        {
            if (IsHandleCreated)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        void Tick()
        {
            double e = (DateTime.UtcNow - start).TotalSeconds;
            if (e > 1.45)
            {
                Close();
                return;
            }
            if (e >= impactAt && !puddleDone)
            {
                puddleDone = true;
                WaterCannon.AddPuddle(target, scale);
                TopMostAgain();
                if (pet != null) pet.BringToTop();
            }
            Draw(e);
        }

        void Draw(double e)
        {
            if (frame == null || frame.Width != Width || frame.Height != Height)
            {
                if (frame != null) frame.Dispose();
                frame = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                frame.SetResolution(96, 96);
            }
            float fade = 1f;
            using (var g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.ScaleTransform(scale, scale);

                PointF m = new PointF((mouth.X - Left) / scale, (mouth.Y - Top) / scale);
                PointF tg = new PointF((target.X - Left) / scale, (target.Y - Top) / scale);

                if (e < 0.30)
                {
                    float q = (float)(e / 0.30);
                    using (var b = new SolidBrush(Color.FromArgb((int)(70 * (1 - q)), 170, 235, 255)))
                        g.FillEllipse(b, m.X - 12f - 10f * q, m.Y - 12f - 10f * q, 24f + 20f * q, 24f + 20f * q);
                    using (var p = new Pen(Color.FromArgb((int)(180 * (1 - q)), 210, 245, 255), 1.2f))
                        g.DrawEllipse(p, m.X - 16f - 14f * q, m.Y - 16f - 14f * q, 32f + 28f * q, 32f + 28f * q);
                }

                if (e >= 0.18)
                {
                    float jp = (float)Math.Max(0, Math.Min(1, (e - 0.18) / 0.24));
                    DrawJet(g, m, tg, jp, e);
                }
                if (e >= impactAt)
                    DrawSplash(g, tg, (float)((e - impactAt) / 0.55));
                if (e >= impactAt)
                    DrawBubble(g, m, tg, (float)(e - impactAt));

                fade = e < 1.15 ? 1f : (float)Math.Max(0, 1 - (e - 1.15) / 0.30);
            }
            Blit(fade);
        }

        void DrawJet(Graphics g, PointF m, PointF t, float jp, double e)
        {
            float dx = t.X - m.X, dy = t.Y - m.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1f || jp < 0.04f) return;
            float arc = Math.Min(90f, len * 0.14f);
            PointF p1 = new PointF(m.X + dx * 0.28f, m.Y + dy * 0.28f - arc * 0.5f);
            PointF p2 = new PointF(m.X + dx * 0.72f, m.Y + dy * 0.72f - arc * 0.5f);

            int n = 44;
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                float u = (i / (float)(n - 1)) * jp;
                PointF p = Bez(m, p1, p2, t, u);
                PointF q = Bez(m, p1, p2, t, Math.Min(1f, u + 0.01f));
                float tx = q.X - p.X, ty = q.Y - p.Y;
                float tl = (float)Math.Sqrt(tx * tx + ty * ty);
                if (tl < 0.001f) { tx = 1; ty = 0; tl = 1; }
                float wob = (float)Math.Sin(u * 12f + e * 14f) * 1.1f * (1f - u);
                pts[i] = new PointF(p.X - ty / tl * wob, p.Y + tx / tl * wob);
            }

            using (var glow = new Pen(Color.FromArgb(45, 120, 210, 250), 22f))
            {
                glow.StartCap = LineCap.Round;
                glow.EndCap = LineCap.Round;
                glow.LineJoin = LineJoin.Round;
                g.DrawLines(glow, pts);
            }
            using (var pen = new Pen(Color.FromArgb(205, 120, 215, 250), 13f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
            using (var core = new Pen(Color.FromArgb(245, 245, 253, 255), 5.5f))
            {
                core.StartCap = LineCap.Round;
                core.EndCap = LineCap.Round;
                core.LineJoin = LineJoin.Round;
                g.DrawLines(core, pts);
            }

            for (int i = 0; i < 14; i++)
            {
                float u = (i / 14f) * jp;
                PointF p = Bez(m, p1, p2, t, u);
                float off = (float)Math.Sin(i * 2.7 + e * 18) * (8f + i % 5);
                float rr = 1.8f + (i % 3);
                using (var b = new SolidBrush(Color.FromArgb(190, 210, 245, 255)))
                    g.FillEllipse(b, p.X + off, p.Y - off * 0.7f, rr * 2, rr * 2);
            }

            if (jp < 1f)
            {
                PointF head = Bez(m, p1, p2, t, jp);
                using (var b = new SolidBrush(Color.FromArgb(235, 235, 252, 255)))
                    g.FillEllipse(b, head.X - 9f, head.Y - 9f, 18f, 18f);
            }
        }

        void DrawSplash(Graphics g, PointF t, float q)
        {
            if (q < 0) q = 0;
            float r = 8f + 46f * (float)Math.Sqrt(Math.Min(1f, q));
            float a = Math.Max(0f, 1f - q);
            using (var p = new Pen(Color.FromArgb((int)(190 * a), 190, 240, 255), 2f))
                g.DrawEllipse(p, t.X - r, t.Y - r * 0.55f, r * 2f, r * 1.1f);
            using (var p = new Pen(Color.FromArgb((int)(90 * a), 150, 220, 250), 1f))
                g.DrawEllipse(p, t.X - r * 1.6f, t.Y - r * 0.9f, r * 3.2f, r * 1.8f);
            for (int i = 0; i < 12; i++)
            {
                double ang = i * Math.PI * 2 / 12 + 0.4;
                float rr = r * (0.6f + (i % 3) * 0.2f);
                float x = t.X + (float)Math.Cos(ang) * rr;
                float y = t.Y + (float)Math.Sin(ang) * rr * 0.45f - (float)(Math.Sin(Math.Min(1, q) * Math.PI) * 20f);
                using (var b = new SolidBrush(Color.FromArgb((int)(200 * a), 225, 250, 255)))
                    g.FillEllipse(b, x - 2f, y - 2f, 4f, 4f);
            }
            using (var b = new SolidBrush(Color.FromArgb((int)(120 * a), 255, 255, 255)))
                g.FillEllipse(b, t.X - 10f, t.Y - 6f, 20f, 12f);
        }

        void DrawBubble(Graphics g, PointF m, PointF t, float bt)
        {
            if (bt < 0 || bubble == null) return;
            float dx = t.X - m.X, dy = t.Y - m.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1f) len = 1f;
            float vx = dx / len * 300f, vy = dy / len * 300f - 260f;
            float x = t.X + vx * bt;
            float y = t.Y + vy * bt + 0.5f * 620f * bt * bt;
            float ang = 220f * bt;
            float alpha = 1f - Smooth(0.55f, 0.95f, bt);
            if (alpha <= 0.01f) return;

            float w = bubble.Width / scale;
            float h = bubble.Height / scale;
            var cm = new ColorMatrix();
            cm.Matrix33 = alpha;
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                var state = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(ang);
                g.DrawImage(bubble, new Rectangle((int)(-w / 2), (int)(-h / 2), (int)w, (int)h),
                    0, 0, bubble.Width, bubble.Height, GraphicsUnit.Pixel, ia);
                g.Restore(state);
            }

            for (int i = 0; i < 6; i++)
            {
                float dt = Math.Max(0, bt - 0.05f * (i + 1));
                float px = t.X + vx * dt;
                float py = t.Y + vy * dt + 0.5f * 620f * dt * dt;
                using (var b = new SolidBrush(Color.FromArgb((int)(120 * alpha), 200, 240, 255)))
                    g.FillEllipse(b, px - 2f, py - 2f, 4f, 4f);
            }
        }

        static float Smooth(float a, float b, float x)
        {
            if (x <= a) return 0f;
            if (x >= b) return 1f;
            float t = (x - a) / (b - a);
            return t * t * (3f - 2f * t);
        }

        static PointF Bez(PointF p0, PointF p1, PointF p2, PointF p3, float t)
        {
            float u = 1f - t;
            float a = u * u * u, b = 3f * u * u * t, c = 3f * u * t * t, d = t * t * t;
            return new PointF(a * p0.X + b * p1.X + c * p2.X + d * p3.X,
                              a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
        }

        void Blit(float opacity)
        {
            if (!Visible || frame == null) return;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                hBmp = frame.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBmp);
                var dst = new Native.POINT(Left, Top);
                var src = new Native.POINT(0, 0);
                var size = new Native.SIZE(Width, Height);
                var blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.SourceConstantAlpha = (byte)Math.Max(0, Math.Min(255, (int)(255 * opacity)));
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, (int)Native.ULW_ALPHA);
            }
            finally
            {
                if (old != IntPtr.Zero) Native.SelectObject(memDc, old);
                if (hBmp != IntPtr.Zero) Native.DeleteObject(hBmp);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) timer.Dispose();
                if (frame != null) frame.Dispose();
                if (bubble != null) bubble.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>冲走图标后留在原地的可爱浅蓝水洼，5 秒后干掉。</summary>
    public class PuddleForm : Form
    {
        readonly DateTime start = DateTime.UtcNow;
        readonly float scale;
        readonly float wDip = 170f;
        readonly float hDip = 140f;
        readonly Timer timer;
        Bitmap frame;

        public PuddleForm(Point target, float scale)
        {
            this.scale = Math.Max(1f, scale);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Width = (int)Math.Round(wDip * this.scale);
            Height = (int)Math.Round(hDip * this.scale);
            Left = target.X - Width / 2;
            Top = target.Y - (int)Math.Round(60 * this.scale);
            timer = new Timer();
            timer.Interval = 40;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        void Tick()
        {
            double t = (DateTime.UtcNow - start).TotalSeconds;
            if (t > 5.0)
            {
                Close();
                return;
            }
            float alpha = 1f;
            if (t < 0.16) alpha = (float)(t / 0.16);
            else if (t > 4.0) alpha = (float)Math.Max(0, (5.0 - t) / 1.0);
            float grow = t < 0.16 ? (float)(0.86 + 0.14 * (t / 0.16)) : 1f;
            if (t > 4.0) grow = 1f - (float)((t - 4.0) / 1.0) * 0.08f;
            Draw(alpha, grow);
        }

        void Draw(float alpha, float grow)
        {
            if (frame == null || frame.Width != Width || frame.Height != Height)
            {
                if (frame != null) frame.Dispose();
                frame = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                frame.SetResolution(96, 96);
            }
            using (var g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.ScaleTransform(scale, scale);
                g.TranslateTransform(wDip / 2f, hDip / 2f);
                g.ScaleTransform(grow, grow);
                g.TranslateTransform(-wDip / 2f, -hDip / 2f);

                var outer = PuddlePath(wDip, hDip);

                var st = g.Save();
                g.TranslateTransform(0f, 4f);
                using (var sb = new SolidBrush(Color.FromArgb((int)(24 * alpha), 36, 84, 116)))
                    g.FillPath(sb, outer);
                g.Restore(st);

                using (var b = new LinearGradientBrush(new PointF(0f, 18f), new PointF(0f, 108f),
                    Color.FromArgb((int)(252 * alpha), 192, 237, 255),
                    Color.FromArgb((int)(252 * alpha), 124, 201, 238)))
                    g.FillPath(b, outer);

                var it = g.Save();
                g.TranslateTransform(wDip / 2f, hDip / 2f - 3f);
                g.ScaleTransform(0.78f, 0.72f);
                g.TranslateTransform(-wDip / 2f, -hDip / 2f);
                using (var b = new SolidBrush(Color.FromArgb((int)(190 * alpha), 219, 248, 255)))
                    g.FillPath(b, outer);
                g.Restore(it);

                using (var p = new Pen(Color.FromArgb((int)(240 * alpha), 62, 140, 185), 2.2f))
                    g.DrawPath(p, outer);

                using (var p = new Pen(Color.FromArgb((int)(225 * alpha), 255, 255, 255), 2f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, 30f, 42f, 48f, 20f, 200f, 105f);
                    g.DrawArc(p, 92f, 62f, 24f, 10f, 205f, 85f);
                }
                using (var b = new SolidBrush(Color.FromArgb((int)(235 * alpha), 255, 255, 255)))
                    g.FillEllipse(b, 36f, 36f, 7f, 3f);

                DrawDrop(g, 16f, 98f, 16f, 8f, alpha);
                DrawDrop(g, 116f, 102f, 13f, 7f, alpha);
                DrawDrop(g, 58f, 108f, 8f, 4f, alpha);

                outer.Dispose();
            }
            Blit(alpha);
        }

        static void DrawDrop(Graphics g, float x, float y, float w, float h, float alpha)
        {
            using (var b = new SolidBrush(Color.FromArgb((int)(245 * alpha), 180, 230, 252)))
                g.FillEllipse(b, x, y, w, h);
            using (var p = new Pen(Color.FromArgb((int)(225 * alpha), 70, 150, 195), 1.4f))
                g.DrawEllipse(p, x, y, w, h);
        }

        static GraphicsPath PuddlePath(float w, float h)
        {
            var pts = new PointF[]
            {
                new PointF(16f, 62f), new PointF(24f, 36f), new PointF(50f, 22f),
                new PointF(82f, 18f), new PointF(112f, 28f), new PointF(130f, 46f),
                new PointF(134f, 68f), new PointF(124f, 88f), new PointF(102f, 102f),
                new PointF(74f, 110f), new PointF(46f, 108f), new PointF(24f, 96f),
                new PointF(8f, 80f)
            };
            float sx = w / 140f, sy = h / 120f;
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new PointF(pts[i].X * sx, pts[i].Y * sy);
            var p = new GraphicsPath(FillMode.Winding);
            p.AddClosedCurve(pts, 0.55f);
            return p;
        }

        void Blit(float opacity)
        {
            if (!Visible || frame == null) return;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                hBmp = frame.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBmp);
                var dst = new Native.POINT(Left, Top);
                var src = new Native.POINT(0, 0);
                var size = new Native.SIZE(Width, Height);
                var blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.SourceConstantAlpha = (byte)Math.Max(0, Math.Min(255, (int)(255 * opacity)));
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, (int)Native.ULW_ALPHA);
            }
            finally
            {
                if (old != IntPtr.Zero) Native.SelectObject(memDc, old);
                if (hBmp != IntPtr.Zero) Native.DeleteObject(hBmp);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) timer.Dispose();
                if (frame != null) frame.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
