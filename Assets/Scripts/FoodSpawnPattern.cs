using System;

namespace Naispo.Overhead
{
    // Visit each third of the cloud before reshuffling; avoid long runs at one side.
    public sealed class FoodSpawnPattern
    {
        public const float LaneSpacing=90, Jitter=15;
        readonly Random random;
        readonly int[] lanes={-1,0,1};
        int remaining,lastLane=2;

        public FoodSpawnPattern() : this(new Random()) { }
        public FoodSpawnPattern(int seed) : this(new Random(seed)) { }
        FoodSpawnPattern(Random random) { this.random=random; }

        public float NextOffset()
        {
            if(remaining==0)
            {
                for(int i=lanes.Length-1;i>0;i--)
                {
                    int other=random.Next(i+1);
                    int swap=lanes[i];lanes[i]=lanes[other];lanes[other]=swap;
                }
                if(lanes[2]==lastLane)
                {
                    int swap=lanes[2];lanes[2]=lanes[0];lanes[0]=swap;
                }
                remaining=lanes.Length;
            }
            lastLane=lanes[--remaining];
            return lastLane*LaneSpacing+(float)(random.NextDouble()*2-1)*Jitter;
        }

        public void Reset() { remaining=0;lastLane=2; }
    }
}
