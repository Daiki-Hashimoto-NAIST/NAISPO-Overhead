using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        // A milestone changes an area of the room, not just one small point of light.
        // All geometry is in canvas space and stays behind the food and characters.
        void DrawRoomAwakening()
        {
            for(int stage=0;stage<2;stage++)
            {
                float light=RoomAwakening.Reveal(garden,stage,playTime,0);
                if(light<=0)continue;
                bool right=stage==1;
                float x=right ? 1254 : 186;
                float pulse=RoomAwakening.ArrivalPulse(garden,stage,playTime);
                Glow(new Rect(x-188,228,376,510),Rgb(0xF4B85D,light*0.32f));
                Glow(new Rect(x-120,310,240,380),Rgb(0xFFD58C,light*0.24f));
                Glow(new Rect(x-216,700,432,178),Rgb(0xF6BE70,light*0.30f));
                // Long, faint reflections make the light feel present on the wooden floor.
                Glow(new Rect(x-78,652,156,236),Rgb(0xF6D29A,light*0.18f));
                DrawPillarLight(right,light);
                if(pulse>0)
                {
                    Vector2 origin=right ? LanternRoom.RightGlass : LanternRoom.LeftGlass;
                    float age=LanternRoom.Evaluate(garden,stage,playTime).Elapsed-LanternRoom.FlightDuration;
                    float radius=Mathf.Lerp(28,136,Mathf.Clamp01(age/1.35f));
                    Arc(origin,radius,radius*1.2f,0,360,36,2,Rgb(0xFFE7A9,pulse*0.35f));
                    Glow(new Rect(origin.x-112,origin.y-140,224,280),Rgb(0xFFD181,pulse*0.22f));
                }
            }
            DrawStarCanopy();
            DrawFloorEnchantment();
        }

        void DrawPillarLight(bool right,float light)
        {
            float x=right ? 1311 : 129;
            Color gold=Rgb(0xF3C777,light*0.32f);
            Capsule(new Vector2(x,350),new Vector2(x,635),1.5f,gold);
            Capsule(new Vector2(x-15,646),new Vector2(x+15,646),2,gold);
            for(int i=0;i<3;i++)
            {
                float y=403+i*87;
                MagicStar(new Vector2(x,y),5.5f,Rgb(0xFFE3A0,light*0.65f));
                Glow(new Rect(x-20,y-30,40,60),Rgb(0xFFD58C,light*0.20f));
            }
        }

        void DrawSatelliteLanterns()
        {
            Color previous=GUI.color;
            try
            {
                for(int stage=0;stage<2;stage++)
                    for(int index=0;index<2;index++)
                    {
                        RoomAwakening.Fixture fixture=RoomAwakening.Satellites[stage*2+index];
                        float light=RoomAwakening.Reveal(garden,stage,playTime,0.16f*(index+1));
                        Vector2 ring=LanternRoom.RingPoint(fixture.Body);
                        Vector2 glass=LanternRoom.GlassPoint(fixture.Body);
                        if(light>0)
                            Glow(new Rect(glass.x-60,glass.y-70,120,140),Rgb(0xFFCC80,0.23f*light));
                        GUI.color=Color.white;
                        Vector2 wall=new Vector2(stage==0 ? 129 : 1311,fixture.Anchor.y);
                        Capsule(wall,fixture.Anchor,1.7f,Rgb(0x9D8056,0.78f));
                        Rounded(new Rect(wall.x-3,wall.y-5,6,10),2,Rgb(0x594735));
                        Capsule(fixture.Anchor,ring,1.4f,Rgb(0x9D8056,0.78f));
                        GUI.color=LanternRoom.Tint(light);
                        DrawLanternBody(fixture.Body);
                    }
            }
            finally { GUI.color=previous; }
        }

        void DrawStarCanopy()
        {
            float reveal=RoomAwakening.Reveal(garden,2,playTime,0);
            if(reveal<=0)return;
            // Two connected constellations spread out from the central lamp. The gap
            // leaves its face and the food warning unobstructed; the HUD stays above.
            for(int side=0;side<2;side++)
            {
                Vector2[] nodes=side==0 ? RoomAwakening.LeftStars : RoomAwakening.RightStars;
                for(int i=0;i<nodes.Length;i++)
                {
                    float light=RoomAwakening.Reveal(garden,2,playTime,i*0.13f);
                    if(light<=0)continue;
                    Vector2 point=nodes[i];
                    if(i>0)
                    {
                        Vector2 start=nodes[i-1];
                        Vector2 end=Vector2.Lerp(start,point,light);
                        Capsule(start,end,1.4f,Rgb(0xF8D58D,light*0.58f));
                    }
                    float twinkle=0.91f+0.09f*Mathf.Sin(playTime*1.1f+i*1.7f+side);
                    float size=(i%2==0 ? 10 : 6)*Mathf.Lerp(0.8f,1,light);
                    Glow(new Rect(point.x-28,point.y-28,56,56),Rgb(0xFFD88E,light*0.28f));
                    MagicStar(point,size,Rgb(0xFFE6AA,light*twinkle));
                    if(i==3)
                    {
                        Vector2 charm=point+new Vector2(0,22);
                        Capsule(point,charm,1,Rgb(0xDDB96D,light*0.5f));
                        MagicStar(charm,7,Rgb(0xFFE5AF,light*0.82f));
                    }
                }
            }
            Glow(new Rect(285,128,350,100),Rgb(0xF5C57B,reveal*0.14f));
            Glow(new Rect(805,128,350,100),Rgb(0xF5C57B,reveal*0.14f));
        }

        void DrawFloorEnchantment()
        {
            float light=RoomAwakening.Reveal(garden,2,playTime,0.35f);
            if(light<=0)return;
            Vector2 center=RoomAwakening.FloorCenter;
            Glow(new Rect(394,785,652,114),Rgb(0xE7C777,0.24f*light));
            // Perspective flattens the seal into the floor. It stays below both food
            // badges, with a single gentle reveal rather than continuous rotation.
            Arc(center,262,39,0,360*light,72,1.8f,Rgb(0xEFCF88,0.52f*light));
            Arc(center,238,31,0,360*light,64,1,Rgb(0xF5DFA7,0.32f*light));
            for(int i=0;i<8;i++)
            {
                if(light<(i+1)/8f)continue;
                float angle=i*Mathf.PI/4;
                Vector2 point=center+new Vector2(Mathf.Cos(angle)*251,Mathf.Sin(angle)*35);
                MagicStar(point,i%2==0 ? 8 : 5,Rgb(0xFFE7A5,0.72f*light));
            }
            MagicStar(center,14,Rgb(0xFFE3A3,0.68f*light));
            Capsule(center-new Vector2(100,0),center-new Vector2(25,0),1,Rgb(0xF1CF86,0.38f*light));
            Capsule(center+new Vector2(25,0),center+new Vector2(100,0),1,Rgb(0xF1CF86,0.38f*light));
        }

        void MagicStar(Vector2 center,float radius,Color color) =>
            DrawIcon(magicStarIcon,center,Vector2.one*radius*2,color);
    }

    // The extra reveal delays use the same deadline evaluator as the main fixtures;
    // no cached unlock flags, animation timers, or state changes during OnGUI.
    public static class RoomAwakening
    {
        public static readonly Vector2 FloorCenter=new Vector2(720,842);
        public static readonly Vector2[] LeftStars={
            new Vector2(605,121),new Vector2(545,144),new Vector2(478,122),
            new Vector2(402,151),new Vector2(321,138)
        };
        public static readonly Vector2[] RightStars=Mirror(LeftStars);
        public static readonly Fixture[] Satellites={
            MakeFixture(158,285,61,new Vector2(158,234)),
            MakeFixture(248,322,48,new Vector2(248,264)),
            MakeFixture(1282,285,61,new Vector2(1282,234)),
            MakeFixture(1192,322,48,new Vector2(1192,264))
        };

        public static float Reveal(GardenProgress progress,int stage,float playTime,float delay)
        {
            if(!LanternRoom.IsFinite(delay)||delay<0)return 0;
            return LanternRoom.Evaluate(progress,stage,playTime-delay).Brightness;
        }

        public static float ArrivalPulse(GardenProgress progress,int stage,float playTime)
        {
            var light=LanternRoom.Evaluate(progress,stage,playTime);
            if(!light.Arrived)return 0;
            float elapsed=Mathf.Max(0,light.Elapsed-LanternRoom.FlightDuration);
            if(elapsed>=1.35f)return 0;
            return Mathf.Sin(elapsed/1.35f*Mathf.PI);
        }

        static Fixture MakeFixture(float x,float y,float height,Vector2 anchor)
        {
            float scale=height/LanternRoom.Crop.height;
            return new Fixture(new Rect(x-(LanternRoom.SourceGlass.x-LanternRoom.Crop.x)*scale,
                y-(LanternRoom.SourceGlass.y-LanternRoom.Crop.y)*scale,
                LanternRoom.Crop.width*scale,height),anchor);
        }

        static Vector2[] Mirror(Vector2[] points)
        {
            var mirrored=new Vector2[points.Length];
            for(int i=0;i<points.Length;i++)mirrored[i]=new Vector2(1440-points[i].x,points[i].y);
            return mirrored;
        }

        public readonly struct Fixture
        {
            public readonly Rect Body;
            public readonly Vector2 Anchor;
            public Fixture(Rect body,Vector2 anchor) { Body=body;Anchor=anchor; }
        }
    }
}
