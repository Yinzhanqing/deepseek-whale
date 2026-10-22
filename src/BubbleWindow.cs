using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    /// <summary>A comic speech bubble that floats above the pet and fades away.</summary>
    public class BubbleWindow : Form
    {
        static readonly FontFamily Family;
        static readonly Font TextFont;
        static readonly Color Fill = Color.FromArgb(255, 255, 248, 230);
        static readonly Color Ink = Color.FromArgb(255, 28, 36, 52);
        static readonly Color Border = Color.FromArgb(255, 32, 42, 62);

        const float Pad = 12f;
        const float MaxTextWidth = 200f;
        const float MinWidth = 88f;
        const float TailH = 10f;

        string text = "";
        bool tailDown = true;
        float scale = 1f;
        Bitmap frame;
        Timer tick;
        PetWindow owner;
        double startAt;
        double holdSeconds;
        float opacity;

        static BubbleWindow()
        {
            try { Family = new FontFamily("Microsoft YaHei UI"); }
            catch { Family = FontFamily.GenericSansSerif; }
            TextFont = new Font(Family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        }

        public BubbleWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(12, 18, 30);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            tick = new Timer();
            tick.Interval = 40;
            tick.Tick += delegate { OnTick(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void Say(string message, double seconds, PetWindow pet)
        {
            text = message ?? "";
            owner = pet;
            holdSeconds = Math.Max(1.5, seconds);
            if (!IsHandleCreated) CreateHandle();
            scale = scale <= 0 ? 1f : scale;
            try
            {
                using (var g = CreateGraphics()) scale = Math.Max(1f, g.DpiX / 96f);
            }
            catch { }

            float w, h;
            Measure(text, out w, out h);
            Width = (int)Math.Round(w * scale);
            Height = (int)Math.Round(h * scale);
            tailDown = true;
            RebuildFrame();
            Reposition();
            opacity = 0f;
            startAt = clockNow();
            if (!Visible) Show();
            TopMostAgain();
            Present();
            tick.Start();
        }

        public void HideBubble()
        {
            try { tick.Stop(); } catch { }
            if (Visible) Hide();
            opacity = 0f;
        }

        public void Follow()
        {
            if (Visible && owner != null) Reposition();
        }

        static double clockNow()
        {
            return Environment.TickCount / 1000.0;
        }

        void OnTick()
        {
            if (owner == null || !owner.Visible) { HideBubble(); return; }
            double e = clockNow() - startAt;
            double fadeIn = 0.16;
            double fadeOut = 0.30;
            double total = fadeIn + holdSeconds + fadeOut;
            if (e < fadeIn) opacity = (float)(e / fadeIn);
            else if (e < fadeIn + holdSeconds) opacity = 1f;
            else if (e < total) opacity = (float)(1.0 - (e - fadeIn - holdSeconds) / fadeOut);
            else { HideBubble(); return; }

            Reposition();
            TopMostAgain();
            Present();
        }

        void RebuildFrame()
        {
            if (Width <= 0 || Height <= 0) return;
            if (frame != null) frame.Dispose();
            frame = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            frame.SetResolution(96f, 96f);
            using (var g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.ScaleTransform(scale, scale);
                DrawBubble(g, Width / scale, Height / scale, text, tailDown);
            }
        }

        void Reposition()
        {
            if (owner == null) return;
            Rectangle pet = owner.Bounds;
            Rectangle wa = Screen.FromRectangle(pet).WorkingArea;
            int w = Width;
            int h = Height;
            int x = pet.Left + pet.Width / 2 - w / 2;
            int y = pet.Top - h + 2;
            bool down = true;
            if (y < wa.Top + 2)
            {
                y = pet.Bottom + 2;
                down = false;
            }
            x = Math.Max(wa.Left + 2, Math.Min(x, wa.Right - w - 2));
            y = Math.Max(wa.Top + 2, Math.Min(y, wa.Bottom - h - 2));
            if (down != tailDown)
            {
                tailDown = down;
                RebuildFrame();
            }
            if (Bounds.X != x || Bounds.Y != y || Bounds.Width != w || Bounds.Height != h)
                Bounds = new Rectangle(x, y, w, h);
        }

        void TopMostAgain()
        {
            if (!IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        void Present()
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

        public static void Measure(string text, out float w, out float h)
        {
            float tw;
            float th;
            using (var bmp = new Bitmap(8, 8))
            {
                bmp.SetResolution(96f, 96f);
                using (var g = Graphics.FromImage(bmp))
                {
                    var fmt = new StringFormat();
                    fmt.Trimming = StringTrimming.None;
                    var size = g.MeasureString(text, TextFont, (int)MaxTextWidth, fmt);
                    tw = Math.Min(MaxTextWidth, size.Width);
                    th = size.Height;
                }
            }
            w = Math.Max(MinWidth, tw + Pad * 2);
            h = th + Pad * 2 + TailH;
        }

        public static void DrawBubble(Graphics g, float w, float h, string text, bool tailDown)
        {
            float bodyTop = tailDown ? 1f : 1f + TailH;
            float bodyH = h - TailH - 2f;
            float bodyW = w - 2f;
            var body = Rounded(new RectangleF(1f, bodyTop, bodyW, bodyH), 11f);

            using (var shadow = new SolidBrush(Color.FromArgb(48, 0, 0, 0)))
            {
                var sp = Rounded(new RectangleF(2f, bodyTop + 2.5f, bodyW, bodyH), 11f);
                g.FillPath(shadow, sp);
                sp.Dispose();
            }

            float cx = w / 2f;
            var tail = new GraphicsPath();
            if (tailDown)
                tail.AddPolygon(new PointF[] { new PointF(cx - 9f, bodyTop + bodyH - 2f), new PointF(cx, h - 1f), new PointF(cx + 9f, bodyTop + bodyH - 2f) });
            else
                tail.AddPolygon(new PointF[] { new PointF(cx - 9f, bodyTop + 2f), new PointF(cx, 1f), new PointF(cx + 9f, bodyTop + 2f) });

            using (var fill = new SolidBrush(Fill))
            {
                g.FillPath(fill, tail);
                g.FillPath(fill, body);
            }
            using (var pen = new Pen(Border, 2f))
            {
                g.DrawPath(pen, body);
                if (tailDown)
                {
                    g.DrawLine(pen, cx - 9f, bodyTop + bodyH - 2f, cx, h - 1f);
                    g.DrawLine(pen, cx, h - 1f, cx + 9f, bodyTop + bodyH - 2f);
                }
                else
                {
                    g.DrawLine(pen, cx - 9f, bodyTop + 2f, cx, 1f);
                    g.DrawLine(pen, cx, 1f, cx + 9f, bodyTop + 2f);
                }
            }
            // 盖住气泡与尾巴交界处的横线
            using (var fill = new SolidBrush(Fill))
            {
                if (tailDown) g.FillRectangle(fill, cx - 10f, bodyTop + bodyH - 4f, 20f, 6f);
                else g.FillRectangle(fill, cx - 10f, bodyTop - 2f, 20f, 6f);
            }

            var fmt = new StringFormat();
            fmt.Trimming = StringTrimming.None;
            using (var ink = new SolidBrush(Ink))
                g.DrawString(text, TextFont, ink, new RectangleF(Pad, bodyTop + Pad - 2f, w - Pad * 2f, bodyH - Pad * 2f + 4f), fmt);
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
                if (tick != null) tick.Dispose();
                if (frame != null) frame.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
