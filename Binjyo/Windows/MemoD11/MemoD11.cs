using System;
using System.Diagnostics;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;


namespace Binjyo
{
    public partial class MemoD11 : Form, ISceneItemView
    {
        #region ======== Types ========

        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

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
        /// Enable DirectComposition-friendly window styles so transparent regions are not backed by a black redirection surface.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOREDIRECTIONBITMAP;
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
            stopwatch.Stop();
            stopwatch = null;
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
                    Console.WriteLine($"Mouse inside: {isMouseInside}");
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
