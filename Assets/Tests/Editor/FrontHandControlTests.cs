using System;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class FrontHandControlTests
    {
        [Test]
        public void LevelWristsAreHorizontalEvenAtDifferentDepths()
        {
            FrontPose pose = Pose(new Vector3(-300, 100, 1000), new Vector3(300, 100, 3000));
            HandLineResult result = Evaluate(pose);
            AssertValid(result, 0);
            Assert.That(result.DistanceMm, Is.EqualTo(2000), "Reported distance is mean camera depth");
        }

        [TestCase(300f, 36.8699f)]
        [TestCase(-300f, -36.8699f)]
        public void WristHeightDifferenceTiltsTowardTheLowerHand(float rightY, float expected)
        {
            AssertValid(Evaluate(Pose(new Vector3(-200, 0, 2000), new Vector3(200, rightY, 2000))), expected);
        }

        [TestCase(false, false, 36.8699f)]
        [TestCase(true, false, -36.8699f)]
        [TestCase(false, true, -36.8699f)]
        [TestCase(true, true, 36.8699f)]
        public void MirrorAndRotationInvertEachChangeTheSignOnce(bool mirror, bool invert, float expected)
        {
            FrontPose pose = Pose(new Vector3(-200, 0, 2000), new Vector3(200, 300, 2000));
            AssertValid(FrontHandControl.Evaluate(pose, 500, 3500, mirror, invert), expected);
        }

        [Test]
        public void ReversingAnatomicalHandOrderKeepsTheSameUnorientedLine()
        {
            FrontPose pose = Pose(new Vector3(200, 300, 2000), new Vector3(-200, 0, 2000));
            AssertValid(Evaluate(pose), 36.8699f);
        }

        [Test]
        public void BentArmsWithTheSameWristsKeepTheSameTilt()
        {
            // Bent elbows need not be outside the hands or form a long silhouette.
            // The control contract intentionally has no elbow or whole-body-axis input.
            FrontPose pose = Pose(new Vector3(-125, 100, 1800), new Vector3(125, 225, 1800));
            AssertValid(Evaluate(pose), 26.5651f);
            // Pixel projection is for the preview; it must not replace the physical wrist line.
            pose.LeftPixel = new Vector2(310, 200);
            pose.RightPixel = new Vector2(330, 400);
            AssertValid(Evaluate(pose), 26.5651f);
        }

        [TestCase(1000f, 65f)]
        [TestCase(-1000f, -65f)]
        public void SteepWristLinesStayWithinPlayableTilt(float rightY, float expected)
        {
            AssertValid(Evaluate(Pose(new Vector3(-100, 0, 2000), new Vector3(100, rightY, 2000))), expected);
        }

        [Test]
        public void SmallTiltsRemainAvailableWithoutADeadZone()
        {
            AssertValid(Evaluate(Pose(new Vector3(-300, 0, 2000), new Vector3(300, 10, 2000))), 0.95484f);
        }

        [Test]
        public void MissingBodyCannotReuseAFormerWristPair()
        {
            FrontPose pose = Pose();
            pose.HasBody = false;
            AssertProblem(Evaluate(pose), FrontPoseProblem.NoBody);
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        [TestCase(0, 2)]
        [TestCase(2, 0)]
        public void EitherPredictedOrMissingWristPausesControl(int left, int right)
        {
            FrontPose pose = Pose();
            pose.LeftConfidence = left;
            pose.RightConfidence = right;
            AssertProblem(Evaluate(pose), FrontPoseProblem.LowConfidence);
        }

        [Test]
        public void MediumAndHighConfidenceWristsAreAccepted()
        {
            FrontPose pose = Pose();
            pose.RightConfidence = 3;
            AssertValid(Evaluate(pose), 0);
        }

        [TestCase(199.9f)]
        [TestCase(-199.9f)]
        [TestCase(0f)]
        public void NearOrCrossingHandsAreRejectedEvenWhenVerticallyFarApart(float rightX)
        {
            FrontPose pose = Pose(new Vector3(0, -500, 2000), new Vector3(rightX, 500, 2000));
            AssertProblem(Evaluate(pose), FrontPoseProblem.HandsTooClose);
        }

        [Test]
        public void MinimumHorizontalSeparationIsInclusive()
        {
            AssertValid(Evaluate(Pose(new Vector3(-100, 0, 2000), new Vector3(100, 0, 2000))), 0);
        }

        [TestCase(499f, 2000f)]
        [TestCase(2000f, 499f)]
        [TestCase(3501f, 2000f)]
        [TestCase(2000f, 3501f)]
        public void EitherWristOutsideTheDepthBandIsRejectedDespiteAnInRangeMean(float leftZ, float rightZ)
        {
            FrontPose pose = Pose(new Vector3(-300, 0, leftZ), new Vector3(300, 0, rightZ));
            AssertProblem(Evaluate(pose), FrontPoseProblem.OutOfRange);
        }

        [Test]
        public void BothDistanceLimitsAreInclusive()
        {
            AssertValid(Evaluate(Pose(new Vector3(-300, 0, 500), new Vector3(300, 0, 3500))), 0);
        }

        [Test]
        public void EqualDistanceLimitsAllowAnExactDepth()
        {
            AssertValid(FrontHandControl.Evaluate(Pose(), 2000, 2000, false, false), 0);
        }

        [TestCase(0f, 2000f)]
        [TestCase(2000f, 0f)]
        [TestCase(-1f, 2000f)]
        [TestCase(2000f, -1f)]
        public void ZeroOrBehindCameraWristsAreInvalidPositions(float leftZ, float rightZ)
        {
            AssertProblem(Evaluate(Pose(new Vector3(-300, 0, leftZ), new Vector3(300, 0, rightZ))),
                FrontPoseProblem.InvalidPosition);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonfiniteWristOrPreviewCoordinatesAreRejected(float invalid)
        {
            for (int coordinate = 0; coordinate < 10; coordinate++)
            {
                FrontPose pose = Pose();
                if (coordinate < 3) pose.LeftWristMm[coordinate] = invalid;
                else if (coordinate < 6) pose.RightWristMm[coordinate - 3] = invalid;
                else if (coordinate < 8) pose.LeftPixel[coordinate - 6] = invalid;
                else pose.RightPixel[coordinate - 8] = invalid;
                HandLineResult result = Evaluate(pose);
                Assert.That(result.Valid, Is.False, "Coordinate " + coordinate);
                Assert.That(result.Problem, Is.EqualTo(FrontPoseProblem.InvalidPosition), "Coordinate " + coordinate);
            }
        }

        [TestCase(-1f, 3500f)]
        [TestCase(1000f, 999f)]
        [TestCase(float.NaN, 3500f)]
        [TestCase(500f, float.NaN)]
        [TestCase(500f, float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity, 3500f)]
        public void InvalidDistanceSettingsAreProgrammingErrors(float minimum, float maximum)
        {
            Assert.Throws<ArgumentException>(() => FrontHandControl.Evaluate(Pose(), minimum, maximum, false, false));
        }

        private static FrontPose Pose() => Pose(new Vector3(-300, 0, 2000), new Vector3(300, 0, 2000));

        private static FrontPose Pose(Vector3 left, Vector3 right) => new FrontPose
        {
            HasBody = true,
            BodyId = 7,
            LeftWristMm = left,
            RightWristMm = right,
            LeftPixel = new Vector2(200, 250),
            RightPixel = new Vector2(440, 250),
            LeftConfidence = 2,
            RightConfidence = 2
        };

        private static HandLineResult Evaluate(FrontPose pose) => FrontHandControl.Evaluate(pose, 500, 3500, false, false);

        private static void AssertValid(HandLineResult result, float expectedAngle)
        {
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Problem, Is.EqualTo(FrontPoseProblem.None));
            Assert.That(result.AngleDegrees, Is.EqualTo(expectedAngle).Within(0.001f));
        }

        private static void AssertProblem(HandLineResult result, FrontPoseProblem expected)
        {
            Assert.That(result.Valid, Is.False);
            Assert.That(result.Problem, Is.EqualTo(expected));
            Assert.That(float.IsNaN(result.AngleDegrees) || float.IsInfinity(result.AngleDegrees), Is.False);
        }
    }
}
