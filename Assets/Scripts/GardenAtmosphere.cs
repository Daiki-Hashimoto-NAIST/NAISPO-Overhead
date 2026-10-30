using UnityEngine;

namespace Naispo.Overhead
{
    // Evaluate on the game's clock so pausing, repeated GUI passes and replay stay in sync.
    public static class GardenAtmosphere
    {
        public const float FinaleDuration = 3.4f;
        public const float WallDelay = 0.16f;
        public const float FloorDelay = 0.32f;

        /// <summary>
        /// The continuous room light: each arrived delivery contributes 1/20 after its 0.65s fade.
        /// Arrivals are evaluated independently, so overlapping successes never lose a contribution.
        /// </summary>
        public static float Progress(GardenProgress garden, float playTime)
        {
            if (garden == null || !Finite(playTime)) return 0;
            float total = 0;
            for (int i = 0; i < garden.LightCount; i++)
            {
                var record = garden.LightAt(i);
                if (!Finite(record.Time)) continue;
                float arrivalAt = record.Time + GardenProgress.LightFlightDuration;
                float t = Mathf.Clamp01((playTime - arrivalAt) / LanternRoom.FadeDuration);
                total += t * t * (3 - 2 * t);
            }
            return Mathf.Clamp01(total / GardenProgress.Goal);
        }

        // The main lamp responds first, then the walls and floor catch the delayed warm light.
        public static float Warmth(GardenProgress garden, float playTime) => Progress(garden, playTime);

        public static float WallWarmth(GardenProgress garden, float playTime) =>
            Delayed(garden, playTime, WallDelay);

        public static float FloorWarmth(GardenProgress garden, float playTime) =>
            Delayed(garden, playTime, FloorDelay);

        // Window decorations appear from the squared wall light, so they stay subtle until late.
        public static float WindowReveal(GardenProgress garden, float playTime)
        {
            float wall = WallWarmth(garden, playTime);
            return wall * wall;
        }

        static float Delayed(GardenProgress garden, float playTime, float delay)
        {
            if (!Finite(playTime)) return 0;
            return Progress(garden, playTime - delay);
        }

        public static float BreezeAngle(GardenProgress garden, float playTime, float rootX)
        {
            if (garden == null || !Finite(playTime) || !Finite(rootX) || garden.LightCount == 0) return 0;
            float progress = Progress(garden, playTime);
            if (progress <= 0.5f) return 0;
            float strength = Mathf.Clamp01((progress - 0.5f) * 2);
            // A slow gust crosses left to right, followed by seven seconds of complete calm.
            float elapsed = playTime - garden.LightAt(0).Time - 3.8f;
            float phase = Mathf.Repeat(elapsed, 12.5f) - Mathf.Clamp01(rootX / 1440f) * 1.8f;
            if (phase <= 0 || phase >= 3.4f) return 0;
            float wave = Mathf.Sin(phase / 3.4f * Mathf.PI);
            return 4.6f * strength * wave * wave;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
