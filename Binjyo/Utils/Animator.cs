using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;


namespace Binjyo
{
    internal class Animator
    {
        private static readonly Dictionary<Guid, Dictionary<string, Animator>> Animators = new Dictionary<Guid, Dictionary<string, Animator>>();
        public static Animator Start(
            Guid id,
            string name,
            Control host,
            Action<double> stepAction,
            double speed,
            double targetDelta = 0,
            double minInterval = 0.05,
            Action stopAction = null)
        {
            if (!Animators.ContainsKey(id))
                Animators[id] = new Dictionary<string, Animator>();

            var anim = Animators[id].ContainsKey(name) ? Animators[id][name] : new Animator(host, stepAction, speed, targetDelta, minInterval, stopAction);
            Animators[id][name] = anim;

            anim.targetDelta = targetDelta;
            anim.speed = speed;
            anim.minInterval = minInterval;
            anim.stepAction = stepAction;
            anim.stopAction = stopAction;
            anim.StartThis();
            return anim;
        }
        public static void Clear(Guid id)
        {
            if (!Animators.ContainsKey(id)) return;
            foreach (var animator in Animators[id].Values)
                animator.StopThis();

            Animators[id].Clear();
        }

        public static void Stop(Guid id, string name)
        {
            if (!Animators.ContainsKey(id)) return;
            if (!Animators[id].ContainsKey(name)) return;
            Animators[id][name].StopThis();
        }

        private readonly Stopwatch sw = new Stopwatch();
        public bool IsAnimating { get; private set; } = false;
        private long lastTicks;
        private readonly Control host;
        public double targetDelta;
        public double speed;
        public double minInterval = 0.1;
        private Action<double> stepAction;
        private Action stopAction;

        private Animator(
            Control host,
            Action<double> stepAction,
            double speed,
            double targetDelta = 0,
            double minInterval = 0.1,
            Action stopAction = null)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.stepAction = stepAction;
            this.speed = speed;
            this.targetDelta = targetDelta;
            this.minInterval = minInterval;
            this.stopAction = stopAction;
        }
        /// <summary>
        /// Start animation on the UI queue without relying on WM_TIMER.
        /// </summary>
        private void StartThis()
        {
            if (IsAnimating)
                return;

            IsAnimating = true;
            sw.Restart();
            lastTicks = sw.ElapsedTicks;
            host.BeginInvoke((Action)Run);
        }

        /// <summary>
        /// Stop animation and clear its timing state.
        /// </summary>
        private void StopThis()
        {
            if (!IsAnimating)
                return;

            IsAnimating = false;
            lastTicks = 0;
            sw.Reset();
            stopAction?.Invoke();
        }

        /// <summary>
        /// Advance animation using elapsed time and immediately queue the next frame on the UI thread.
        /// </summary>
        private void Run()
        {
            if (!IsAnimating)
                return;

            if (Math.Abs(targetDelta) < 0.001)
            {
                targetDelta = 0;
                StopThis();
                return;
            }

            var currentTicks = sw.ElapsedTicks;
            double elapsedSeconds = (currentTicks - lastTicks) / (double)Stopwatch.Frequency;
            if (elapsedSeconds < minInterval)
            {
                host.BeginInvoke((Action)Run);
                return;
            }
            lastTicks = currentTicks;

            // Linear
            double step = Math.Sign(targetDelta) * Math.Min(Math.Abs(targetDelta), elapsedSeconds * speed);

            targetDelta -= step;
            Console.WriteLine("anim run target " + minInterval + " , " + elapsedSeconds);

            // Callback
            if (IsAnimating && !host.IsDisposed && !host.Disposing)
                stepAction(step);

            // Queue next frame
            if (IsAnimating && !host.IsDisposed && !host.Disposing)
                host.BeginInvoke((Action)Run);
        }

    }
}
