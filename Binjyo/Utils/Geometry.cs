using System.Collections.Generic;
using System.Windows;


namespace Binjyo
{
    public static class Geo
    {

        public static bool DoRectsOverlap(Rect a, Rect b)
        {
            return a.Right >= b.Left && b.Right >= a.Left &&
                   a.Bottom >= b.Top && b.Bottom >= a.Top;
        }

        public static bool DoSegmentsOverlap(double a1, double a2, double b1, double b2)
        {
            return a2 >= b1 && b2 >= a1;
        }

    }
}