using System;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class FoodDeliveryRulesTests
    {
        static readonly Vector2 Pivot = new Vector2(505, 530);

        [TestCase(-230f, -40f)]
        [TestCase(230f, -40f)]
        [TestCase(-180f, 160f)]
        [TestCase(180f, 160f)]
        public void UntouchedFoodIsNeverCollectedByTheNewRule(float x, float y)
        {
            Assert.That(Side(new Vector2(x, y), new Vector2(0, 100), false), Is.EqualTo(0));
        }

        [TestCase(-50f)]
        [TestCase(0f)]
        public void RisingOrStationaryFoodKeepsItsBounce(float verticalSpeed)
        {
            Assert.That(Side(new Vector2(240, 100), new Vector2(100, verticalSpeed)), Is.EqualTo(0));
        }

        [TestCase(-190f, 30f)]
        [TestCase(190f, 30f)]
        [TestCase(-170f, -80f)]
        [TestCase(170f, -80f)]
        public void FoodWithinThePaddlesFullTurningReachIsNotCollected(float x, float y)
        {
            Assert.That(Side(new Vector2(x, y), new Vector2(0, 100)), Is.EqualTo(0));
        }

        [TestCase(-180f, 1)]
        [TestCase(180f, 2)]
        public void WeakFoodFallingPastAnEndIsCollectedWithoutReachingAMonster(float x, int expected)
        {
            Assert.That(Side(new Vector2(x, 120), new Vector2(0, 0.01f)), Is.EqualTo(expected));
        }

        [TestCase(-230f, -20f, 1)]
        [TestCase(230f, 20f, 2)]
        [TestCase(230f, 0f, 2)]
        public void OutwardFoodBeyondEveryPossibleTipPositionCanBeCollectedAboveThePivot(
            float x, float horizontalSpeed, int expected)
        {
            Assert.That(Side(new Vector2(x, -180), new Vector2(horizontalSpeed, 40)), Is.EqualTo(expected));
        }

        [TestCase(-230f, 200f)]
        [TestCase(230f, -200f)]
        public void FoodOutsideTheTipsButReturningTowardThePaddleIsNotCollected(float x, float horizontalSpeed)
        {
            Assert.That(Side(new Vector2(x, -40), new Vector2(horizontalSpeed, 20)), Is.EqualTo(0));
        }

        [Test]
        public void FoodAboveThePivotCanStillFallBackIntoTheTurningReach()
        {
            Assert.That(Side(new Vector2(160, -180), new Vector2(0, 100)), Is.EqualTo(0));
        }

        [Test]
        public void FoodBelowThePivotIsNotCollectedWhileApproachingTheTurningReach()
        {
            Assert.That(Side(new Vector2(160, 160), new Vector2(-300, 10)), Is.EqualTo(0));
        }

        [TestCase(-28f)]
        [TestCase(-0.0001f)]
        [TestCase(0f)]
        [TestCase(0.0001f)]
        [TestCase(28f)]
        public void CentralFallsDoNotChooseAMonsterFromRoundingNoise(float x)
        {
            Assert.That(Side(new Vector2(x, 340), new Vector2(0, 900)), Is.EqualTo(0));
        }

        [TestCase(-190f, 1)]
        [TestCase(190f, 2)]
        public void LargeFramePassingTheBottomStillRecoversFoodBeforeTheMissFallback(float x, int expected)
        {
            Vector2 position = Pivot + new Vector2(x, 340);
            Assert.That(SorterRules.ExitSide(position), Is.EqualTo(3));
            Assert.That(FoodDeliveryRules.DepartedSide(true, position, new Vector2(0, 900),
                Pivot, 170, 28, FoodMotion.GateHalfThickness), Is.EqualTo(expected));
        }

        [TestCase(208f, 0)]
        [TestCase(209f, 0)]
        [TestCase(210f, 2)]
        public void ClearanceIncludesFoodRadiusAndPaddleThickness(float x, int expected)
        {
            Assert.That(Side(new Vector2(x, 0), new Vector2(1, 1)), Is.EqualTo(expected));
        }

        [TestCase(5f, 105f, 2, 15)]
        [TestCase(5f, 105f, 2, 60)]
        [TestCase(-5f, -105f, 1, 15)]
        [TestCase(-5f, -105f, 1, 60)]
        public void ActualWeakPaddleLaunchIsDeliveredWhereTheOldRuleWouldCountAMiss(
            float angle, float offset, int expected, int fps)
        {
            Vector2 position = Pivot + new Vector2(offset, -280), velocity = new Vector2(0, 85);
            bool touched = false;
            int newSide = 0, oldExit = 0, bounces = 0;
            float deliveredY = float.PositiveInfinity;
            for (int frame = 0; frame < fps * 12; frame++)
            {
                bool hit = FoodMotion.Advance(ref position, ref velocity, Pivot,
                    angle, angle, 170, 28, 1f / fps);
                if (hit) { touched = true; bounces++; }
                int departure = FoodDeliveryRules.DepartedSide(touched, position, velocity,
                    Pivot, 170, 28, FoodMotion.GateHalfThickness);
                if (newSide == 0 && departure != 0) { newSide = departure; deliveredY = position.y; }
                // Keep simulating the old path to reproduce its failure rather than assuming it.
                oldExit = SorterRules.ExitSide(position);
                if (oldExit != 0) break;
            }

            Assert.That(bounces, Is.GreaterThan(0), "The food was actually caught by FoodMotion");
            Assert.That(newSide, Is.EqualTo(expected));
            Assert.That(deliveredY, Is.LessThan(825), "Recovery starts before the old bottom miss");
            Assert.That(oldExit, Is.EqualTo(3), "Without recovery this weak launch falls short of the monster");
        }

        [Test]
        public void ASecondContactDuringAPaddleTurnIsAllowedBeforeDelivery()
        {
            Vector2 position = Pivot + new Vector2(100, -70), velocity = Vector2.zero;
            Assert.That(FoodMotion.Advance(ref position, ref velocity, Pivot,
                0, -45, 170, 28, 0.05f), Is.True);
            Assert.That(FoodDeliveryRules.DepartedSide(true, position, velocity,
                Pivot, 170, 28, FoodMotion.GateHalfThickness), Is.EqualTo(0));

            Assert.That(FoodMotion.Advance(ref position, ref velocity, Pivot,
                -45, -60, 170, 28, 0.05f), Is.True);
            Assert.That(FoodDeliveryRules.DepartedSide(true, position, velocity,
                Pivot, 170, 28, FoodMotion.GateHalfThickness), Is.EqualTo(0));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidGeometryCannotChooseADeliverySide(float value)
        {
            Assert.Throws<ArgumentException>(() => Side(new Vector2(value, 340), new Vector2(0, 100)));
        }

        static int Side(Vector2 relativePosition, Vector2 velocity, bool touched = true)
            => FoodDeliveryRules.DepartedSide(touched, Pivot + relativePosition, velocity,
                Pivot, 170, 28, FoodMotion.GateHalfThickness);
    }
}
