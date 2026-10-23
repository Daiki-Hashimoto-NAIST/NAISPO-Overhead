using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        const float MonsterWidth=248,MonsterFeetY=730;

        struct MonsterVisualPose
        {
            public Matrix4x4 Transform;
            public float Height,Jump,Elapsed,GroundX;
            public int Expression;
            public bool Celebrating;
        }

        void UpdateMonsterAttention(float dt)
        {
            UpdateMonsterAttention(leftMonster,false,165,dt);
            UpdateMonsterAttention(rightMonster,true,845,dt);
        }

        void UpdateMonsterAttention(Monster monster,bool right,float centerX,float dt)
        {
            float attention=0,direction=0;
            Vector2 mouth=MonsterMouthPosition(right);
            foreach(var token in tokens)
            {
                Vector2 towards=mouth-token.Position;
                float distance=towards.magnitude;
                if(distance<1||distance>480||token.Position.y>MonsterFeetY+40)continue;
                float approach=Vector2.Dot(token.Velocity,towards/distance);
                if(approach<=12)continue;
                float interest=Mathf.Clamp01(1-distance/480)*Mathf.Clamp01(approach/110)*1.5f;
                if(interest<=attention)continue;
                attention=Mathf.Clamp01(interest);
                direction=Mathf.Clamp((token.Position.x-centerX)/180,-1,1);
            }
            // Food has left the playfield but is still on its way to this mouth.
            // Keep the welcoming pose until the delivery actually arrives.
            foreach(var delivery in deliveries)
            {
                if(delivery.Right!=right)continue;
                attention=1;
                direction=Mathf.Clamp((delivery.Start.x-centerX)/180,-1,1);
                if(Mathf.Abs(direction)<0.2f)direction=right ? -0.55f : 0.55f;
                break;
            }
            float attentionRate=attention>monster.Attention ? 5 : 2.7f;
            float attentionBlend=1-Mathf.Exp(-attentionRate*Mathf.Max(0,dt));
            float lookBlend=1-Mathf.Exp(-(right ? 3.6f : 4.2f)*Mathf.Max(0,dt));
            monster.LookDirection=Mathf.Lerp(monster.LookDirection,direction,lookBlend);
            monster.Attention=Mathf.Lerp(monster.Attention,attention,attentionBlend);
        }

        // Both the sprite and the food delivery use this transform. It is deterministic for
        // playTime, so IMGUI Layout/Repaint and Update cannot advance the animation twice.
        MonsterVisualPose GetMonsterPose(Monster monster,bool right,float centerX)
        {
            float elapsed=playTime-monster.ReactedAt;
            float celebration=playTime-celebrationAt;
            bool dancing=celebration>=0&&celebration<CelebrationDuration;
            float danceEnvelope=dancing ? MonsterHold(celebration,0,0.3f,2.35f,CelebrationDuration) : 0;
            float reactionWeight=dancing ? 1-MonsterEase(celebration,0,0.3f) : 1;
            bool reacting=elapsed>=0&&elapsed<2.25f&&monster.Expression!=0;
            float phase=right ? 1.7f : 0;
            float breath=Mathf.Sin(playTime*(right ? 1.48f : 1.62f)+phase);
            float weightShift=Mathf.Sin(playTime*1.12f+phase);
            float anticipation=Mathf.SmoothStep(0,1,monster.Attention)*(1-danceEnvelope);
            if(reacting)anticipation*=1-0.65f*MonsterHold(elapsed,0,0.3f,1.85f,2.25f);
            float lean=monster.LookDirection*anticipation;
            float sx=1-breath*0.008f-anticipation*0.012f;
            float sy=1+breath*0.012f+anticipation*0.025f;
            float rotation=weightShift*1.2f+lean*4.8f;
            float shift=weightShift*2.2f+lean*7;
            float jump=0;
            int expression=0;
            float side=right ? -1 : 1;

            if(reacting)
            {
                expression=elapsed<0.1f ? 0 : monster.Expression;
                float delay=right ? 0.06f : 0;
                float bite=MonsterPulse(elapsed,0,0.12f,0.32f)*reactionWeight;
                // Two small, eased chews leave room to enjoy the food before reacting.
                float chew=(MonsterPulse(elapsed,0.3f,0.46f,0.65f)
                    +0.8f*MonsterPulse(elapsed,0.64f+delay,0.79f+delay,0.96f+delay))*reactionWeight;
                sx+=bite*0.065f+chew*0.027f;
                sy-=bite*0.075f+chew*0.024f;
                rotation+=side*chew*0.55f;

                if(monster.Expression==1)
                {
                    float time=elapsed-delay;
                    float crouch=MonsterHold(time,0.94f,1.09f,1.16f,1.3f)*reactionWeight;
                    float stretch=MonsterPulse(time,1.16f,1.34f,1.55f)*reactionWeight;
                    float landing=MonsterPulse(time,1.79f,1.9f,2.15f)*reactionWeight;
                    jump=(right ? 18 : 22)*MonsterHold(time,1.18f,1.44f,1.52f,1.8f)*reactionWeight;
                    sx+=crouch*0.045f-stretch*0.03f+landing*0.065f;
                    sy+=-crouch*0.055f+stretch*0.045f-landing*0.07f;
                    rotation+=side*1.8f*MonsterHold(time,1.08f,1.4f,1.7f,2.15f)*reactionWeight;
                }
                else if(monster.Expression==2)
                {
                    // Tilt, think for a moment, then gently return. No repeated shaking.
                    float puzzled=MonsterHold(elapsed,0.75f+delay,1.12f+delay,1.67f,2.22f)*reactionWeight;
                    rotation+=side*(right ? 5 : 5.8f)*puzzled;
                    shift+=side*2*puzzled;
                    sx+=puzzled*0.006f;sy-=puzzled*0.008f;
                }
            }

            if(dancing)
            {
                expression=1;
                float dancePhase=celebration*Mathf.PI*2/1.65f+(right ? Mathf.PI*0.8f : 0);
                float dance=Mathf.Sin(dancePhase);
                float hopTime=celebration-(right ? 0.82f : 0.48f);
                float crouch=MonsterPulse(hopTime,-0.22f,-0.08f,0.08f)*danceEnvelope;
                float landing=MonsterPulse(hopTime,0.56f,0.7f,0.98f)*danceEnvelope;
                float hop=MonsterHold(hopTime,0,0.23f,0.31f,0.59f)*danceEnvelope;
                rotation+=dance*3.8f*danceEnvelope;
                shift+=dance*4.5f*danceEnvelope;
                jump=Mathf.Max(jump,(right ? 11 : 14)*hop);
                sx+=crouch*0.035f+landing*0.04f-hop*0.012f;
                sy-=crouch*0.04f+landing*0.045f-hop*0.018f;
            }
            // Roll around the supporting edge of the feet rather than into the floor.
            float supportLift=Mathf.Abs(Mathf.Sin(rotation*Mathf.Deg2Rad))*MonsterWidth*0.5f*sx;
            return new MonsterVisualPose
            {
                Transform=Matrix4x4.TRS(new Vector3(centerX+shift,MonsterFeetY-jump-supportLift,0),
                    Quaternion.Euler(0,0,rotation),new Vector3(sx,sy,1)),
                Height=MonsterWidth*monster.Crop.height/monster.Crop.width,
                GroundX=centerX+shift,Jump=jump,Elapsed=elapsed,Expression=expression,Celebrating=dancing
            };
        }

        static float MonsterEase(float time,float start,float end) =>
            Mathf.SmoothStep(0,1,Mathf.Clamp01((time-start)/(end-start)));

        static float MonsterPulse(float time,float start,float peak,float end) =>
            MonsterEase(time,start,peak)*(1-MonsterEase(time,peak,end));

        static float MonsterHold(float time,float start,float riseEnd,float fallStart,float end) =>
            MonsterEase(time,start,riseEnd)*(1-MonsterEase(time,fallStart,end));

        // Returns game coordinates, before GameLayout.StageOffsetX and screen scaling.
        Vector2 MonsterMouthPosition(bool right)
        {
            Monster monster=right ? rightMonster : leftMonster;
            MonsterVisualPose pose=GetMonsterPose(monster,right,right ? 845 : 165);
            // These positions were measured on the existing 1280px expression PNGs.
            Vector2 pixel=pose.Expression==1 ? new Vector2(630,530)
                : right ? new Vector2(627,pose.Expression==2 ? 578 : 562) : new Vector2(625,578);
            float scale=MonsterWidth/monster.Crop.width;
            Vector3 local=new Vector3((pixel.x-monster.Crop.center.x)*scale,(pixel.y-monster.Crop.yMax)*scale,0);
            return pose.Transform.MultiplyPoint3x4(local);
        }

        void DrawMonster(Monster monster,float centerX,Color accent,bool isFish)
        {
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                bool right=ReferenceEquals(monster,rightMonster);
                MonsterVisualPose pose=GetMonsterPose(monster,right,centerX);
                Texture2D face=monster.Faces[pose.Expression];
                if(face==null)face=monster.Faces[0];
                if(face==null)return;
                float shadowScale=1-pose.Jump/150;
                Oval(new Rect(pose.GroundX-107*shadowScale,717,214*shadowScale,26),Rgb(0xA99B7D,0.18f-pose.Jump*0.0015f));
                Rect crop=monster.Crop;
                Rect uv=new Rect(crop.x/face.width,1-crop.yMax/face.height,crop.width/face.width,crop.height/face.height);
                GUI.color=Color.white;
                GUI.matrix=originalMatrix*pose.Transform;
                GUI.DrawTextureWithTexCoords(new Rect(-MonsterWidth/2,-pose.Height,MonsterWidth,pose.Height),face,uv,true);
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;

                DrawMonsterEmotion(pose,centerX);
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

    }
}
