using UnityEngine;

namespace Naispo.Overhead
{
    public static class FoodMotion
    {
        public const float Gravity=170,GateHalfThickness=10;

        public static bool Advance(ref Vector2 position,ref Vector2 velocity,Vector2 pivot,
            float fromAngle,float toAngle,float halfLength,float radius,float dt)
            => Advance(ref position,ref velocity,pivot,fromAngle,toAngle,halfLength,radius,dt,out _);

        public static bool Advance(ref Vector2 position,ref Vector2 velocity,Vector2 pivot,
            float fromAngle,float toAngle,float halfLength,float radius,float dt,out GateHit impact)
        {
            impact=default;
            if(dt<=0)return false;
            // Keep gravity and repeated contacts stable during slow frames or quick turns.
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(dt*120,Mathf.Abs(toAngle-fromAngle))));
            float stepTime=dt/steps;
            bool bounced=false;
            for(int step=0;step<steps;step++)
            {
                float startAngle=Mathf.Lerp(fromAngle,toAngle,(float)step/steps);
                float endAngle=Mathf.Lerp(fromAngle,toAngle,(float)(step+1)/steps);
                velocity.y+=Gravity*stepTime;
                Vector2 next=position+velocity*stepTime;
                if(GateCollision.SweepCircle(position,next,pivot,startAngle,endAngle,
                    halfLength,radius,GateHalfThickness,out var hit))
                {
                    impact=hit;
                    position=hit.Center;
                    velocity=SorterRules.LaunchVelocity(hit.AngleDegrees);
                    // At a round tip the usual downhill launch can point into the paddle.
                    // Keep its sorting direction while giving it enough outward motion to leave.
                    float outwardSpeed=Vector2.Dot(velocity,hit.Normal);
                    if(outwardSpeed<30)velocity+=hit.Normal*(30-outwardSpeed);
                    // The paddle keeps turning for the remainder of this small step.
                    if(GateCollision.SweepCircle(position,position,pivot,endAngle,endAngle,
                        halfLength,radius,GateHalfThickness,out var correction))
                        position=correction.Center;
                    bounced=true;
                }
                else position=next;
            }
            return bounced;
        }
    }
}
