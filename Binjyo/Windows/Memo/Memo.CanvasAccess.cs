using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public partial class Memo
    {
        public Rect GetCanvasBounds()
        {
            return new Rect(anchorLeft, anchorTop, Width, Height);
        }

        public BitmapSource CreateDisplayBitmapSource()
        {
            return CreateOutputBitmapSource(true);
        }

        public Bitmap CreateDisplayBitmap()
        {
            return CreateOutputBitmap(true);
        }
    }
}
