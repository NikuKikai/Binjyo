
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;


namespace Binjyo
{
    public static class CaptureScreen
    {
        public static WriteableBitmap Run()
        {
            // 1. Win32 APIから「全画面を包含する仮想画面」の物理ピクセルサイズ・座標を取得
            // ※ SystemParameters.VirtualScreenは論理単位（DPI影響を受ける）なので使いません。
            int left = Geo.GetSystemMetrics(Geo.SM_XVIRTUALSCREEN);
            int top = Geo.GetSystemMetrics(Geo.SM_YVIRTUALSCREEN);
            int width = Geo.GetSystemMetrics(Geo.SM_CXVIRTUALSCREEN);
            int height = Geo.GetSystemMetrics(Geo.SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("画面サイズを取得できませんでした。");

            // 2. 物理ピクセルサイズのWriteableBitmapを作成 (96DPI指定で論理・物理を1:1にする)
            WriteableBitmap wbitmap = new WriteableBitmap(
                width, height, 96, 96, PixelFormats.Bgra32, null);

            // 3. 画面のデバイスコンテキスト(DC)を取得
            IntPtr hScreenDC = GetDC(IntPtr.Zero);
            IntPtr hMemoryDC = CreateCompatibleDC(hScreenDC);

            // 4. GDIセクション（WPFと互換性のある共有メモリ）からビットマップを作成
            // これにより、GDIの描画結果を確実にWPFが認識できるようになります
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // 上下反転を防ぐためマイナスを指定
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            IntPtr pBits;
            IntPtr hBitmap = CreateDIBSection(hScreenDC, ref bmi, 0, out pBits, IntPtr.Zero, 0);
            IntPtr hOldBitmap = SelectObject(hMemoryDC, hBitmap);

            // 5. 画面からメモリDC（DIBSection）へ高速コピー
            uint dwRop = 0x00CC0020 | 0x40000000; // SRCCOPY | CAPTUREBLT
            BitBlt(hMemoryDC, 0, 0, width, height, hScreenDC, left, top, dwRop);

            // 6. WriteableBitmapのバッファへ安全にデータをコピー
            wbitmap.Lock();
            try
            {
                // メモリからWPFのバックバッファへ一括コピー
                long size = width * height * 4;
                CopyMemory(wbitmap.BackBuffer, pBits, (uint)size);

                // 変更領域をWPFに通知（これを呼ばないと画面が更新されません）
                wbitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                wbitmap.Unlock();

                // GDIリソースを確実に解放
                SelectObject(hMemoryDC, hOldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(hMemoryDC);
                ReleaseDC(IntPtr.Zero, hScreenDC);
            }

            return wbitmap;
        }

        public static WriteableBitmap RunPrimary()
        {
            // 1. 画面サイズを取得
            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;

            // 2. WPF用のWriteableBitmapを作成 (一般的なBGRA 32bit形式)
            WriteableBitmap wbitmap = new WriteableBitmap(
                width, height, 96, 96, PixelFormats.Bgra32, null);

            // 3. 画面のデバイスコンテキスト(DC)からデータを転送
            IntPtr hScreenDC = GetDC(IntPtr.Zero);
            IntPtr hMemoryDC = CreateCompatibleDC(hScreenDC);

            // WriteableBitmapのバックバッファを直接操作
            wbitmap.Lock();
            try
            {
                IntPtr hBitmap = CreateCompatibleBitmap(hScreenDC, width, height);
                IntPtr hOldBitmap = SelectObject(hMemoryDC, hBitmap);

                // 画面からメモリDCへコピー
                BitBlt(hMemoryDC, 0, 0, width, height, hScreenDC, 0, 0, 0x00CC0020); // SRCCOPY

                // メモリDCからWriteableBitmapのバッファへピクセルを直接コピー
                int stride = width * 4;
                GetDIBits(hMemoryDC, hBitmap, 0, (uint)height, wbitmap.BackBuffer, ref stride, 0);

                // 描画領域を更新通知
                wbitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));

                // 後片付け
                SelectObject(hMemoryDC, hOldBitmap);
                DeleteObject(hBitmap);
            }
            finally
            {
                wbitmap.Unlock();
                DeleteDC(hMemoryDC);
                ReleaseDC(IntPtr.Zero, hScreenDC);
            }

            return wbitmap;
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;    // publicを付与
            public int biHeight;   // publicを付与
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr ptr);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        public static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref int lpbi, uint uUsage);
        [DllImport("gdi32.dll")] 
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint cb);


    }
}
