using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Binjyo
{
    internal static class CursorTeleportService
    {
        private const int PollIntervalMilliseconds = 16;
        private const int MaxTeleportDepth = 32;

        private static DispatcherTimer timer;
        private static bool isEnabled;
        private static bool wasEnterDown;
        private static Point lastTeleportedPoint = Point.Empty;
        private static bool hasLastTeleportedPoint;

        public static bool IsEnabled => isEnabled;

        public static void Initialize()
        {
            if (timer != null)
                return;

            timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(PollIntervalMilliseconds)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public static void Shutdown()
        {
            if (timer == null)
                return;

            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer = null;
            isEnabled = false;
            wasEnterDown = false;
            hasLastTeleportedPoint = false;
        }

        public static void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            hasLastTeleportedPoint = false;
        }

        private static void Timer_Tick(object sender, EventArgs e)
        {
            bool isEnterDown = WinService.IsKeyDown(Keys.Enter);
            if (isEnabled && isEnterDown && !wasEnterDown)
            {
                SetEnabled(false);
            }
            wasEnterDown = isEnterDown;

            if (!isEnabled)
                return;

            Point cursor = Cursor.Position;
            if (hasLastTeleportedPoint && cursor == lastTeleportedPoint)
                return;

            if (!TryResolveTeleportDestination(cursor, out Point destination, out MemoD11 focusMemo))
            {
                hasLastTeleportedPoint = false;
                return;
            }

            if (destination == cursor)
            {
                hasLastTeleportedPoint = false;
                return;
            }

            focusMemo?.FocusForCursorTeleport();
            if (WinService.TrySetCursorPosition(destination.X, destination.Y))
            {
                lastTeleportedPoint = destination;
                hasLastTeleportedPoint = true;
            }
        }

        private static bool TryResolveTeleportDestination(Point startPoint, out Point destination, out MemoD11 focusMemo)
        {
            destination = startPoint;
            focusMemo = null;

            HashSet<IntPtr> visitedMemoHandles = new HashSet<IntPtr>();
            Point currentPoint = startPoint;

            for (int depth = 0; depth < MaxTeleportDepth; depth++)
            {
                MemoD11 memo = FindTopMemoAtPoint(currentPoint);
                if (memo == null || !memo.IsCaptureCursorTeleportTarget)
                    break;

                if (!visitedMemoHandles.Add(memo.Handle))
                    break;

                if (!memo.TryGetCaptureTeleportTarget(currentPoint.X, currentPoint.Y, out Point nextPoint))
                    break;

                focusMemo = memo;
                destination = nextPoint;
                currentPoint = nextPoint;
            }

            return destination != startPoint;
        }

        private static MemoD11 FindTopMemoAtPoint(Point screenPoint)
        {
            return Application.OpenForms
                .OfType<MemoD11>()
                .Where(memo => memo.Visible && memo.ContainsScreenPoint(screenPoint.X, screenPoint.Y))
                .OrderByDescending(memo => memo.FocusOrder)
                .FirstOrDefault();
        }
    }
}
