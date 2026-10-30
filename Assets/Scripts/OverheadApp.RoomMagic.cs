using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        const int RoomShadeWidth=288,RoomShadeHeight=180;
        Texture2D roomShade,roomWarm;
        RoomLightingCache roomLightingCache;

        void InitializeRoomShade()
        {
            roomShade=CreateRoomLightTexture("NAISPO Room Night");
            roomWarm=CreateRoomLightTexture("NAISPO Room Candlelight");
            roomLightingCache=new RoomLightingCache(RoomShadeWidth,RoomShadeHeight);
        }

        static Texture2D CreateRoomLightTexture(string name) =>
            new Texture2D(RoomShadeWidth,RoomShadeHeight,TextureFormat.RGBA32,false)
            {
                name=name,
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Bilinear,
                hideFlags=HideFlags.DontSave
            };

        void DrawRoomShade(float arrivedProgress)
        {
            if(roomShade==null||roomWarm==null||roomLightingCache==null)return;
            // Layout/input events can run between repaints. Update and upload only
            // when the new exposure is actually going to be displayed.
            if(Event.current.type==EventType.Repaint&&roomLightingCache.TryUpdate(arrivedProgress))
            {
                roomShade.SetPixels32(roomLightingCache.ShadePixels);
                roomShade.Apply(false,false);
                roomWarm.SetPixels32(roomLightingCache.WarmPixels);
                roomWarm.Apply(false,false);
            }
            Color previous=GUI.color;
            try
            {
                GUI.color=Color.white;
                Rect canvas=new Rect(0,0,1440,900);
                GUI.DrawTexture(canvas,roomShade,ScaleMode.StretchToFill,true);
                GUI.DrawTexture(canvas,roomWarm,ScaleMode.StretchToFill,true);
            }
            finally { GUI.color=previous; }
        }

        void DrawRoomFixtures()
        {
            float progress=GardenAtmosphere.WallWarmth(garden,playTime);
            Color previous=GUI.color;
            try
            {
                for(int index=0;index<RoomAwakening.PairCount;index++)
                {
                    float brightness=RoomAwakening.PairBrightness(progress,index);
                    DrawRoomFixture(RoomAwakening.LeftFixtures[index],brightness);
                    DrawRoomFixture(RoomAwakening.RightFixtures[index],brightness);
                }
            }
            finally { GUI.color=previous; }
        }

        void DrawRoomFixture(RoomAwakening.Fixture fixture,float brightness)
        {
            float scale=fixture.Scale;
            Vector2 glass=fixture.Glass;
            GUI.color=Color.white;
            if(brightness>0)
            {
                // Small halos belong to the actual lamps; broad light is restricted to
                // the painted wood, foliage and floor by the shared architectural mask.
                Glow(new Rect(glass.x-43*scale,glass.y-52*scale,86*scale,104*scale),
                    Rgb(0xFFE3A3,0.27f*brightness));
                Vector2 reflection=RoomAwakening.FloorReflection(fixture);
                Glow(new Rect(reflection.x-75*scale,reflection.y-64*scale,150*scale,128*scale),
                    Rgb(0xE8C894,0.10f*brightness));
            }

            Vector2 ring=LanternRoom.RingPoint(fixture.Body);
            Vector2 elbow=new Vector2(ring.x,fixture.Anchor.y);
            Color metal=Rgb(0x977F5A,Mathf.Lerp(0.30f,0.94f,brightness));
            Rounded(new Rect(fixture.Anchor.x-4*scale,fixture.Anchor.y-6*scale,8*scale,12*scale),
                2*scale,Rgb(0x493F34));
            Capsule(fixture.Anchor,elbow,2.2f*scale,metal);
            Capsule(elbow,ring,1.25f*scale,metal);

            GUI.color=LanternRoom.Tint(brightness);
            DrawLanternBody(fixture.Body);
            GUI.color=Color.white;
        }
    }

    // Architecture and light falloff never move. Sample them once at startup so an
    // arriving light only blends the two existing pixel buffers, not the room geometry.
    public sealed class RoomLightingCache
    {
        public const int Steps=256;
        public readonly int Width,Height;
        public readonly Color32[] ShadePixels,WarmPixels;
        public int Step { get; private set; }=-1;
        readonly PixelGeometry[] geometry;

        public RoomLightingCache(int width,int height)
        {
            if(width<=0)throw new System.ArgumentOutOfRangeException(nameof(width));
            if(height<=0)throw new System.ArgumentOutOfRangeException(nameof(height));
            Width=width;
            Height=height;
            int count=checked(width*height);
            ShadePixels=new Color32[count];
            WarmPixels=new Color32[count];
            geometry=new PixelGeometry[count];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                // Texture rows run upward, whereas the room's canvas runs downward.
                Vector2 point=new Vector2((x+0.5f)*1440/width,900-(y+0.5f)*900/height);
                geometry[y*width+x]=new PixelGeometry(point);
            }
        }

        public bool TryUpdate(float arrivedProgress)
        {
            float progress=LanternRoom.IsFinite(arrivedProgress) ? Mathf.Clamp01(arrivedProgress) : 0;
            int step=Mathf.RoundToInt(progress*Steps);
            if(step==Step)return false;
            progress=step/(float)Steps;
            float ambient=0.84f*Mathf.SmoothStep(0,1,progress);
            float inner=RoomAwakening.PairBrightness(progress,0);
            float outer=RoomAwakening.PairBrightness(progress,1);
            for(int index=0;index<geometry.Length;index++)
            {
                PixelGeometry pixel=geometry[index];
                float local=Mathf.Max(progress*pixel.FloorPool,
                    Mathf.Max(inner*pixel.InnerLight,outer*pixel.OuterLight));
                float illumination=Mathf.Clamp01(ambient+0.16f*local);
                float shade=Mathf.Lerp(pixel.ExteriorShade,
                    Mathf.Lerp(0.87f,0.12f,illumination),pixel.Interior);
                float warm=0.15f*pixel.Interior*illumination;
                ShadePixels[index]=new Color32(3,10,17,(byte)Mathf.RoundToInt(shade*255));
                WarmPixels[index]=new Color32(245,179,86,(byte)Mathf.RoundToInt(warm*255));
            }
            Step=step;
            return true;
        }

        readonly struct PixelGeometry
        {
            public readonly float Interior,ExteriorShade,FloorPool,InnerLight,OuterLight;

            public PixelGeometry(Vector2 point)
            {
                Interior=RoomAwakening.InteriorWeight(point);
                ExteriorShade=0.32f-0.23f*RoomAwakening.LightPool(point,RoomAwakening.MoonCenter,new Vector2(88,98));
                FloorPool=RoomAwakening.LightPool(point,RoomAwakening.FloorCenter,new Vector2(800,430));
                InnerLight=Mathf.Max(RoomAwakening.FixtureLight(point,RoomAwakening.LeftFixtures[0]),
                    RoomAwakening.FixtureLight(point,RoomAwakening.RightFixtures[0]));
                OuterLight=Mathf.Max(RoomAwakening.FixtureLight(point,RoomAwakening.LeftFixtures[1]),
                    RoomAwakening.FixtureLight(point,RoomAwakening.RightFixtures[1]));
            }
        }
    }

    // Geometry follows the finite glass room: only surfaces inside the window frames
    // receive warm light. The distant blue forest and moon keep their night exposure.
    public static class RoomAwakening
    {
        public const int PairCount=2;
        public static readonly Vector2 MoonCenter=new Vector2(144,132);
        public static readonly Vector2 FloorCenter=new Vector2(720,795);
        public static readonly Fixture[] LeftFixtures=BuildFixtures(false);
        public static readonly Fixture[] RightFixtures=BuildFixtures(true);

        public static float Reveal(GardenProgress progress,float playTime,float delay)
        {
            if(!LanternRoom.IsFinite(delay)||delay<0)return 0;
            return GardenAtmosphere.Progress(progress,playTime-delay);
        }

        public static float ArrivalPulse(GardenProgress progress,int index,float playTime) =>
            LanternRoom.ArrivalPulse(progress,index,playTime);

        public static float PairBrightness(float arrivedProgress,int index)
        {
            if(index<0||index>=PairCount||!LanternRoom.IsFinite(arrivedProgress))return 0;
            float progress=Mathf.Clamp01(arrivedProgress);
            float start=index==0 ? 0.10f : 0.45f;
            float end=index==0 ? 0.65f : 1.00f;
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,progress));
        }

        public static float PairBrightness(GardenProgress garden,float playTime,int index) =>
            PairBrightness(GardenAtmosphere.WallWarmth(garden,playTime),index);

        public static float InteriorWeight(Vector2 point)
        {
            if(!Finite(point))return 0;
            float edge=Mathf.Abs(point.x-720)/720f;
            // The sill rises toward the centre. A soft transition follows it rather
            // than drawing a horizontal band across the glass and floor.
            float floorY=581+43*edge*edge;
            float floor=Mathf.SmoothStep(0,1,Mathf.InverseLerp(floorY-11,floorY+20,point.y));
            float architecture=0;
            for(int side=-1;side<=1;side+=2)
            {
                float localX=side<0 ? point.x : 1440-point.x;
                float pillar=Strip(localX,270,18,43)*VerticalRange(point.y,180,610,42);
                float outer=Strip(localX,60,19,38)*VerticalRange(point.y,110,655,45);
                float mullion=Strip(localX,514,3,10)*VerticalRange(point.y,0,584,16);
                float archX=270+190*Mathf.Pow(Mathf.Clamp01(1-point.y/320f),2);
                float arch=Strip(localX,archX,22,73)*VerticalRange(point.y,-20,365,35);
                float upperLeaves=LightPool(new Vector2(localX,point.y),
                    new Vector2(150,15),new Vector2(400,195));
                float corner=Strip(localX,0,20,62);
                architecture=Mathf.Max(architecture,Mathf.Max(pillar,Mathf.Max(outer,
                    Mathf.Max(mullion,Mathf.Max(arch,Mathf.Max(upperLeaves,corner))))));
            }
            architecture*=1-LightPool(point,MoonCenter,new Vector2(100,120));
            return Mathf.Clamp01(Mathf.Max(floor,architecture));
        }

        // Food, spirits and controls are drawn afterward and never receive this shade.
        public static float ShadeAlpha(Vector2 point,float arrivedProgress)
        {
            if(!Finite(point))return 0.87f;
            float progress=Progress(arrivedProgress);
            float exterior=0.32f-0.23f*LightPool(point,MoonCenter,new Vector2(88,98));
            float interior=Mathf.Lerp(0.87f,0.12f,Illumination(point,progress));
            return Mathf.Lerp(exterior,interior,InteriorWeight(point));
        }

        public static float WarmAlpha(Vector2 point,float arrivedProgress)
        {
            if(!Finite(point))return 0;
            float progress=Progress(arrivedProgress);
            return 0.15f*InteriorWeight(point)*Illumination(point,progress);
        }

        public static Vector2 FloorReflection(Fixture fixture) =>
            new Vector2(fixture.Glass.x,fixture.Scale>0.95f ? 724 : 779);

        static float Illumination(Vector2 point,float progress)
        {
            float local=progress*LightPool(point,FloorCenter,new Vector2(800,430));
            for(int i=0;i<PairCount;i++)
            {
                float brightness=PairBrightness(progress,i);
                local=Mathf.Max(local,brightness*FixtureLight(point,LeftFixtures[i]));
                local=Mathf.Max(local,brightness*FixtureLight(point,RightFixtures[i]));
            }
            // The central lantern gradually fills the room; the wall fittings add
            // local pools without changing the exposure of anything beyond the glass.
            return Mathf.Clamp01(0.84f*Mathf.SmoothStep(0,1,progress)+0.16f*local);
        }

        internal static float FixtureLight(Vector2 point,Fixture fixture) =>
            Mathf.Max(LightPool(point,fixture.Glass,new Vector2(340,440)),
                LightPool(point,FloorReflection(fixture),new Vector2(310,250)));

        static float Progress(float value) => LanternRoom.IsFinite(value) ? Mathf.Clamp01(value) : 0;
        static bool Finite(Vector2 point) => LanternRoom.IsFinite(point.x)&&LanternRoom.IsFinite(point.y);

        static float Strip(float position,float center,float solid,float feather) =>
            1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(solid,feather,Mathf.Abs(position-center)));

        static float VerticalRange(float y,float start,float end,float feather) =>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(start-feather,start,y))*
            (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(end,end+feather,y)));

        internal static float LightPool(Vector2 point,Vector2 center,Vector2 radius)
        {
            float x=(point.x-center.x)/radius.x,y=(point.y-center.y)/radius.y;
            float falloff=Mathf.Clamp01(1-Mathf.Sqrt(x*x+y*y));
            return falloff*falloff*(3-2*falloff);
        }

        static Fixture[] BuildFixtures(bool right)
        {
            var fixtures=new Fixture[PairCount];
            float[] xs={322,124},ys={330,475},heights={96,84},anchorXs={270,63},anchorYs={270,413};
            for(int i=0;i<fixtures.Length;i++)
            {
                Vector2 glass=new Vector2(right ? 1440-xs[i] : xs[i],ys[i]);
                Vector2 anchor=new Vector2(right ? 1440-anchorXs[i] : anchorXs[i],anchorYs[i]);
                float sourceScale=heights[i]/LanternRoom.Crop.height;
                Rect body=new Rect(glass.x-(LanternRoom.SourceGlass.x-LanternRoom.Crop.x)*sourceScale,
                    glass.y-(LanternRoom.SourceGlass.y-LanternRoom.Crop.y)*sourceScale,
                    LanternRoom.Crop.width*sourceScale,heights[i]);
                fixtures[i]=new Fixture(glass,body,anchor,heights[i]/LanternRoom.SideHeight);
            }
            return fixtures;
        }

        public readonly struct Fixture
        {
            public readonly Vector2 Glass,Anchor;
            public readonly Rect Body;
            public readonly float Scale;
            public Fixture(Vector2 glass,Rect body,Vector2 anchor,float scale)
            {
                Glass=glass;
                Body=body;
                Anchor=anchor;
                Scale=scale;
            }
        }
    }
}
