using System;
using UnityEngine;

namespace Naispo.Overhead
{
    public struct GateHit
    {
        public float Time;
        public float AngleDegrees;
        public Vector2 Point;
        public Vector2 Normal;
        // Corrected circle center at the contact time, including a small separation margin.
        public Vector2 Center;
        public bool StartedOverlapping;
    }

    /// <summary>One-sided swept circle contact with a rotating, rounded gate.</summary>
    public static class GateCollision
    {
        const float SeparationMargin = 0.05f;
        const float PositionTolerance = 0.001f;
        const float MotionTolerance = 0.00001f;

        /// <summary>
        /// Angles are image-space degrees (Y down). A gate has no directed front/back, so angles
        /// interpolate along the shortest 180-degree-equivalent arc, at most 90 degrees per call.
        /// At a vertical gate, the upper side changes: this alone never counts as an impact.
        /// For longer rotations, call with intermediate angles. Center is resolved at Time;
        /// a caller advancing the gate farther should resolve overlap against its final pose too.
        /// </summary>
        public static bool SweepCircle(Vector2 from, Vector2 to, Vector2 pivot,
            float fromAngle, float toAngle, float halfLength, float circleRadius,
            float gateHalfThickness, out GateHit hit)
        {
            hit = default;
            if (!Finite(from) || !Finite(to) || !Finite(pivot) || !Finite(fromAngle) || !Finite(toAngle)
                || !Finite(halfLength) || !Finite(circleRadius) || !Finite(gateHalfThickness)
                || halfLength <= 0 || circleRadius <= 0 || gateHalfThickness < 0)
                throw new ArgumentException("Gate geometry must be finite, with positive length/radius and nonnegative thickness.");

            double startAngle = Fold(fromAngle);
            double angleDelta = Fold((double)toAngle - fromAngle);
            float totalRadius = circleRadius + gateHalfThickness;
            Vector2 movement = to - from;
            float angularMotion = (float)(angleDelta * Math.PI / 180);
            Sample previous = At(0, from, movement, pivot, startAngle, angleDelta, halfLength, totalRadius);
            float approach = Approach(previous, movement, pivot, angularMotion);

            // Repair only upper-side overlap. An object rising through the underside must pass.
            if (IsUpper(previous) && previous.Gap <= 0)
            {
                bool penetrating = previous.Gap < -PositionTolerance;
                if ((penetrating && approach <= MotionTolerance) || approach < -MotionTolerance)
                {
                    hit = Contact(previous, 0, circleRadius, gateHalfThickness, penetrating);
                    return true;
                }
            }

            // Bound both circle travel and endpoint travel between probes. This is intentionally
            // a practical game sweep; extremely shallow grazing is not an exact analytic CCD.
            double travel = movement.magnitude + halfLength * Math.Abs(angularMotion);
            double stride = Math.Max(0.5, Math.Min(4, totalRadius * 0.2));
            int steps = (int)Math.Max(1, Math.Min(4096, Math.Ceiling(travel / stride)));
            float previousTime = 0;
            for (int step = 1; step <= steps; step++)
            {
                float time = (float)step / steps;
                Sample current = At(time, from, movement, pivot, startAngle, angleDelta, halfLength, totalRadius);
                // Unsigned capsule distance is continuous even when the upward normal flips at 90°.
                if (previous.Gap >= 0 && current.Gap <= 0)
                {
                    float low = previousTime, high = time;
                    Sample contact = current;
                    for (int iteration = 0; iteration < 18; iteration++)
                    {
                        float middle = (low + high) * 0.5f;
                        Sample probe = At(middle, from, movement, pivot, startAngle, angleDelta, halfLength, totalRadius);
                        if (probe.Gap > 0) low = middle;
                        else { high = middle; contact = probe; }
                    }
                    // A touching circle at the pivot can have zero normal speed at time zero,
                    // yet rotation immediately pushes the gate into it (second-order motion).
                    bool approaching = Approach(contact, movement, pivot, angularMotion) < -MotionTolerance
                        || (current.Gap < -PositionTolerance && IsUpper(current));
                    if (IsUpper(contact) && approaching)
                    {
                        hit = Contact(contact, high, circleRadius, gateHalfThickness, false);
                        return true;
                    }
                }
                // Overlap recovery is only evaluated at time zero, never halfway through an
                // underside pass. Otherwise a rising object would bounce when it reached the top.
                previous = current;
                previousTime = time;
            }
            return false;
        }

        struct Sample
        {
            public Vector2 Closest, Normal, Up;
            public float Gap, Angle;
            public bool AtEnd;
        }

        static Sample At(float time, Vector2 from, Vector2 movement, Vector2 pivot,
            double startAngle, double angleDelta, float halfLength, float totalRadius)
        {
            double angle = Fold(startAngle + angleDelta * time);
            double radians = angle * Math.PI / 180;
            var tangent = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians));
            var up = new Vector2(tangent.y, -tangent.x);
            Vector2 position = from + movement * time;
            float along = Mathf.Clamp(Vector2.Dot(position - pivot, tangent), -halfLength, halfLength);
            Vector2 closest = pivot + tangent * along;
            Vector2 difference = position - closest;
            float distance = difference.magnitude;
            return new Sample
            {
                Closest = closest,
                Normal = distance > PositionTolerance ? difference / distance : up,
                Up = up,
                Gap = distance - totalRadius,
                Angle = (float)angle,
                AtEnd = Math.Abs(along) >= halfLength - PositionTolerance
            };
        }

        // Food falling onto the top of a round tip should also count on the downhill side of a
        // steep gate. For tips use screen-up; along the shaft use the gate's upward-facing side.
        static bool IsUpper(Sample sample) => sample.AtEnd
            ? sample.Normal.y <= PositionTolerance
            : Vector2.Dot(sample.Normal, sample.Up) >= -PositionTolerance;

        static float Approach(Sample sample, Vector2 movement, Vector2 pivot, float angularMotion)
        {
            Vector2 arm = sample.Closest - pivot;
            Vector2 gateMovement = new Vector2(-arm.y, arm.x) * angularMotion;
            return Vector2.Dot(movement - gateMovement, sample.Normal);
        }

        static GateHit Contact(Sample sample, float time, float radius, float halfThickness, bool overlapping)
        {
            Vector2 point = sample.Closest + sample.Normal * halfThickness;
            return new GateHit
            {
                Time = time,
                AngleDegrees = sample.Angle,
                Point = point,
                Normal = sample.Normal,
                Center = point + sample.Normal * (radius + SeparationMargin),
                StartedOverlapping = overlapping
            };
        }

        static double Fold(double angle) => angle - Math.Floor((angle + 90) / 180) * 180;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
    }
}
