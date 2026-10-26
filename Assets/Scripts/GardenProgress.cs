using System;

namespace Naispo.Overhead
{
    /// <summary>Counts successful deliveries and records bloom times on the caller's game clock.</summary>
    public sealed class GardenProgress
    {
        public const int Goal = 30;
        public const int DeliveriesPerBloom = 10;
        public const int StageCount = 3;

        readonly float[] bloomStartedAt = new float[StageCount];

        public int DeliveredCount { get; private set; }
        public int BloomCount => DeliveredCount / DeliveriesPerBloom;
        public bool IsComplete => DeliveredCount == Goal;

        public GardenProgress() => Reset();

        /// <summary>Records one success. Returns true only when a new bloom is earned.</summary>
        public bool Deliver(float playTime)
        {
            if (IsComplete) return false;
            if (float.IsNaN(playTime) || float.IsInfinity(playTime))
                throw new ArgumentOutOfRangeException(nameof(playTime), "Game time must be finite.");

            DeliveredCount++;
            if (DeliveredCount % DeliveriesPerBloom != 0) return false;
            bloomStartedAt[BloomCount - 1] = playTime;
            return true;
        }

        /// <summary>Returns a zero-based stage's bloom time, or negative infinity before it blooms.</summary>
        public float BloomStartedAt(int stage)
        {
            if (stage < 0 || stage >= StageCount)
                throw new ArgumentOutOfRangeException(nameof(stage));
            return bloomStartedAt[stage];
        }

        public void Reset()
        {
            DeliveredCount = 0;
            for (int stage = 0; stage < StageCount; stage++)
                bloomStartedAt[stage] = float.NegativeInfinity;
        }
    }
}
