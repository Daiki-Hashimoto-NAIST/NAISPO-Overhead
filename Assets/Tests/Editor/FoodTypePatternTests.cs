using NUnit.Framework;

namespace Naispo.Overhead.Tests
{
    public sealed class FoodTypePatternTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(72)]
        [TestCase(2026)]
        [TestCase(int.MaxValue)]
        public void EveryFourFoodsContainTwoOfEachAndNeverRepeatThreeAcrossGroups(int seed)
        {
            var pattern = new FoodTypePattern(seed);
            bool previous = false;
            int runLength = 0;
            for (int group = 0; group < 250; group++)
            {
                int fishCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    bool isFish = pattern.NextIsFish();
                    if (isFish) fishCount++;
                    runLength = runLength > 0 && isFish == previous ? runLength + 1 : 1;
                    Assert.That(runLength, Is.LessThanOrEqualTo(2), "Three repeated foods at group " + group);
                    previous = isFish;
                }
                Assert.That(fishCount, Is.EqualTo(2), "Unbalanced group " + group);
            }
        }

        [Test]
        public void BothTypesCanStartAndTheOrderIsNotAlwaysAlternating()
        {
            bool sawFishFirst = false, sawStrawberryFirst = false, sawSameTypeTwice = false;
            for (int seed = 0; seed < 64; seed++)
            {
                var pattern = new FoodTypePattern(seed);
                bool previous = pattern.NextIsFish();
                sawFishFirst |= previous;
                sawStrawberryFirst |= !previous;
                for (int i = 1; i < 16; i++)
                {
                    bool current = pattern.NextIsFish();
                    sawSameTypeTwice |= current == previous;
                    previous = current;
                }
            }
            Assert.That(sawFishFirst, Is.True);
            Assert.That(sawStrawberryFirst, Is.True);
            Assert.That(sawSameTypeTwice, Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ResetDiscardsThePartialGroupAndItsRepeatHistory(int consumed)
        {
            for (int seed = 0; seed < 32; seed++)
            {
                var partial = new FoodTypePattern(seed);
                var completed = new FoodTypePattern(seed);
                for (int i = 0; i < consumed; i++) partial.NextIsFish();
                for (int i = 0; i < 4; i++) completed.NextIsFish();
                partial.Reset();
                completed.Reset();

                // Both selected one group before reset. Its consumed length and final type
                // must not affect a new round or carry a partial group into that round.
                for (int group = 0; group < 4; group++)
                {
                    int fishCount = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        bool isFish = partial.NextIsFish();
                        Assert.That(isFish, Is.EqualTo(completed.NextIsFish()), "Seed " + seed);
                        if (isFish) fishCount++;
                    }
                    Assert.That(fishCount, Is.EqualTo(2));
                }
            }
        }

        [Test]
        public void ResetDoesNotReplayTheSeededOpeningEveryRound()
        {
            bool sawChangedOpening = false;
            for (int seed = 0; seed < 32; seed++)
            {
                var pattern = new FoodTypePattern(seed);
                var opening = new bool[4];
                for (int i = 0; i < opening.Length; i++) opening[i] = pattern.NextIsFish();
                pattern.Reset();
                for (int i = 0; i < opening.Length; i++)
                    sawChangedOpening |= pattern.NextIsFish() != opening[i];
            }
            Assert.That(sawChangedOpening, Is.True, "Reset must keep advancing the random sequence.");
        }
    }
}
