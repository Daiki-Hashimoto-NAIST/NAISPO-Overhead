using System;
using UnityEngine;

namespace Naispo.Overhead
{
    public enum GardenPhase
    {
        Intro,
        Playing,
        Awakening,
        Complete
    }

    /// <summary>
    /// Records the twenty successful deliveries on the caller's game clock. Each success keeps
    /// the spirit side and the canvas-space mouth position that sent its light.
    /// </summary>
    public sealed class GardenProgress
    {
        public const int Goal = 20;
        public const int MaxLights = Goal;
        public const float LightFlightDuration = 1.10f;

        readonly LightRecord[] lights = new LightRecord[MaxLights];

        public int DeliveredCount { get; private set; }
        public int LightCount => DeliveredCount;
        public bool IsComplete => DeliveredCount == Goal;

        public GardenProgress() => Reset();

        /// <summary>Records one success. Returns false once the twentieth light is already stored.</summary>
        public bool Deliver(float playTime, bool right, Vector2 source)
        {
            if (IsComplete) return false;
            if (!Finite(playTime))
                throw new ArgumentOutOfRangeException(nameof(playTime), "Game time must be finite.");
            if (!Finite(source))
                throw new ArgumentOutOfRangeException(nameof(source), "Light source must be finite.");

            lights[DeliveredCount] = new LightRecord(playTime, right, source);
            DeliveredCount++;
            return true;
        }

        /// <summary>Returns a stored success, or throws when the index has not been earned yet.</summary>
        public LightRecord LightAt(int index)
        {
            if (index < 0 || index >= DeliveredCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return lights[index];
        }

        public float LightTime(int index) => LightAt(index).Time;
        public bool LightIsRight(int index) => LightAt(index).Right;
        public Vector2 LightSource(int index) => LightAt(index).Source;

        /// <summary>The final light's arrival time; negative infinity until the garden is complete.</summary>
        public float FinalArrivalAt
        {
            get
            {
                if (!IsComplete) return float.NegativeInfinity;
                float latest = float.NegativeInfinity;
                for (int i = 0; i < DeliveredCount; i++)
                {
                    float arrival = lights[i].Time + LightFlightDuration;
                    if (arrival > latest) latest = arrival;
                }
                return latest;
            }
        }

        public void Reset()
        {
            DeliveredCount = 0;
            for (int i = 0; i < lights.Length; i++)
                lights[i] = default;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);

        public readonly struct LightRecord
        {
            public readonly float Time;
            public readonly bool Right;
            public readonly Vector2 Source;

            public LightRecord(float time, bool right, Vector2 source)
            {
                Time = time;
                Right = right;
                Source = source;
            }
        }
    }
}
