using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class SorterRulesTests
    {
        [TestCase(-89f, 89f, 91f)]
        [TestCase(89f, -89f, -91f)]
        [TestCase(-45f, 315f, 315f)]
        [TestCase(45f, -315f, -315f)]
        public void AxisWrapChoosesNearestEquivalentDirection(float measured, float previous, float expected)
        {
            Assert.That(SorterRules.NearestAxis(measured, previous), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void RepeatedMeasurementsStayContinuousAcrossVertical()
        {
            float[] measured = { 85, 89, -89, -85, -75, -85, -89, 89, 85 };
            float[] expected = { 85, 89, 91, 95, 105, 95, 91, 89, 85 };
            float previous = measured[0];
            for (int i = 0; i < measured.Length; i++)
            {
                previous = SorterRules.NearestAxis(measured[i], previous);
                Assert.That(previous, Is.EqualTo(expected[i]).Within(0.001f), "Sample " + i);
            }
        }

        [TestCase(-65f)]
        [TestCase(-35f)]
        [TestCase(0f)]
        [TestCase(35f)]
        [TestCase(65f)]
        public void FallingTokenCrossesEitherGateTilt(float angle)
        {
            var center = new Vector2(40, 90);
            Assert.That(SorterRules.CrossesGate(center + Vector2.down * 100,
                center + Vector2.up * 100, center, angle, 170, 13), Is.True);
        }

        [TestCase(-35f)]
        [TestCase(0f)]
        [TestCase(35f)]
        public void EquivalentAxisAnglesHaveIdenticalFallingAndRisingCollisionRules(float angle)
        {
            // Coordinates are screen-space: increasing Y is falling, not rising.
            var above = new Vector2(0, -100);
            var below = new Vector2(0, 100);
            for (int turns = -2; turns <= 2; turns++)
            {
                float equivalent = angle + 180 * turns;
                Assert.That(SorterRules.CrossesGate(above, below, Vector2.zero, equivalent, 170, 13),
                    Is.True, "Falling token at " + equivalent);
                Assert.That(SorterRules.CrossesGate(below, above, Vector2.zero, equivalent, 170, 13),
                    Is.False, "Rising token at " + equivalent);
            }
        }

        [TestCase(-35f)]
        [TestCase(0f)]
        [TestCase(35f)]
        public void CrossingOutsideGateSpanIsRejected(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            var tangent = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var normal = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 beyondEnd = tangent * (side * 185);
                Assert.That(SorterRules.CrossesGate(beyondEnd + normal * 40,
                    beyondEnd - normal * 40, Vector2.zero, angle, 170, 13), Is.False);
            }
        }

        [Test]
        public void RemainingAboveGateOrNotMovingCannotCreateAHit()
        {
            Assert.That(SorterRules.CrossesGate(new Vector2(0, -100), new Vector2(0, -30),
                Vector2.zero, 0, 170, 13), Is.False);
            Assert.That(SorterRules.CrossesGate(new Vector2(0, -13), new Vector2(0, -13),
                Vector2.zero, 0, 170, 13), Is.False);
        }

        [TestCase(-65f, -1)]
        [TestCase(0f, 0)]
        [TestCase(65f, 1)]
        [TestCase(115f, -1)]
        [TestCase(-115f, 1)]
        public void LaunchMovesTowardTheDownhillBinAndInitiallyUpward(float angle, int horizontalSign)
        {
            Vector2 velocity = SorterRules.LaunchVelocity(angle);
            Assert.That(velocity.y, Is.LessThan(0));
            if (horizontalSign == 0) Assert.That(velocity.x, Is.EqualTo(0).Within(0.001f));
            else Assert.That(velocity.x * horizontalSign, Is.GreaterThan(0));
        }

        [TestCase(-65f)]
        [TestCase(0f)]
        [TestCase(35f)]
        public void EquivalentAxisAnglesHaveIdenticalLaunchVelocity(float angle)
        {
            Vector2 expected = SorterRules.LaunchVelocity(angle);
            for (int turns = -2; turns <= 2; turns++)
            {
                Vector2 actual = SorterRules.LaunchVelocity(angle + 180 * turns);
                Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.001f));
            }
        }
    }
}
