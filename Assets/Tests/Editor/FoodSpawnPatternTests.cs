using NUnit.Framework;

namespace Naispo.Overhead.Tests
{
    public sealed class FoodSpawnPatternTests
    {
        [TestCase(1)]
        [TestCase(72)]
        [TestCase(2026)]
        public void EveryThreeFoodsVisitAllLanesWithoutConsecutiveRepeats(int seed)
        {
            var pattern=new FoodSpawnPattern(seed);
            int previous=2;
            for(int group=0;group<100;group++)
            {
                var seen=new bool[3];
                for(int i=0;i<3;i++)
                {
                    float offset=pattern.NextOffset();
                    Assert.That(offset,Is.InRange(-105f,105f));
                    int lane=offset<-45 ? -1 : offset>45 ? 1 : 0;
                    Assert.That(lane,Is.Not.EqualTo(previous),"Repeated lane at group "+group);
                    Assert.That(seen[lane+1],Is.False);
                    Assert.That(offset-lane*90,Is.InRange(-15f,15f));
                    seen[lane+1]=true;previous=lane;
                }
                Assert.That(seen,Is.All.True);
            }
        }

        [Test]
        public void ResetDiscardsAPartiallyUsedSetOfLanes()
        {
            var pattern=new FoodSpawnPattern(5);
            pattern.NextOffset();pattern.Reset();
            var seen=new bool[3];
            for(int i=0;i<3;i++)
            {
                float offset=pattern.NextOffset();
                int lane=offset<-45 ? -1 : offset>45 ? 1 : 0;
                seen[lane+1]=true;
            }
            Assert.That(seen,Is.All.True);
        }
    }
}
