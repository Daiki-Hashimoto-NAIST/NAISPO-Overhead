using System;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class GateCollisionTests
    {
        [Test]
        public void FastFallReturnsTheFirstContactWithTheVisibleTopSurface()
        {
            Vector2 pivot = new Vector2(505, 530);
            Assert.That(GateCollision.SweepCircle(new Vector2(505, 100), new Vector2(505, 900),
                pivot, 0, 0, 170, 28, 11, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.EqualTo(391f / 800).Within(0.00001f));
            AssertPoint(hit.Point, 505, 519);
            AssertPoint(hit.Normal, 0, -1);
            AssertPoint(hit.Center, 505, 490.95f);
            Assert.That(hit.StartedOverlapping, Is.False);
        }

        [Test]
        public void RotationCanHitAStationaryObjectEvenWhenBothFrameEndsAreClear()
        {
            Vector2 food = new Vector2(100, -50);
            Assert.That(Sweep(food, food, 0, -45, out GateHit hit), Is.True);
            Assert.That(hit.AngleDegrees, Is.EqualTo(-16.26f).Within(0.02f));
            Assert.That(hit.Time, Is.InRange(0.36f, 0.37f));
            Assert.That(hit.Normal.y, Is.LessThan(0));
            Assert.That(hit.StartedOverlapping, Is.False);
        }

        [Test]
        public void FallingObjectIsCaughtWhenRotationPutsItsOldPositionBehindTheNewGate()
        {
            // At -45 degrees even the old position is behind the gate. The old current-angle-only
            // test rejected this motion before checking the crossing that occurs earlier in time.
            Assert.That(Sweep(new Vector2(100, -50), new Vector2(100, -35), 0, -45, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.InRange(0.2f, 0.5f));
        }

        [Test]
        public void RotatingIntoAnInitiallyTouchingObjectCountsEvenWithZeroInitialNormalSpeed()
        {
            var food = new Vector2(0, -20);
            Assert.That(Sweep(food, food, 0, 45, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.LessThan(0.001f));
            Assert.That(hit.Normal.y, Is.LessThan(0));
        }

        [TestCase(89f, 91f)]
        [TestCase(89f, -89f)]
        [TestCase(269f, 271f)]
        public void VerticalSideSwitchDoesNotInventAnImpact(float before, float after)
        {
            Assert.That(Sweep(new Vector2(100, 0), new Vector2(100, 0), before, after, out _), Is.False);
        }

        [TestCase(-360f)]
        [TestCase(-180f)]
        [TestCase(180f)]
        [TestCase(360f)]
        public void EquivalentGateAnglesPreserveTimeNormalAndContact(float turns)
        {
            Vector2 from = new Vector2(100, -50), to = new Vector2(100, -35);
            Assert.That(Sweep(from, to, 0, -45, out GateHit expected), Is.True);
            Assert.That(Sweep(from, to, turns, -45 + turns, out GateHit actual), Is.True);
            Assert.That(actual.Time, Is.EqualTo(expected.Time).Within(0.00001f));
            AssertPoint(actual.Point, expected.Point.x, expected.Point.y);
            AssertPoint(actual.Normal, expected.Normal.x, expected.Normal.y);
        }

        [Test]
        public void EquivalentEndAngleDoesNotCreateAFullTurn()
        {
            Assert.That(Sweep(new Vector2(0, -100), new Vector2(0, 100), 0, 180, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(hit.AngleDegrees, Is.EqualTo(0).Within(0.001f));
        }

        [Test]
        public void RoundedEndpointReturnsItsRadialContactNormal()
        {
            Assert.That(GateCollision.SweepCircle(new Vector2(112, -30), new Vector2(112, 0),
                Vector2.zero, 0, 0, 100, 20, 0, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.EqualTo(14f / 30).Within(0.00001f));
            AssertPoint(hit.Point, 100, 0);
            AssertPoint(hit.Normal, 0.6f, -0.8f);
        }

        [Test]
        public void HorizontalEntryCanHitTheUpperHalfOfAnEndpoint()
        {
            Assert.That(GateCollision.SweepCircle(new Vector2(150, -10), new Vector2(0, -10),
                Vector2.zero, 0, 0, 100, 20, 0, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.EqualTo((150 - 117.3205f) / 150).Within(0.00001f));
            AssertPoint(hit.Normal, 0.8660254f, -0.5f);
        }

        [TestCase(-65f, -105f)]
        [TestCase(-65f, 105f)]
        [TestCase(65f, -105f)]
        [TestCase(65f, 105f)]
        public void FallingFoodCanLandOnTheTopOfEitherRoundTipAtMaximumTilt(float angle, float x)
        {
            Assert.That(GateCollision.SweepCircle(new Vector2(x, -300), new Vector2(x, 300),
                Vector2.zero, angle, angle, 170, 28, 11, out GateHit hit), Is.True);
            Assert.That(hit.Normal.y, Is.LessThan(0));
        }

        [TestCase(119f, -30f, -10f)]
        [TestCase(121f, -50f, 50f)]
        public void RectangleAroundAnEndpointDoesNotCountAsTheRoundedGate(float x, float y0, float y1)
        {
            Assert.That(GateCollision.SweepCircle(new Vector2(x, y0), new Vector2(x, y1),
                Vector2.zero, 0, 0, 100, 20, 0, out _), Is.False);
        }

        [TestCase(0f)]
        [TestCase(35f)]
        [TestCase(-35f)]
        [TestCase(115f)]
        public void RisingFromUnderneathDoesNotBounce(float angle)
        {
            Assert.That(Sweep(new Vector2(0, 100), new Vector2(0, -100), angle, angle, out _), Is.False);
        }

        [TestCase(-100f)]
        [TestCase(-20f)]
        public void StationaryClearOrExactlyTouchingObjectDoesNotKeepBouncing(float y)
        {
            var position = new Vector2(0, y);
            Assert.That(Sweep(position, position, 0, 0, out _), Is.False);
        }

        [Test]
        public void StationaryUpperOverlapIsRecoveredAndTheCorrectedPositionIsClear()
        {
            var position = new Vector2(0, -10);
            Assert.That(Sweep(position, position, 0, 0, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.EqualTo(0));
            Assert.That(hit.StartedOverlapping, Is.True);
            AssertPoint(hit.Center, 0, -20.05f);
            Assert.That(Sweep(hit.Center, hit.Center, 0, 0, out _), Is.False);
        }

        [Test]
        public void FallingUpperOverlapIsRecoveredButUpwardEscapeIsNotRebounced()
        {
            Assert.That(Sweep(new Vector2(0, -10), new Vector2(0, -5), 0, 0, out GateHit hit), Is.True);
            Assert.That(hit.StartedOverlapping, Is.True);
            Assert.That(Sweep(new Vector2(0, -10), new Vector2(0, -30), 0, 0, out _), Is.False);
        }

        [Test]
        public void UnderSideOverlapDoesNotTeleportToTheTop()
        {
            Assert.That(Sweep(new Vector2(0, 10), new Vector2(0, 10), 0, 0, out _), Is.False);
            Assert.That(Sweep(new Vector2(0, 10), new Vector2(0, -30), 0, 0, out _), Is.False);
        }

        [Test]
        public void LowFrameRateSweepFindsAnObjectThatPassesCompletelyThroughTheGate()
        {
            Assert.That(Sweep(new Vector2(0, -5000), new Vector2(0, 5000), 0, 0, out GateHit hit), Is.True);
            Assert.That(hit.Time, Is.EqualTo(0.498f).Within(0.00001f));
        }

        [Test]
        public void InvalidGeometryFailsExplicitly()
        {
            Assert.Throws<ArgumentException>(() => GateCollision.SweepCircle(Vector2.zero, Vector2.zero,
                Vector2.zero, 0, float.NaN, 170, 28, 11, out _));
            Assert.Throws<ArgumentException>(() => GateCollision.SweepCircle(Vector2.zero, Vector2.zero,
                Vector2.zero, 0, 0, 170, 0, 11, out _));
        }

        static bool Sweep(Vector2 from, Vector2 to, float before, float after, out GateHit hit)
            => GateCollision.SweepCircle(from, to, Vector2.zero, before, after, 170, 20, 0, out hit);

        static void AssertPoint(Vector2 actual, float x, float y)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(y).Within(0.001f));
        }
    }
}
