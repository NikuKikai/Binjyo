using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Binjyo
{
    public partial class EditModePanel : Window
    {
        public EditModePanel()
        {
            InitializeComponent();
        }

        public void UpdateToolName(string toolName)
        {
            ToolNameText.Text = $"Tool: {toolName}";
        }

        public void UpdateBrushSize(double brushSize)
        {
            BrushSizeText.Text = $"Size: {Math.Round(brushSize):0}";
        }

        public void UpdatePlacement(double memoLeft, double memoTop, double memoWidth, double dpiFactor)
        {
            double margin = 0;
            int centerX = (int)Math.Round((memoLeft + memoWidth / 2) * dpiFactor);
            int centerY = (int)Math.Round(memoTop * dpiFactor);
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point(centerX, centerY));
            double screenLeft = screen.Bounds.Left / dpiFactor;
            double screenRight = screen.Bounds.Right / dpiFactor;
            double desiredLeft = memoLeft + memoWidth + margin;

            if (desiredLeft + Width > screenRight)
                desiredLeft = memoLeft - Width - margin;

            if (desiredLeft < screenLeft)
                desiredLeft = screenLeft;

            Left = desiredLeft;
            Top = memoTop;
        }
    }
}
