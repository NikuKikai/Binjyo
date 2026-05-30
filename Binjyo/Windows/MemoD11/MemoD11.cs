using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;


namespace Binjyo
{
    public partial class MemoD11 : Form, ISceneItemView
    {
        #region ======== Types ========

        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int LWA_ALPHA = 0x00000002;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int WM_SIZE = 0x0005;
        private const int WM_SIZING = 0x0214;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        #endregion

        #region ======== State ========

        private readonly SceneItem Item;
        private readonly bool IsRendererField = false;
        private bool lastMouseInside = false;
        private Timer timer = new Timer();

        private double FinalOpacity
        {
            get
            {
                if (Scene.DisplayMode == EDisplayMode.AutoHide
                    && (EAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior == EAutoHideBehavior.HideOnHover
                    && IsMouseInside())
                    return 0;
                return Item.IsOpacity ? Item.Opacity : 1;
            }
        }

        #endregion

        #region ======== Lifecycle ========

        /// <summary>
        /// Create a D3D11-backed memo window for a scene item.
        /// </summary>
        public MemoD11(SceneItem item)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = false;
            BackColor = System.Drawing.Color.Black;
            TopMost = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.UserPaint, true);

            MouseDown += MemoD11_MouseDown;
            MouseMove += MemoD11_MouseMove;
            MouseUp += MemoD11_MouseUp;
            MouseWheel += MemoD11_MouseWheel;
            KeyDown += MemoD11_KeyDown;
            KeyUp += MemoD11_KeyUp;
            FormClosed += MemoD11_FormClosed;
            MouseDoubleClick += MemoD11_MouseDoubleClick;
            // Application.Idle += OnApplicationIdle;

            item.RegisterView(this);
            Show();
            NotifiedTransform(false);
            NotifiedDisplayMode();

            timer.Interval = 16;
            timer.Tick += (s, e) => PerFrameUpdate();
            timer.Start();
        }

        /// <summary>
        /// Create the native graphics resources after the window handle exists.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitializeGraphics();
            UploadSourceBitmap();
            RenderSceneItem();
        }

        /// <summary>
        /// Suppress WinForms background clears because DirectComposition owns the surface.
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        /// <summary>
        /// Suppress GDI painting because all visuals come from D3D11.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
        }

        /// <summary>
        /// Return transparent hit-test results for fully transparent pixels so input can pass through irregular image shapes.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                long lParam = m.LParam.ToInt64();
                int screenX = unchecked((short)(lParam & 0xFFFF));
                int screenY = unchecked((short)((lParam >> 16) & 0xFFFF));
                // if (Item.InCollider(screenX, screenY)) // Optimization to avoid expensive pixel hit test when fully transparent
                {
                    m.Result = (IntPtr)HTTRANSPARENT;
                    return;
                }
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// Enable DirectComposition-friendly window styles so transparent regions are not backed by a black redirection surface.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOREDIRECTIONBITMAP;
                // cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return cp;
            }
        }

        /// <summary>
        /// Rebuild the swap chain from the actual native client size after the window resize completes.
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (!isGraphicsReady || WindowState == FormWindowState.Minimized)
                return;

            int width = Math.Max(1, ClientSize.Width);
            int height = Math.Max(1, ClientSize.Height);
            if (width == renderWidth && height == renderHeight)
                return;

            ResizeSwapChain(width, height);
            RenderSceneItem();
        }

        /// <summary>
        /// Tear down interaction and graphics resources when the form closes.
        /// </summary>
        private void MemoD11_FormClosed(object sender, FormClosedEventArgs e)
        {
            Animator.Clear(Id);
            timer?.Stop();
            timer?.Dispose();
            timer = null;
            DisposeGraphics();
            if (Item.views.Contains(this))
                Item.UnregisterView(this);
        }

        private void PerFrameUpdate()
        {
            // DisplayMode: AutoHide
            if (Scene.DisplayMode == EDisplayMode.AutoHide)
            {
                if ((EAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior == EAutoHideBehavior.HideOnHover)
                {
                    var isMouseInside = IsMouseInside();
                    if (isMouseInside != lastMouseInside)
                    {
                        Opacity = FinalOpacity;
                        lastMouseInside = isMouseInside;
                    }
                }
                else
                {
                }
            }
        }

        #endregion

    }
}
