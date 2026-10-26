using System;
using UnityEngine;

namespace Naispo.Overhead
{
    /// <summary>Recovers caught food only after it can no longer return to the rotating paddle.</summary>
    public static class FoodDeliveryRules
    {
        const float Clearance = 1f;

        /// <summary>
        /// Returns 0 to keep simulating, 1 to deliver left, or 2 to deliver right.
        /// Call after motion/contact processing and before the usual off-screen exit test.
        /// Positive Y points down; free food has constant horizontal velocity and downward gravity.
        /// The paddle's full rotation is bounded, so changing its angle cannot cause early pickup.
        /// </summary>
        public static int DepartedSide(bool hasTouchedGate, Vector2 position, Vector2 velocity,
            Vector2 pivot, float halfLength, float radius, float gateHalfThickness)
        {
            if (!Finite(position) || !Finite(velocity) || !Finite(pivot)
                || !Finite(halfLength) || !Finite(radius) || !Finite(gateHalfThickness)
                || halfLength <= 0 || radius <= 0 || gateHalfThickness < 0)
                throw new ArgumentException("Food and paddle geometry must be finite, with positive length/radius and nonnegative thickness.");

            if (!hasTouchedGate || velocity.y <= 0) return 0;

            double x = (double)position.x - pivot.x, y = (double)position.y - pivot.y;
            // Food still spanning the center has not clearly departed either side.
            // In particular, tiny numerical offsets must not decide its recipient.
            if (Math.Abs(x) <= radius) return 0;
            int side = x < 0 ? 1 : 2;
            double reach = (double)halfLength + radius + gateHalfThickness + Clearance;

            // Beyond the whole horizontal reach and travelling outward: even a future turn
            // cannot catch this food, including while it falls from above the pivot.
            if (Math.Abs(x) > reach && x * velocity.x >= 0) return side;

            // Below the pivot, an already-outward radius only grows under downward gravity:
            // d(r dot v)/dt = |v|^2 + y * gravity > 0. Waiting for this condition also handles
            // weak launches and large frames without stealing food that can bounce again.
            if (y >= 0 && x * x + y * y > reach * reach
                && x * velocity.x + y * velocity.y >= 0)
                return side;

            return 0;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
    }
}
