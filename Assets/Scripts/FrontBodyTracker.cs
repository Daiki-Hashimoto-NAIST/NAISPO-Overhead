using System;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;
using BodyJoint = Microsoft.Azure.Kinect.BodyTracking.Joint;

namespace Naispo.Overhead
{
    /// <summary>Worker-owned SDK adapter. A selected person is kept until capture restarts.</summary>
    internal sealed class FrontBodyTracker : IDisposable
    {
        readonly Calibration calibration;
        Tracker tracker;
        uint selectedBodyId;
        public bool HasSelectedBody { get; private set; }

        public FrontBodyTracker(Calibration calibration, string modelPath)
        {
            this.calibration = calibration;
            var config = TrackerConfiguration.Default;
            config.SensorOrientation = SensorOrientation.Default;
            config.ProcessingMode = TrackerProcessingMode.DirectML;
            config.ModelPath = modelPath;
            tracker = Tracker.Create(calibration, config);
            try
            {
                // This spelling is part of the official 1.1.2 managed wrapper.
                tracker.SetTemporalSmooting(0.2f);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void EnqueueCapture(Capture capture, TimeSpan timeout) => tracker.EnqueueCapture(capture, timeout);
        public Frame PopResult(TimeSpan timeout) => tracker.PopResult(timeout, false);

        public FrontPose ReadPose(Frame frame)
        {
            int selectedIndex = -1;
            if (!HasSelectedBody)
            {
                float nearestDepth = float.PositiveInfinity;
                for (uint i = 0; i < frame.NumberOfBodies; ++i)
                {
                    var pelvis = frame.GetBodySkeleton(i).GetJoint(JointId.Pelvis);
                    if (pelvis.ConfidenceLevel == JointConfidenceLevel.None || !ValidPosition(pelvis.Position)) continue;
                    if (pelvis.Position.Z >= nearestDepth) continue;
                    nearestDepth = pelvis.Position.Z;
                    selectedIndex = (int)i;
                }
                if (selectedIndex >= 0)
                {
                    selectedBodyId = frame.GetBodyId((uint)selectedIndex);
                    HasSelectedBody = true;
                }
            }
            else
            {
                for (uint i = 0; i < frame.NumberOfBodies; ++i)
                    if (frame.GetBodyId(i) == selectedBodyId) { selectedIndex = (int)i; break; }
            }

            // Do not substitute another person or reuse wrists when the selected body vanishes.
            var pose = new FrontPose { BodyId = selectedBodyId };
            if (selectedIndex < 0) return pose;
            var skeleton = frame.GetBodySkeleton((uint)selectedIndex);
            var left = skeleton.GetJoint(JointId.WristLeft);
            var right = skeleton.GetJoint(JointId.WristRight);
            pose.HasBody = true;
            pose.LeftWristMm = new Vector3(left.Position.X, left.Position.Y, left.Position.Z);
            pose.RightWristMm = new Vector3(right.Position.X, right.Position.Y, right.Position.Z);
            pose.LeftConfidence = (int)left.ConfidenceLevel;
            pose.RightConfidence = (int)right.ConfidenceLevel;
            pose.LeftPixel = Project(left);
            pose.RightPixel = Project(right);
            return pose;
        }

        Vector2 Project(BodyJoint joint)
        {
            if (joint.ConfidenceLevel >= JointConfidenceLevel.Medium && ValidPosition(joint.Position))
            {
                var pixel = calibration.TransformTo2D(joint.Position, CalibrationDeviceType.Depth, CalibrationDeviceType.Depth);
                if (pixel.HasValue && Finite(pixel.Value.X) && Finite(pixel.Value.Y)
                    && pixel.Value.X >= 0 && pixel.Value.X < calibration.DepthCameraCalibration.ResolutionWidth
                    && pixel.Value.Y >= 0 && pixel.Value.Y < calibration.DepthCameraCalibration.ResolutionHeight)
                    return new Vector2(pixel.Value.X, pixel.Value.Y);
            }
            return new Vector2(float.NaN, float.NaN);
        }

        static bool ValidPosition(System.Numerics.Vector3 position)
            => Finite(position.X) && Finite(position.Y) && Finite(position.Z) && position.Z > 0;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Dispose()
        {
            var releasing = tracker;
            tracker = null;
            if (releasing == null) return;
            try { releasing.Shutdown(); }
            finally { releasing.Dispose(); }
        }
    }
}
