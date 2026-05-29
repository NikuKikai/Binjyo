using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;


namespace Binjyo
{
    internal class Animator
    {
        private static readonly Dictionary<Guid, Dictionary<string, Animator>> Animators = new Dictionary<Guid, Dictionary<string, Animator>>();
        public static void Start(
            Guid id, string name, Control host, Action<double> stepAction, double speed, double targetDelta = 0)
        {
            if (!Animators.ContainsKey(id))
                Animators[id] = new Dictionary<string, Animator>();

            if (Animators[id].ContainsKey(name))
            {
                Animators[id][name].targetDelta = targetDelta;
                Animators[id][name].speed = speed;
                Animators[id][name].StartThis();
            }
            else
                Animators[id][name] = new Animator(host, stepAction, speed, targetDelta);
        }
        public static void Clear(Guid id)
        {
            if (!Animators.ContainsKey(id)) return;
            foreach (var animator in Animators[id].Values)
                animator.StopThis();

            Animators[id].Clear();
        }

        private readonly Stopwatch sw = new Stopwatch();
        public bool IsAnimating { get; private set; } = false;
        private long lastTicks;
        private readonly Control host;
        public double targetDelta;
        public double speed;
        private readonly Action<double> stepAction;

        public Animator(Control host, Action<double> stepAction, double speed, double targetDelta = 0)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.stepAction = stepAction;
            this.speed = speed;
            this.targetDelta = targetDelta;
            StartThis();
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
            lastTicks = currentTicks;

            // Linear
            double step = Math.Sign(targetDelta) * Math.Min(Math.Abs(targetDelta), elapsedSeconds * speed);

            targetDelta -= step;

            // Callback
            if (IsAnimating && !host.IsDisposed && !host.Disposing)
                stepAction(step);

            // Queue next frame
            if (IsAnimating && !host.IsDisposed && !host.Disposing)
                host.BeginInvoke((Action)Run);
        }

    }
}
