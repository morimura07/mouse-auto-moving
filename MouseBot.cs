// MouseBot - physically moves the mouse cursor at a regular interval to
// prevent Windows from going to sleep / locking / logging out while idle.
//
// It uses the Win32 SendInput API with MOUSEEVENTF_MOVE, which injects a real
// mouse-input event into the OS input queue. Windows treats this exactly like
// movement from a physical mouse: the cursor actually moves on screen and the
// system idle timer is reset. This is NOT a hidden/background trick - it is the
// same input path a real mouse uses.
//
// Each cycle the cursor GLIDES to a new RANDOM position (it does not return to
// where it started) using an ease-in / ease-out curve, so the motion looks
// exactly like a person dragging the mouse around the screen.
//
// Build:  see build.bat  (compiles to MouseBot.exe, no dependencies to install)

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MouseBot
{
    public class MainForm : Form
    {
        // ---- Win32 interop ------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;   // we only ever send mouse input
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private const uint INPUT_MOUSE      = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;   // relative movement

        // ---- UI controls --------------------------------------------------

        private readonly Button        btnToggle   = new Button();
        private readonly NumericUpDown numInterval = new NumericUpDown();
        private readonly NumericUpDown numDistance = new NumericUpDown();
        private readonly NumericUpDown numDuration = new NumericUpDown();
        private readonly Label         lblStatus   = new Label();
        private readonly Label         lblCount    = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon    tray        = new NotifyIcon();
        private readonly Random        rng         = new Random();

        private bool running   = false;
        private long moveCount = 0;
        private volatile bool animating = false;

        public MainForm()
        {
            // --- Form ------------------------------------------------------
            Text            = "Mouse Mover Bot";
            ClientSize      = new Size(320, 250);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterScreen;

            // --- Interval (seconds) ---------------------------------------
            var lblInterval = new Label { Text = "Move every (seconds):", Left = 15, Top = 18, Width = 170 };
            numInterval.Left = 195; numInterval.Top = 15; numInterval.Width = 100;
            numInterval.Minimum = 1; numInterval.Maximum = 600; numInterval.Value = 30;

            // --- Max step distance (pixels) -------------------------------
            var lblDistance = new Label { Text = "Max step distance (px):", Left = 15, Top = 52, Width = 170 };
            numDistance.Left = 195; numDistance.Top = 49; numDistance.Width = 100;
            numDistance.Minimum = 20; numDistance.Maximum = 1200; numDistance.Value = 250;

            // --- Drag duration (how slow) ---------------------------------
            var lblDuration = new Label { Text = "Drag duration (ms):", Left = 15, Top = 86, Width = 170 };
            numDuration.Left = 195; numDuration.Top = 83; numDuration.Width = 100;
            numDuration.Minimum = 100; numDuration.Maximum = 5000; numDuration.Increment = 100; numDuration.Value = 1000;

            // --- Toggle button --------------------------------------------
            btnToggle.Text = "Start";
            btnToggle.Left = 17; btnToggle.Top = 120; btnToggle.Width = 286; btnToggle.Height = 42;
            btnToggle.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
            btnToggle.Click += (s, e) => Toggle();

            // --- Status ---------------------------------------------------
            lblStatus.Left = 17; lblStatus.Top = 172; lblStatus.Width = 286;
            lblStatus.Text = "Status: Stopped";
            lblStatus.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);
            lblStatus.ForeColor = Color.Firebrick;

            lblCount.Left = 17; lblCount.Top = 198; lblCount.Width = 286;
            lblCount.Text = "Moves performed: 0";

            Controls.AddRange(new Control[] {
                lblInterval, numInterval, lblDistance, numDistance,
                lblDuration, numDuration, btnToggle, lblStatus, lblCount
            });

            // --- Timer ----------------------------------------------------
            timer.Tick += (s, e) => DoMove();

            // --- Tray icon (minimize to tray) -----------------------------
            tray.Icon = SystemIcons.Application;
            tray.Text = "Mouse Mover Bot";
            tray.Visible = true;
            tray.DoubleClick += (s, e) => RestoreFromTray();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Show / Hide", null, (s, e) => RestoreFromTray());
            menu.Items.Add("Start / Stop", null, (s, e) => Toggle());
            menu.Items.Add("Exit", null, (s, e) => { tray.Visible = false; Application.Exit(); });
            tray.ContextMenuStrip = menu;

            Resize    += (s, e) => { if (WindowState == FormWindowState.Minimized) Hide(); };
            FormClosing += (s, e) => { tray.Visible = false; };
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void Toggle()
        {
            if (running) Stop(); else Start();
        }

        private void Start()
        {
            timer.Interval = (int)numInterval.Value * 1000;
            timer.Start();
            running = true;
            btnToggle.Text = "Stop";
            lblStatus.Text = "Status: Running";
            lblStatus.ForeColor = Color.ForestGreen;
            numInterval.Enabled = false;
            tray.Text = "Mouse Mover Bot - Running";
            DoMove(); // move immediately so the user sees it works
        }

        private void Stop()
        {
            timer.Stop();
            running = false;
            btnToggle.Text = "Start";
            lblStatus.Text = "Status: Stopped";
            lblStatus.ForeColor = Color.Firebrick;
            numInterval.Enabled = true;
            tray.Text = "Mouse Mover Bot - Stopped";
        }

        // Kick off one smooth drag to a new random spot. Runs on a background
        // thread so the UI and the timer stay responsive while gliding.
        private void DoMove()
        {
            if (animating) return;   // don't overlap drags

            int maxStep  = (int)numDistance.Value;
            int duration = (int)numDuration.Value;

            // Where is the cursor now, and which screen is it on?
            POINT cur;
            if (!GetCursorPos(out cur)) return;
            Rectangle screen = Screen.FromPoint(new Point(cur.X, cur.Y)).Bounds;

            // Pick a random target: random direction, random magnitude up to
            // maxStep, then clamp so it stays a little inside the screen edges.
            const int margin = 5;
            int targetX, targetY;
            double angle = rng.NextDouble() * 2 * Math.PI;
            double mag   = maxStep * (0.4 + 0.6 * rng.NextDouble()); // 40%..100%

            targetX = cur.X + (int)Math.Round(Math.Cos(angle) * mag);
            targetY = cur.Y + (int)Math.Round(Math.Sin(angle) * mag);

            targetX = Math.Max(screen.Left + margin,  Math.Min(screen.Right  - margin, targetX));
            targetY = Math.Max(screen.Top  + margin,  Math.Min(screen.Bottom - margin, targetY));

            int dx = targetX - cur.X;
            int dy = targetY - cur.Y;
            if (dx == 0 && dy == 0) return;

            animating = true;
            var t = new Thread(() =>
            {
                try
                {
                    GlideRelative(dx, dy, duration);
                }
                finally
                {
                    animating = false;
                }

                moveCount++;
                try { BeginInvoke((Action)(() => lblCount.Text = "Moves performed: " + moveCount)); }
                catch { /* form closing */ }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Move the cursor by (totalDx, totalDy) over 'durationMs' using an
        // ease-in / ease-out curve, sent as many tiny real mouse-move events.
        private static void GlideRelative(int totalDx, int totalDy, int durationMs)
        {
            const int stepMs = 15;                       // ~66 updates per second
            int steps = Math.Max(1, durationMs / stepMs);

            int sentX = 0, sentY = 0;                    // pixels already emitted
            for (int i = 1; i <= steps; i++)
            {
                double t    = (double)i / steps;
                double ease = EaseInOut(t);              // 0..1, slow-fast-slow

                int targetX = (int)Math.Round(totalDx * ease);
                int targetY = (int)Math.Round(totalDy * ease);

                int dx = targetX - sentX;
                int dy = targetY - sentY;
                sentX = targetX;
                sentY = targetY;

                if (dx != 0 || dy != 0)
                    MoveRelative(dx, dy);

                Thread.Sleep(stepMs);
            }
        }

        // Cubic ease-in-out: smooth acceleration then deceleration, like a hand.
        private static double EaseInOut(double t)
        {
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        private static void MoveRelative(int dx, int dy)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
