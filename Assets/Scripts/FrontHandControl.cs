using System;
using UnityEngine;

namespace Naispo.Overhead
{
    /// <summary>A single tracked body's wrists, in camera coordinates (X right, Y down).</summary>
    public struct FrontPose
    {
        public bool HasBody;
        public uint BodyId;
        public Vector3 LeftWristMm, RightWristMm;
        public Vector2 LeftPixel, RightPixel;
        public int LeftConfidence, RightConfidence;
    }

    public enum FrontPoseProblem
    {
        None,
        NoBody,
        LowConfidence,
        HandsTooClose,
        OutOfRange,
        InvalidPosition
    }

    public struct HandLineResult
    {
        public bool Valid;
        public float AngleDegrees;
        // Mean wrist depth from the camera, not the distance between the hands.
        public float DistanceMm;
        public FrontPoseProblem Problem;
    }

    /// <summary>Converts a reliable pair of wrists into a bounded, unoriented gate tilt.</summary>
    public static class FrontHandControl
    {
        public static HandLineResult Evaluate(FrontPose pose, float minDistanceMm, float maxDistanceMm,
            bool mirror, bool invert)
        {
            if (!Finite(minDistanceMm) || !Finite(maxDistanceMm)
                || minDistanceMm < 0 || maxDistanceMm < minDistanceMm)
                throw new ArgumentException("Camera distance limits must be finite, nonnegative, and ordered.");

            if (!pose.HasBody)
                return Invalid(FrontPoseProblem.NoBody);
            // Require at least Medium (2); Low can be a predicted, hidden joint.
            if (pose.LeftConfidence < 2 || pose.RightConfidence < 2)
                return Invalid(FrontPoseProblem.LowConfidence);

            Vector3 left = pose.LeftWristMm, right = pose.RightWristMm;
            if (!Finite(left) || !Finite(right) || !Finite(pose.LeftPixel) || !Finite(pose.RightPixel)
                || left.z <= 0 || right.z <= 0)
                return Invalid(FrontPoseProblem.InvalidPosition);

            float distance = left.z * 0.5f + right.z * 0.5f;
            if (left.z < minDistanceMm || left.z > maxDistanceMm
                || right.z < minDistanceMm || right.z > maxDistanceMm)
                return Invalid(FrontPoseProblem.OutOfRange, distance);

            // Horizontal separation matters: vertically stacked hands do not define a stable tilt.
            double dx = (double)right.x - left.x;
            if (Math.Abs(dx) < 200)
                return Invalid(FrontPoseProblem.HandsTooClose, distance);

            double degrees = Math.Atan2((double)right.y - left.y, dx) * 180 / Math.PI;
            if (degrees >= 90) degrees -= 180;
            else if (degrees < -90) degrees += 180;
            if (mirror) degrees = -degrees;
            if (invert) degrees = -degrees;

            return new HandLineResult
            {
                Valid = true,
                AngleDegrees = Mathf.Clamp((float)degrees, -65, 65),
                DistanceMm = distance,
                Problem = FrontPoseProblem.None
            };
        }

        private static HandLineResult Invalid(FrontPoseProblem problem, float distance = 0)
            => new HandLineResult { Problem = problem, DistanceMm = distance };

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
