using System;
using System.Diagnostics;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;


namespace Binjyo
{
    public partial class MemoD11 : Form, ISceneItemView
    {

        #region ======== State ========

        private readonly SceneItem Item;
        private readonly bool IsRendererField = false;
        private bool lastMouseInside = false;
        private Timer timer = new Timer();
        private readonly Stopwatch frameClock = new Stopwatch();
        private long lastFrameTicks = 0;
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
            AllowTransparency = false;
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
            UpdateRenderHostLayout();
            Bounds = currentHostBounds;
            Show();

            NotifiedTransform(false);
            NotifiedDisplayMode();

            frameClock.Start();
            lastFrameTicks = frameClock.ElapsedTicks;
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
            RenderSceneItem();
        }

        /// <summary>
        /// Suppress WinForms background clears because the layered bitmap is fully owned by the D3D11 render path.
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
        /// Use a layered tool window so the system performs hit testing against the final per-pixel alpha content.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        /// <summary>
        /// Rebuild the offscreen render targets from the native client size after the window resize completes.
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

            ResetRenderTargets(width, height);
            RenderRequest();
        }

        /// <summary>
        /// Tear down interaction and graphics resources when the form closes.
        /// </summary>
        private void MemoD11_FormClosed(object sender, FormClosedEventArgs e)
        {
            drawPanel?.Close();
            drawPanel = null;
            memoMenuHostWindow?.Close();
            memoMenuHostWindow = null;
            timer?.Stop();
            timer?.Dispose();
            timer = null;
            hsvWheelWindow?.Close();
            hsvWheelWindow = null;
            DisposeGraphics();
            if (Item.views.Contains(this))
                Item.UnregisterView(this);
        }

        private void PerFrameUpdate()
        {
            long currentTicks = frameClock.ElapsedTicks;
            double deltaSeconds = (currentTicks - lastFrameTicks) / (double)Stopwatch.Frequency;
            lastFrameTicks = currentTicks;

            if (deltaSeconds > 0)
                UpdateFrameAnimations(deltaSeconds);

            // DisplayMode: AutoHide
            if (Scene.DisplayMode == EDisplayMode.AutoHide)
            {
                if ((EAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior == EAutoHideBehavior.HideOnHover)
                {
                    var isMouseInside = IsMouseInside();
                    if (isMouseInside != lastMouseInside)
                    {
                        RenderRequest();
                        lastMouseInside = isMouseInside;
                    }
                }
                else
                {
                    if (UpdateEvadeState())
                    {
                        UpdateRenderHostLayout();
                        UpdateDrawPanelPlacement();
                        RenderNowOrQueue();
                    }
                }
            }
            else if (ResetEvadeOffset())
            {
                UpdateRenderHostLayout();
                UpdateDrawPanelPlacement();
                RenderNowOrQueue();
            }

            if (!isRendering && isRenderRequested)
            {
                isRendering = true;
                isRenderRequested = false;
                RenderSceneItem();
                isRendering = false;
            }
        }

        /// <summary>
        /// Advance all memo-local animations from the shared frame timer.
        /// </summary>
        private void UpdateFrameAnimations(double deltaSeconds)
        {
            UpdateRotateAnimation(deltaSeconds);
            UpdateHighlightAnimation(deltaSeconds);
        }

        #endregion

    }

}
