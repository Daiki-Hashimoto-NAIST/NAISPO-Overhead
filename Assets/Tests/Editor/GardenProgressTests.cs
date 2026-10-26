using System;
using NUnit.Framework;

namespace Naispo.Overhead.Tests
{
    public sealed class GardenProgressTests
    {
        [Test]
        public void NewGardenHasNoDeliveriesOrBloomTimes()
        {
            AssertEmpty(new GardenProgress());
        }

        [TestCase(9, 0, false, false)]
        [TestCase(10, 1, true, false)]
        [TestCase(19, 1, false, false)]
        [TestCase(20, 2, true, false)]
        [TestCase(29, 2, false, false)]
        [TestCase(30, 3, true, true)]
        public void EachTenDeliveriesAddsOneBloomUntilThirty(int deliveries, int blooms,
            bool lastDeliveryBlooms, bool complete)
        {
            var garden = new GardenProgress();
            int bloomEvents = 0;
            bool lastResult = false;
            for (int i = 1; i <= deliveries; i++)
            {
                lastResult = garden.Deliver(i * 2f);
                if (lastResult) bloomEvents++;
            }

            Assert.That(garden.DeliveredCount, Is.EqualTo(deliveries));
            Assert.That(garden.BloomCount, Is.EqualTo(blooms));
            Assert.That(bloomEvents, Is.EqualTo(blooms));
            Assert.That(lastResult, Is.EqualTo(lastDeliveryBlooms));
            Assert.That(garden.IsComplete, Is.EqualTo(complete));
        }

        [Test]
        public void OnlyMilestoneDeliveriesRecordTheirSuppliedGameTime()
        {
            var garden = new GardenProgress();
            for (int i = 0; i < 9; i++) garden.Deliver(0);
            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(float.NegativeInfinity));
            Assert.That(garden.Deliver(12.5f), Is.True);
            for (int i = 0; i < 9; i++) garden.Deliver(40);
            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(12.5f));
            Assert.That(garden.BloomStartedAt(1), Is.EqualTo(float.NegativeInfinity));

            Assert.That(garden.Deliver(61.25f), Is.True);
            for (int i = 0; i < 9; i++) garden.Deliver(90);
            Assert.That(garden.BloomStartedAt(2), Is.EqualTo(float.NegativeInfinity));
            Assert.That(garden.Deliver(135.75f), Is.True);

            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(12.5f));
            Assert.That(garden.BloomStartedAt(1), Is.EqualTo(61.25f));
            Assert.That(garden.BloomStartedAt(2), Is.EqualTo(135.75f));
        }

        [Test]
        public void ExtraDeliveriesAfterCompletionCannotAddBloomsOrRestartTheirAnimations()
        {
            var garden = new GardenProgress();
            for (int i = 1; i <= 30; i++) garden.Deliver(i);
            for (int i = 0; i < 50; i++)
                Assert.That(garden.Deliver(1000 + i), Is.False);

            Assert.That(garden.DeliveredCount, Is.EqualTo(30));
            Assert.That(garden.BloomCount, Is.EqualTo(3));
            Assert.That(garden.IsComplete, Is.True);
            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(10));
            Assert.That(garden.BloomStartedAt(1), Is.EqualTo(20));
            Assert.That(garden.BloomStartedAt(2), Is.EqualTo(30));
        }

        [Test]
        public void MultipleDeliveriesMayShareTheSameGameTime()
        {
            var garden = new GardenProgress();
            for (int i = 0; i < 30; i++) garden.Deliver(0);

            Assert.That(garden.IsComplete, Is.True);
            for (int stage = 0; stage < 3; stage++)
                Assert.That(garden.BloomStartedAt(stage), Is.EqualTo(0));
        }

        [TestCase(9)]
        [TestCase(20)]
        [TestCase(30)]
        public void ResetClearsProgressAndAllowsANewRoundWithNewBloomTimes(int deliveries)
        {
            var garden = new GardenProgress();
            for (int i = 0; i < deliveries; i++) garden.Deliver(100 + i);
            garden.Reset();
            AssertEmpty(garden);

            for (int i = 0; i < 9; i++) Assert.That(garden.Deliver(2), Is.False);
            Assert.That(garden.Deliver(7.5f), Is.True);
            Assert.That(garden.DeliveredCount, Is.EqualTo(10));
            Assert.That(garden.BloomCount, Is.EqualTo(1));
            Assert.That(garden.IsComplete, Is.False);
            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(7.5f));
            Assert.That(garden.BloomStartedAt(1), Is.EqualTo(float.NegativeInfinity));
            Assert.That(garden.BloomStartedAt(2), Is.EqualTo(float.NegativeInfinity));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void InvalidStageIndexIsRejected(int stage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GardenProgress().BloomStartedAt(stage));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTimeCannotConsumeTheNextBloom(float playTime)
        {
            var garden = new GardenProgress();
            for (int i = 0; i < 9; i++) garden.Deliver(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => garden.Deliver(playTime));
            Assert.That(garden.DeliveredCount, Is.EqualTo(9));
            Assert.That(garden.BloomCount, Is.EqualTo(0));
            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(float.NegativeInfinity));
            Assert.That(garden.Deliver(2), Is.True);
            Assert.That(garden.BloomStartedAt(0), Is.EqualTo(2));
        }

        static void AssertEmpty(GardenProgress garden)
        {
            Assert.That(garden.DeliveredCount, Is.EqualTo(0));
            Assert.That(garden.BloomCount, Is.EqualTo(0));
            Assert.That(garden.IsComplete, Is.False);
            for (int stage = 0; stage < 3; stage++)
                Assert.That(garden.BloomStartedAt(stage), Is.EqualTo(float.NegativeInfinity));
        }
    }
}
