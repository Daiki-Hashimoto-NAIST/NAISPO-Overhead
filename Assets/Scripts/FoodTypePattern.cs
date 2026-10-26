using System;

namespace Naispo.Overhead
{
    // Each four-food bag contains two of each kind. Select only shuffles that
    // cannot extend the previous bag into a run of three identical foods.
    public sealed class FoodTypePattern
    {
        static readonly bool[][] Orders={
            new[]{false,false,true,true},
            new[]{false,true,false,true},
            new[]{false,true,true,false},
            new[]{true,false,false,true},
            new[]{true,false,true,false},
            new[]{true,true,false,false}
        };

        readonly Random random;
        readonly int[] eligible=new int[Orders.Length];
        bool[] bag;
        int nextIndex=4,lastRun;
        bool lastIsFish;

        // Do not share a time-based seed with the lane generator created in the same frame.
        public FoodTypePattern() : this(Guid.NewGuid().GetHashCode()) { }
        public FoodTypePattern(int seed) { random=new Random(seed); }

        public bool NextIsFish()
        {
            if(nextIndex==4)Refill();
            bool isFish=bag[nextIndex++];
            lastRun=lastRun>0&&isFish==lastIsFish ? lastRun+1 : 1;
            lastIsFish=isFish;
            return isFish;
        }

        public void Reset()
        {
            bag=null;
            nextIndex=4;
            lastRun=0;
            // Keep the random stream moving so replay chooses a fresh sequence.
        }

        void Refill()
        {
            int count=0;
            for(int i=0;i<Orders.Length;i++)
            {
                bool[] order=Orders[i];
                if(lastRun>0&&order[0]==lastIsFish&&(lastRun>=2||order[1]==lastIsFish))
                    continue;
                eligible[count++]=i;
            }
            // At least the three shuffles starting with the other kind are always valid.
            bag=Orders[eligible[random.Next(count)]];
            nextIndex=0;
        }
    }
}
