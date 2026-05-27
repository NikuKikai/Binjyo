using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Binjyo
{
    public enum DrawTool
    {
        Brush,
        Eraser
    }

    public partial class DrawPanel : Window
    {
        public DrawTool Tool { get; private set; } = DrawTool.Brush;
        public double BrushSize { get; private set; } = 5;
        public string ToolName => Tool == DrawTool.Brush ? "Brush" : "Eraser";

        public DrawPanel()
        {
            InitializeComponent();
        }

        public void SetTool(DrawTool tool)
        {
            Tool = tool;
            ToolNameText.Text = $"Tool: {ToolName}";
        }

        public void SetBrushSize(double brushSize)
        {
            brushSize = Math.Min(Math.Max(1, brushSize), 99);
            BrushSize = brushSize;
            BrushSizeText.Text = $"Size: {Math.Round(brushSize):0}";
        }

        public void UpdatePlacement(double memoLeft, double memoTop, double memoWidth, double dpiFactor)
        {
            const double margin = 0;

            int anchorX = (int)Math.Round((memoLeft + memoWidth) * dpiFactor);
            int anchorY = (int)Math.Round(memoTop * dpiFactor);
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point(anchorX, anchorY));

            double screenRight = screen.Bounds.Right / dpiFactor;
            double desiredLeft = memoLeft + memoWidth + margin;

            if (desiredLeft + Width > screenRight)
                desiredLeft = memoLeft - Width - margin;

            Left = desiredLeft;
            Top = memoTop;
        }
    }
}
