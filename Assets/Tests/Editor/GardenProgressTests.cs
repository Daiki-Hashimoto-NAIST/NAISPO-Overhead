using System;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class GardenProgressTests
    {
        [Test]
        public void NewGardenHasNoLightsAndNoFinalArrival()
        {
            var garden = new GardenProgress();

            Assert.That(garden.DeliveredCount, Is.Zero);
            Assert.That(garden.LightCount, Is.Zero);
            Assert.That(garden.IsComplete, Is.False);
            Assert.That(garden.FinalArrivalAt, Is.EqualTo(float.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightAt(0));
        }

        [Test]
        public void EverySuccessRecordsItsOwnTimeSideAndSourceUntilTwenty()
        {
            var garden = new GardenProgress();

            for (int i = 1; i <= GardenProgress.Goal; i++)
            {
                bool right = i % 2 == 0;
                Vector2 source = new Vector2(300 + i, 600 + i * 2);
                Assert.That(garden.Deliver(i * 1.5f, right, source), Is.True, "delivery " + i);
                Assert.That(garden.DeliveredCount, Is.EqualTo(i));
                Assert.That(garden.LightCount, Is.EqualTo(i));
                Assert.That(garden.LightAt(i - 1).Time, Is.EqualTo(i * 1.5f));
                Assert.That(garden.LightIsRight(i - 1), Is.EqualTo(right));
                Assert.That(garden.LightSource(i - 1), Is.EqualTo(source));
                Assert.That(garden.LightTime(i - 1), Is.EqualTo(i * 1.5f));
                Assert.That(garden.IsComplete, Is.EqualTo(i == GardenProgress.Goal));
                Assert.That(garden.FinalArrivalAt,
                    Is.EqualTo(i == GardenProgress.Goal
                        ? GardenProgress.Goal * 1.5f + GardenProgress.LightFlightDuration
                        : float.NegativeInfinity));
            }
        }

        [Test]
        public void DeliveriesAfterTheGoalAreRejectedWithoutChangingTheFinalLight()
        {
            var garden = new GardenProgress();
            FillTo(garden, GardenProgress.Goal, 10f);

            float finalArrival = garden.FinalArrivalAt;
            var finalLight = garden.LightAt(GardenProgress.Goal - 1);
            for (int i = 0; i < 8; i++)
                Assert.That(garden.Deliver(1000 + i, i % 2 == 0, new Vector2(i, i)), Is.False);

            Assert.That(garden.DeliveredCount, Is.EqualTo(GardenProgress.Goal));
            Assert.That(garden.LightCount, Is.EqualTo(GardenProgress.Goal));
            Assert.That(garden.IsComplete, Is.True);
            Assert.That(garden.FinalArrivalAt, Is.EqualTo(finalArrival));
            Assert.That(garden.LightAt(GardenProgress.Goal - 1).Time, Is.EqualTo(finalLight.Time));
            Assert.That(garden.LightAt(GardenProgress.Goal - 1).Source, Is.EqualTo(finalLight.Source));
        }

        [Test]
        public void SeveralSuccessesMayShareTheSameFrameTime()
        {
            var garden = new GardenProgress();
            for (int i = 0; i < GardenProgress.Goal; i++)
                Assert.That(garden.Deliver(42f, i % 2 == 0, new Vector2(100 + i, 700)), Is.True);

            Assert.That(garden.IsComplete, Is.True);
            for (int i = 0; i < GardenProgress.Goal; i++)
                Assert.That(garden.LightTime(i), Is.EqualTo(42f));
            Assert.That(garden.FinalArrivalAt,
                Is.EqualTo(42f + GardenProgress.LightFlightDuration));
        }

        [Test]
        public void NonFiniteTimeOrSourceIsRejectedBeforeItCanConsumeALight()
        {
            var garden = new GardenProgress();
            Assert.That(garden.Deliver(1f, false, Vector2.zero), Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => garden.Deliver(float.NaN, false, Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.Deliver(float.PositiveInfinity, false, Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.Deliver(float.NegativeInfinity, false, Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                garden.Deliver(2f, false, new Vector2(float.NaN, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                garden.Deliver(2f, false, new Vector2(0, float.PositiveInfinity)));

            Assert.That(garden.DeliveredCount, Is.EqualTo(1));
            Assert.That(garden.Deliver(2f, true, new Vector2(500, 700)), Is.True);
            Assert.That(garden.LightTime(1), Is.EqualTo(2f));
            Assert.That(garden.LightIsRight(1), Is.True);
        }

        [Test]
        public void InvalidLightIndexIsRejected()
        {
            var garden = new GardenProgress();
            Assert.That(garden.Deliver(1f, false, Vector2.zero), Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightAt(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightTime(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightIsRight(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightSource(1));
        }

        [TestCase(1)]
        [TestCase(19)]
        [TestCase(20)]
        public void ResetClearsEveryRecordAndAllowsANewRound(int deliveries)
        {
            var garden = new GardenProgress();
            FillTo(garden, deliveries, 100f);
            garden.Reset();

            Assert.That(garden.DeliveredCount, Is.Zero);
            Assert.That(garden.LightCount, Is.Zero);
            Assert.That(garden.IsComplete, Is.False);
            Assert.That(garden.FinalArrivalAt, Is.EqualTo(float.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.LightAt(0));

            Assert.That(garden.Deliver(7.5f, true, new Vector2(900, 700)), Is.True);
            Assert.That(garden.DeliveredCount, Is.EqualTo(1));
            Assert.That(garden.LightTime(0), Is.EqualTo(7.5f));
            Assert.That(garden.LightIsRight(0), Is.True);
        }

        static void FillTo(GardenProgress garden, int count, float time)
        {
            if (count < 0 || count > GardenProgress.MaxLights)
                throw new ArgumentOutOfRangeException(nameof(count), "Tests may fill at most one goal.");
            while (garden.DeliveredCount < count)
                garden.Deliver(time, false, new Vector2(500, 700));
        }
    }
}
