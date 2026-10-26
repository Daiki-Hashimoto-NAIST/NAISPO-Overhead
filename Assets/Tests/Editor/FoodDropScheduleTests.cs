using System;
using NUnit.Framework;

namespace Naispo.Overhead.Tests
{
    public sealed class FoodDropScheduleTests
    {
        [Test]
        public void WarningHiddenByCelebrationRestartsWithoutChangingThePlannedFood()
        {
            var schedule=Schedule();
            schedule.Advance(1,0,out _);
            var preview=schedule.Upcoming;
            schedule.Advance(0.6f,0,out _);
            schedule.RestartWarning();
            Assert.That(schedule.WarningProgress,Is.EqualTo(0));
            Assert.That(schedule.Advance(0.64f,0,out _),Is.False);
            Assert.That(schedule.Advance(0.02f,0,out var drop),Is.True);
            AssertSame(drop,preview);
        }

        [Test]
        public void FirstFoodHasAFullWarningAndUsesTheAdvertisedPositionAndType()
        {
            var schedule = Schedule();
            Assert.That(schedule.Advance(0.34f, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.Advance(0.02f, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True);
            ScheduledFoodDrop preview = schedule.Upcoming;
            Assert.That(schedule.WarningProgress, Is.EqualTo(0));
            Assert.That(schedule.Advance(0.64f, 0, out _), Is.False);
            Assert.That(schedule.WarningProgress, Is.InRange(0.98f, 1f));
            Assert.That(schedule.Advance(0.02f, 0, out ScheduledFoodDrop drop), Is.True);
            AssertSame(drop, preview);
            Assert.That(schedule.SpawnedCount, Is.EqualTo(1));
            Assert.That(schedule.HasWarning, Is.False);
        }

        [Test]
        public void LaterFoodsWaitForTheIntervalAndThenReceiveTheirOwnWarning()
        {
            var schedule = Schedule();
            DropNext(schedule);
            Assert.That(schedule.Advance(1.14f, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.Advance(0.02f, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True);
            Assert.That(schedule.Upcoming.Sequence, Is.EqualTo(1));
            ScheduledFoodDrop preview = schedule.Upcoming;
            Assert.That(schedule.Advance(0.64f, 0, out _), Is.False);
            Assert.That(schedule.Advance(0.02f, 0, out ScheduledFoodDrop drop), Is.True);
            Assert.That(drop.Sequence, Is.EqualTo(1));
            AssertSame(drop, preview);
        }

        [Test]
        public void LongFrameCannotSkipTheFirstWarningOrProduceCatchUpDrops()
        {
            var schedule = Schedule();
            Assert.That(schedule.Advance(100, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True);
            Assert.That(schedule.SpawnedCount, Is.EqualTo(0));
            Assert.That(schedule.WarningProgress, Is.EqualTo(0));
            Assert.That(schedule.Advance(100, 0, out ScheduledFoodDrop first), Is.True);
            Assert.That(first.Sequence, Is.EqualTo(0));
            Assert.That(schedule.SpawnedCount, Is.EqualTo(1));
            Assert.That(schedule.Advance(0, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.Advance(100, 0, out _), Is.False, "The next warning must be shown before its drop");
            Assert.That(schedule.SpawnedCount, Is.EqualTo(1));
        }

        [TestCase(7)]
        [TestCase(12)]
        public void FullCapacityDoesNotConsumeThePendingDropOrAdvanceTheInitialDelay(int activeCount)
        {
            var schedule = Schedule();
            ScheduledFoodDrop preview = schedule.Upcoming;
            Assert.That(schedule.Advance(100, activeCount, out _), Is.False);
            Assert.That(schedule.Advance(100, activeCount, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.SpawnedCount, Is.EqualTo(0));
            AssertSame(schedule.Upcoming, preview);
            Assert.That(schedule.Advance(100, 6, out _), Is.False, "Reopening a slot cannot cause an instant drop");
            Assert.That(schedule.Advance(0.34f, 6, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.Advance(0.02f, 6, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True);
            Assert.That(schedule.Advance(0.64f, 6, out _), Is.False);
            Assert.That(schedule.Advance(0.02f, 6, out ScheduledFoodDrop drop), Is.True);
            AssertSame(drop, preview);
        }

        [Test]
        public void CapacityInterruptingTheLastMomentOfAWarningRestartsThatSameWarning()
        {
            var schedule = Schedule();
            schedule.Advance(1, 0, out _);
            ScheduledFoodDrop preview = schedule.Upcoming;
            Assert.That(schedule.Advance(0.6f, 0, out _), Is.False);
            Assert.That(schedule.WarningProgress, Is.GreaterThan(0.9f));
            Assert.That(schedule.Advance(100, 7, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.WarningProgress, Is.EqualTo(0));
            Assert.That(schedule.Advance(100, 7, out _), Is.False);
            Assert.That(schedule.Advance(100, 6, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True);
            Assert.That(schedule.WarningProgress, Is.EqualTo(0));
            Assert.That(schedule.Advance(0.64f, 6, out _), Is.False);
            Assert.That(schedule.Advance(0.02f, 6, out ScheduledFoodDrop drop), Is.True);
            AssertSame(drop, preview);
        }

        [Test]
        public void ReadingAWarningDoesNotAdvanceTimeOrRerollItsPositionOrType()
        {
            var schedule = Schedule();
            schedule.Advance(1, 0, out _);
            schedule.Advance(0.3f, 0, out _);
            ScheduledFoodDrop preview = schedule.Upcoming;
            float progress = schedule.WarningProgress;
            for (int i = 0; i < 100; i++)
            {
                Assert.That(schedule.HasWarning, Is.True);
                Assert.That(schedule.WarningProgress, Is.EqualTo(progress));
                AssertSame(schedule.Upcoming, preview);
            }
            Assert.That(schedule.Advance(0, 0, out _), Is.False);
            Assert.That(schedule.WarningProgress, Is.EqualTo(progress));
        }

        [Test]
        public void ResetDiscardsTheWarningAndRestartsWithANewAdvertisedFood()
        {
            var schedule = Schedule();
            DropNext(schedule);
            schedule.Advance(2, 0, out _);
            schedule.Advance(0.6f, 0, out _);
            schedule.Reset();
            ScheduledFoodDrop preview = schedule.Upcoming;
            Assert.That(schedule.SpawnedCount, Is.EqualTo(0));
            Assert.That(schedule.Upcoming.Sequence, Is.EqualTo(0));
            Assert.That(schedule.HasWarning, Is.False);
            Assert.That(schedule.WarningProgress, Is.EqualTo(0));
            Assert.That(schedule.Advance(1, 0, out _), Is.False);
            Assert.That(schedule.Advance(0.64f, 0, out _), Is.False);
            Assert.That(schedule.Advance(0.02f, 0, out ScheduledFoodDrop drop), Is.True);
            Assert.That(drop.Sequence, Is.EqualTo(0));
            AssertSame(drop, preview);
        }

        [Test]
        public void ResetAlsoClearsCapacityBlocking()
        {
            var schedule = Schedule();
            schedule.Advance(100, 7, out _);
            schedule.Reset();
            Assert.That(schedule.Advance(1, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True, "There is no old blocked-frame delay after reset");
        }

        [Test]
        public void SuppliedPositionPatternRetainsThreeLaneCoverage()
        {
            var schedule = Schedule();
            for (int group = 0; group < 4; group++)
            {
                var lanes = new bool[3];
                for (int i = 0; i < 3; i++)
                {
                    ScheduledFoodDrop drop = DropNext(schedule);
                    int sequence = group * 3 + i;
                    Assert.That(drop.Sequence, Is.EqualTo(sequence));
                    Assert.That(drop.XOffset, Is.InRange(-105f, 105f));
                    int lane = drop.XOffset < -45 ? 0 : drop.XOffset > 45 ? 2 : 1;
                    Assert.That(lanes[lane], Is.False);
                    lanes[lane] = true;
                }
                Assert.That(lanes, Is.All.True);
            }
        }

        [Test]
        public void FoodTypesAndDropPositionsUseIndependentRandomSequences()
        {
            var baseline = new FoodDropSchedule(new FoodSpawnPattern(72), typePattern: new FoodTypePattern(42));
            var differentTypes = new FoodDropSchedule(new FoodSpawnPattern(72), typePattern: new FoodTypePattern(9));
            var differentPositions = new FoodDropSchedule(new FoodSpawnPattern(9), typePattern: new FoodTypePattern(42));
            bool sawDifferentType = false, sawDifferentPosition = false;
            for (int i = 0; i < 48; i++)
            {
                ScheduledFoodDrop expected = DropNext(baseline);
                ScheduledFoodDrop changedType = DropNext(differentTypes);
                ScheduledFoodDrop changedPosition = DropNext(differentPositions);
                Assert.That(changedType.XOffset, Is.EqualTo(expected.XOffset));
                Assert.That(changedPosition.IsFish, Is.EqualTo(expected.IsFish));
                sawDifferentType |= changedType.IsFish != expected.IsFish;
                sawDifferentPosition |= changedPosition.XOffset != expected.XOffset;
            }
            Assert.That(sawDifferentType, Is.True);
            Assert.That(sawDifferentPosition, Is.True);
        }

        [Test]
        public void WarningReadsRestartsAndCapacityWaitsDoNotConsumeFutureFoods()
        {
            var interrupted = Schedule();
            var uninterrupted = Schedule();
            for (int i = 0; i < 16; i++)
            {
                Assert.That(interrupted.Advance(2, 0, out _), Is.False);
                ScheduledFoodDrop preview = interrupted.Upcoming;
                for (int read = 0; read < 5; read++) AssertSame(interrupted.Upcoming, preview);
                Assert.That(interrupted.Advance(0.3f, 0, out _), Is.False);
                interrupted.RestartWarning();
                Assert.That(interrupted.Advance(100, 7, out _), Is.False);
                Assert.That(interrupted.Advance(100, 7, out _), Is.False);
                Assert.That(interrupted.Advance(0, 0, out _), Is.False);
                Assert.That(interrupted.Advance(0.65f, 0, out ScheduledFoodDrop drop), Is.True);
                AssertSame(drop, preview);
                AssertSame(drop, DropNext(uninterrupted));
            }
        }

        [Test]
        public void ShortFirstDelayStillGivesTheWholeConfiguredWarning()
        {
            var schedule = new FoodDropSchedule(new FoodSpawnPattern(2), firstDropDelaySeconds: 0);
            Assert.That(schedule.Advance(0, 0, out _), Is.False);
            Assert.That(schedule.HasWarning, Is.True);
            Assert.That(schedule.Advance(0.64f, 0, out _), Is.False);
            Assert.That(schedule.Advance(0.02f, 0, out _), Is.True);
        }

        [Test]
        public void ConfiguredCapacityAndTimingAreApplied()
        {
            var schedule = new FoodDropSchedule(new FoodSpawnPattern(2), intervalSeconds: 1,
                warningSeconds: 0.25f, firstDropDelaySeconds: 0, capacity: 2);
            Assert.That(schedule.Advance(0, 1, out _), Is.False);
            Assert.That(schedule.Advance(0.2f, 1, out _), Is.False);
            Assert.That(schedule.Advance(0.1f, 2, out _), Is.False);
            Assert.That(schedule.Advance(0.1f, 1, out _), Is.False);
            Assert.That(schedule.Advance(0.24f, 1, out _), Is.False);
            Assert.That(schedule.Advance(0.02f, 1, out _), Is.True);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidElapsedTimeIsRejected(float dt)
        {
            Assert.Throws<ArgumentException>(() => Schedule().Advance(dt, 0, out _));
        }

        [Test]
        public void NegativeActiveCountIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Schedule().Advance(0.1f, -1, out _));
        }

        [TestCase(0.5f, 0.65f, 1f, 7)]
        [TestCase(1.8f, 0f, 1f, 7)]
        [TestCase(1.8f, 0.65f, -1f, 7)]
        [TestCase(1.8f, 0.65f, 1f, 0)]
        [TestCase(float.NaN, 0.65f, 1f, 7)]
        [TestCase(1.8f, float.PositiveInfinity, 1f, 7)]
        public void InvalidConfigurationIsRejected(float interval, float warning, float first, int capacity)
        {
            Assert.Throws<ArgumentException>(() => new FoodDropSchedule(null, interval, warning, first, capacity));
        }

        static FoodDropSchedule Schedule() => new FoodDropSchedule(new FoodSpawnPattern(72), typePattern: new FoodTypePattern(42));

        static ScheduledFoodDrop DropNext(FoodDropSchedule schedule)
        {
            Assert.That(schedule.Advance(2, 0, out _), Is.False);
            ScheduledFoodDrop preview = schedule.Upcoming;
            Assert.That(schedule.Advance(0.65f, 0, out ScheduledFoodDrop drop), Is.True);
            AssertSame(drop, preview);
            return drop;
        }

        static void AssertSame(ScheduledFoodDrop actual, ScheduledFoodDrop expected)
        {
            Assert.That(actual.Sequence, Is.EqualTo(expected.Sequence));
            Assert.That(actual.XOffset, Is.EqualTo(expected.XOffset));
            Assert.That(actual.IsFish, Is.EqualTo(expected.IsFish));
        }
    }
}
