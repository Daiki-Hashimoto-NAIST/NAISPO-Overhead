using System;

namespace Naispo.Overhead
{
    public struct ScheduledFoodDrop
    {
        public int Sequence;
        public float XOffset;
        public bool IsFish;
    }

    /// <summary>
    /// Plans each food before showing its warning. Advance only while gameplay is running;
    /// reading the preview does not advance time or consume random positions.
    /// </summary>
    public sealed class FoodDropSchedule
    {
        readonly FoodSpawnPattern pattern;
        readonly double interval, warningDuration, firstDelay;
        readonly int capacity;
        double delayRemaining, warningRemaining;
        bool warning, blocked;

        public ScheduledFoodDrop Upcoming { get; private set; }
        public int SpawnedCount { get; private set; }
        public bool HasWarning => warning && !blocked;
        public float WarningProgress => HasWarning
            ? (float)Math.Max(0, Math.Min(1, 1 - warningRemaining / warningDuration)) : 0;

        public FoodDropSchedule(FoodSpawnPattern pattern = null, float intervalSeconds = 1.8f,
            float warningSeconds = 0.65f, float firstDropDelaySeconds = 1f, int capacity = 7)
        {
            if (!Finite(intervalSeconds) || !Finite(warningSeconds) || !Finite(firstDropDelaySeconds)
                || warningSeconds <= 0 || intervalSeconds < warningSeconds || firstDropDelaySeconds < 0
                || capacity < 1)
                throw new ArgumentException("Drop timing must be finite, with a positive warning, interval >= warning, nonnegative first delay, and positive capacity.");

            this.pattern = pattern ?? new FoodSpawnPattern();
            interval = intervalSeconds;
            warningDuration = warningSeconds;
            firstDelay = firstDropDelaySeconds;
            this.capacity = capacity;
            Reset();
        }

        /// <summary>
        /// Returns at most one drop. Opening a warning never also drops the food in that call,
        /// even with a large dt. Excess time is discarded rather than causing catch-up drops.
        /// At capacity the schedule freezes and hides the cue. An interrupted warning restarts
        /// in full for the same planned food when a slot is available again.
        /// </summary>
        public bool Advance(float dt, int activeCount, out ScheduledFoodDrop drop)
        {
            drop = default;
            if (!Finite(dt) || dt < 0 || activeCount < 0)
                throw new ArgumentException("Elapsed time must be finite and nonnegative; active count must be nonnegative.");

            if (activeCount >= capacity)
            {
                blocked = true;
                if (warning) warningRemaining = warningDuration;
                return false;
            }
            if (blocked)
            {
                // The first available frame restores the cue without spending its visible time.
                blocked = false;
                return false;
            }
            if (!warning)
            {
                delayRemaining -= dt;
                if (delayRemaining <= 0)
                {
                    warning = true;
                    warningRemaining = warningDuration;
                }
                return false;
            }

            warningRemaining -= dt;
            if (warningRemaining > 0) return false;

            drop = Upcoming;
            SpawnedCount++;
            PlanNext();
            warning = false;
            warningRemaining = warningDuration;
            delayRemaining = interval - warningDuration;
            return true;
        }

        public void Reset()
        {
            SpawnedCount = 0;
            warning = blocked = false;
            warningRemaining = warningDuration;
            // Even a requested first delay shorter than the warning still gets a full warning.
            delayRemaining = Math.Max(0, firstDelay - warningDuration);
            pattern.Reset();
            PlanNext();
        }

        // If a celebration hides an existing cue, show the full warning again afterwards.
        public void RestartWarning()
        {
            if(warning)warningRemaining=warningDuration;
        }

        void PlanNext()
        {
            Upcoming = new ScheduledFoodDrop
            {
                Sequence = SpawnedCount,
                XOffset = pattern.NextOffset(),
                IsFish = SpawnedCount % 2 == 1
            };
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
