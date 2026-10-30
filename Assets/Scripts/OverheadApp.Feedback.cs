using System.Collections.Generic;
using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        const float DeliveryDuration=0.42f;
        readonly List<Delivery> deliveries=new List<Delivery>();
        readonly List<Impact> impacts=new List<Impact>();
        float cloudMood,cloudTarget,cloudReactedAt=-100;
        bool finalSoundPlayed;

        float FinaleStart => garden.IsComplete ? garden.FinalArrivalAt : float.PositiveInfinity;
        bool FinaleStarted => garden.IsComplete && playTime>=FinaleStart;
        bool Celebrating => FinaleStarted && playTime-FinaleStart<GardenAtmosphere.FinaleDuration;

        sealed class Delivery
        {
            public Vector2 Start;
            public bool IsFish,Right,Correct;
            public float StartedAt,Rotation;
        }
        sealed class Impact
        {
            public Vector2 Position;
            public float StartedAt;
        }

        void UpdateFeedback(float dt)
        {
            for(int i=impacts.Count-1;i>=0;i--)
                if(playTime-impacts[i].StartedAt>0.45f)impacts.RemoveAt(i);
            if(phase==GardenPhase.Playing)
            {
                for(int i=deliveries.Count-1;i>=0;i--)
                {
                    var delivery=deliveries[i];
                    if(playTime-delivery.StartedAt<DeliveryDuration)continue;
                    deliveries.RemoveAt(i);
                    (delivery.Right ? rightMonster : leftMonster).React(delivery.Correct,playTime);
                    ReactCloud(delivery.Correct);
                    if(delivery.Correct)
                    {
                        // The light starts at the spirit's actual mouth on the canvas. The
                        // playfield is translated by StageOffsetX, so add that once here.
                        Vector2 source=MonsterMouthPosition(delivery.Right)
                            +new Vector2(GameLayout.StageOffsetX,0);
                        garden.Deliver(playTime,delivery.Right,source);
                        sounds.PlayDelivery(true);
                        if(garden.IsComplete)
                        {
                            // Pending food cannot score past the goal or interrupt the ending.
                            // The light records stay; only leftover gameplay is discarded.
                            tokens.Clear();deliveries.Clear();impacts.Clear();
                            draggingGate=false;
                            phase=GardenPhase.Awakening;
                            break;
                        }
                    }
                    else
                    {
                        misses++;
                        sounds.PlayDelivery(false);
                    }
                }
            }
            UpdateFinale();
            float moodTarget=FinaleStarted ? 1 : playTime-cloudReactedAt<1.8f ? cloudTarget : 0;
            cloudMood=Mathf.Lerp(cloudMood,moodTarget,1-Mathf.Exp(-8*dt));
            UpdateMonsterAttention(dt);
        }

        void UpdateFinale()
        {
            if(!garden.IsComplete)return;
            float arrival=FinaleStart;
            if(!float.IsFinite(arrival))return;
            if(playTime>=arrival&&!finalSoundPlayed)
            {
                // The blessing belongs to the final arrival, never to an interim milestone.
                finalSoundPlayed=true;
                sounds.PlayCelebration();
            }
            if(phase==GardenPhase.Awakening&&playTime>=arrival+GardenAtmosphere.FinaleDuration)
                phase=GardenPhase.Complete;
        }

        void ReactCloud(bool correct)
        {
            cloudTarget=correct ? 1 : -1;
            cloudReactedAt=playTime;
        }

        void ResetFeedback()
        {
            deliveries.Clear();impacts.Clear();
            cloudMood=cloudTarget=0;cloudReactedAt=-100;
            finalSoundPlayed=false;
            if(sounds!=null)sounds.ResetSounds();
        }

        void DrawDropWarning()
        {
            if(phase!=GardenPhase.Playing||!dropSchedule.HasWarning)return;
            var next=dropSchedule.Upcoming;
            float progress=dropSchedule.WarningProgress;
            Vector2 center=new Vector2(PivotX+next.XOffset,250);
            Circle(center,30,cream);
            DrawFood(center,next.IsFish,Mathf.Lerp(32,42,Mathf.SmoothStep(0,1,progress)));
        }

        void DrawAnimatedFood(Token token)
        {
            float elapsed=playTime-token.HitAt;
            Matrix4x4 saved=GUI.matrix;
            if(elapsed>=0&&elapsed<0.45f)
            {
                float squeeze=Mathf.Cos(elapsed*26)*Mathf.Exp(-elapsed*9)*0.24f;
                Vector3 center=new Vector3(token.Position.x,token.Position.y,0);
                GUI.matrix=saved*Matrix4x4.Translate(center)
                    *Matrix4x4.Rotate(Quaternion.Euler(0,0,token.HitAngle))
                    *Matrix4x4.Scale(new Vector3(1+squeeze,1-squeeze,1))
                    *Matrix4x4.Rotate(Quaternion.Euler(0,0,-token.HitAngle))
                    *Matrix4x4.Translate(-center);
            }
            DrawFood(token.Position,token.IsFish,FoodSize,token.Rotation);
            GUI.matrix=saved;
        }

        void DrawFoodFeedback()
        {
            foreach(var impact in impacts)
            {
                float t=Mathf.Clamp01((playTime-impact.StartedAt)/0.45f);
                Sparkle(impact.Position,10*(1-t),Rgb(0xE9BD62,(1-t)*0.65f));
            }
            foreach(var delivery in deliveries)
            {
                float t=Mathf.Clamp01((playTime-delivery.StartedAt)/DeliveryDuration);
                Vector2 mouth=MonsterMouthPosition(delivery.Right);
                Vector2 position=Vector2.Lerp(delivery.Start,mouth,Mathf.SmoothStep(0,1,t));
                position.y-=Mathf.Sin(t*Mathf.PI)*24;
                DrawFood(position,delivery.IsFish,FoodSize*Mathf.Lerp(1,0.16f,t*t),delivery.Rotation*(1-t));
            }
        }

    }
}
