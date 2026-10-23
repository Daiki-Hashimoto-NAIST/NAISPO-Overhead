using System;
using UnityEngine;

namespace Naispo.Overhead
{
    // Bake each icon into one alpha surface. Overlapping shapes use their maximum
    // coverage, so a single GUI.color fade cannot reveal their internal joins.
    public static class GameIconArt
    {
        const float InverseSqrtTwo=0.70710678118f;

        // Original continuous thread, in 72 x 54 display coordinates around (0, 0).
        // Uneven cubic loops and two free ends preserve its loose, tangled shape.
        static readonly Vector2[] TangledThread={
            new Vector2(-30,12),
            new Vector2(-12,22),new Vector2(6,-21),new Vector2(-9,-17),
            new Vector2(-28,-12),new Vector2(-24,16),new Vector2(5,14),
            new Vector2(32,15),new Vector2(24,-21),new Vector2(8,-15),
            new Vector2(-8,-10),new Vector2(-2,20),new Vector2(17,7),
            new Vector2(31,-4),new Vector2(11,-27),new Vector2(-6,-14),
            new Vector2(-26,3),new Vector2(-5,25),new Vector2(14,12),
            new Vector2(31,-1),new Vector2(-4,-16),new Vector2(-18,-4),
            new Vector2(-32,12),new Vector2(9,23),new Vector2(22,3),
            new Vector2(33,-18),new Vector2(-1,-23),new Vector2(-13,-5),
            new Vector2(-21,11),new Vector2(25,18),new Vector2(23,-1),
            new Vector2(21,-15),new Vector2(-10,-24),new Vector2(-24,-9),
            new Vector2(-32,10),new Vector2(6,18),new Vector2(10,-2),
            new Vector2(13,-16),new Vector2(-17,-14),new Vector2(-10,4),
            new Vector2(-4,17),new Vector2(23,17),new Vector2(31,12)
        };

        // Display at 44 x 44 to retain the original size-36 heart and its padding.
        public static Texture2D CreateHeart() =>
            CreateDistanceMask("NAISPO Heart",128,44,HeartDistance);

        public static Texture2D CreateTangle()
        {
            const int width=256,height=192,steps=14;
            const float scale=width/72f,radius=1.85f*0.5f*scale;
            Color32[] pixels=WhitePixels(width*height);
            Vector2 previous=ThreadPixel(TangledThread[0],scale,height);
            for(int segment=0;segment<TangledThread.Length-1;segment+=3)
            {
                Vector2 start=TangledThread[segment],controlA=TangledThread[segment+1];
                Vector2 controlB=TangledThread[segment+2],end=TangledThread[segment+3];
                for(int step=1;step<=steps;step++)
                {
                    float t=step/(float)steps,u=1-t;
                    Vector2 point=u*u*u*start+3*u*u*t*controlA+3*u*t*t*controlB+t*t*t*end;
                    Vector2 next=ThreadPixel(point,scale,height);
                    UnionCapsule(pixels,width,height,previous,next,radius);
                    previous=next;
                }
            }
            return CreateTexture("NAISPO Tangled Thread",width,height,pixels);
        }

        // A unit radius maps directly to a display rectangle of radius * 2.
        public static Texture2D CreateSparkle() =>
            CreateDistanceMask("NAISPO Sparkle",128,2,SparkleDistance);

        static Texture2D CreateDistanceMask(string name,int resolution,float displaySize,Func<Vector2,float> distance)
        {
            Color32[] pixels=WhitePixels(resolution*resolution);
            float scale=resolution/displaySize;
            for(int y=0;y<resolution;y++)
                for(int x=0;x<resolution;x++)
                {
                    // Texture rows start at the bottom; the original GUI geometry uses Y down.
                    Vector2 point=new Vector2((x+0.5f)/scale-displaySize*0.5f,
                        displaySize*0.5f-(y+0.5f)/scale);
                    pixels[y*resolution+x].a=Coverage(distance(point)*scale);
                }
            return CreateTexture(name,resolution,resolution,pixels);
        }

        static float HeartDistance(Vector2 point)
        {
            const float size=36;
            Vector2 relative=point-new Vector2(0,size*0.08f);
            Vector2 diamond=new Vector2(relative.x+relative.y,relative.y-relative.x)*InverseSqrtTwo;
            float square=RoundedBoxDistance(diamond,new Vector2(size*0.31f,size*0.31f),0);
            float left=Vector2.Distance(point,new Vector2(-size*0.22f,-size*0.14f))-size*0.31f;
            float right=Vector2.Distance(point,new Vector2(size*0.22f,-size*0.14f))-size*0.31f;
            return Mathf.Min(square,Mathf.Min(left,right));
        }

        static float SparkleDistance(Vector2 point)
        {
            Vector2 rotated=new Vector2(point.x+point.y,point.y-point.x)*InverseSqrtTwo;
            float vertical=RoundedBoxDistance(rotated,new Vector2(0.36f,1),0.3f);
            float horizontal=RoundedBoxDistance(rotated,new Vector2(1,0.36f),0.3f);
            return Mathf.Min(vertical,horizontal);
        }

        static float RoundedBoxDistance(Vector2 point,Vector2 halfSize,float radius)
        {
            Vector2 q=new Vector2(Mathf.Abs(point.x),Mathf.Abs(point.y))-halfSize+Vector2.one*radius;
            return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude
                +Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
        }

        static Vector2 ThreadPixel(Vector2 point,float scale,int height) =>
            new Vector2((point.x+36)*scale,height*0.5f-point.y*scale);

        static void UnionCapsule(Color32[] pixels,int width,int height,Vector2 start,Vector2 end,float radius)
        {
            // Restrict work to this short curve segment's bounds, including its AA fringe.
            float padding=radius+1;
            int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(start.x,end.x)-padding));
            int maxX=Mathf.Min(width-1,Mathf.CeilToInt(Mathf.Max(start.x,end.x)+padding));
            int minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(start.y,end.y)-padding));
            int maxY=Mathf.Min(height-1,Mathf.CeilToInt(Mathf.Max(start.y,end.y)+padding));
            Vector2 segment=end-start;
            float lengthSquared=segment.sqrMagnitude;
            for(int y=minY;y<=maxY;y++)
                for(int x=minX;x<=maxX;x++)
                {
                    int index=y*width+x;
                    if(pixels[index].a==255)continue;
                    Vector2 point=new Vector2(x+0.5f,y+0.5f);
                    float t=lengthSquared>0 ? Mathf.Clamp01(Vector2.Dot(point-start,segment)/lengthSquared) : 0;
                    byte coverage=Coverage(Vector2.Distance(point,start+segment*t)-radius);
                    if(coverage>pixels[index].a)pixels[index].a=coverage;
                }
        }

        // A one-texel transition gives smooth edges without weakening the solid interior.
        static byte Coverage(float signedPixelDistance) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f-signedPixelDistance)*255);

        static Color32[] WhitePixels(int count)
        {
            var pixels=new Color32[count];
            for(int i=0;i<count;i++)pixels[i]=new Color32(255,255,255,0);
            return pixels;
        }

        static Texture2D CreateTexture(string name,int width,int height,Color32[] pixels)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name=name,
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp,
                anisoLevel=0,
                hideFlags=HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            return texture;
        }
    }
}
