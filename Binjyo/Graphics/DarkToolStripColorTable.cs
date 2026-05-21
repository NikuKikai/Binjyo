using System.Drawing;
using System.Windows.Forms;

namespace Binjyo
{
    internal sealed class DarkToolStripColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(22, 22, 22);
        public override Color MenuBorder => Color.FromArgb(50, 50, 50);
        public override Color MenuItemBorder => Color.FromArgb(50, 50, 50);
        public override Color MenuItemSelected => Color.FromArgb(43, 43, 43);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(31, 94, 59);
        public override Color MenuItemPressedGradientMiddle => Color.FromArgb(31, 94, 59);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(31, 94, 59);
        public override Color ImageMarginGradientBegin => Color.FromArgb(22, 22, 22);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(22, 22, 22);
        public override Color ImageMarginGradientEnd => Color.FromArgb(22, 22, 22);
        public override Color SeparatorDark => Color.FromArgb(58, 58, 58);
        public override Color SeparatorLight => Color.FromArgb(58, 58, 58);
        public override Color CheckBackground => Color.FromArgb(31, 94, 59);
        public override Color CheckSelectedBackground => Color.FromArgb(31, 94, 59);
        public override Color CheckPressedBackground => Color.FromArgb(31, 94, 59);
    }
}
