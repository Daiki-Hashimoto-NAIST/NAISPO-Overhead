using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        const float GardenSpriteWidth=64,GardenSpriteHeight=96,GardenRootY=92;
        static readonly int[] GardenPalette={0xEBA3B7,0xF0C861,0xFFF2BC,0xC5B1DF,0xF2AD8D};
        // Small, uneven groups leave space between them. The distant meadow carries
        // most of the colour; only nearby flowers have individual stems and petals.
        static readonly GardenFlower[] GardenBackground={
            new GardenFlower(54,855,0.90f,0,0,-9,0.2f),
            new GardenFlower(216,821,0.42f,0,2,10,0.6f),
            new GardenFlower(1195,879,0.67f,0,3,-12,0.4f),
            new GardenFlower(1388,835,0.46f,0,1,7,0.8f),

            new GardenFlower(142,891,1.16f,1,4,9,0.6f),
            new GardenFlower(29,772,0.36f,1,2,-6,0.1f),
            new GardenFlower(269,885,0.63f,1,0,-13,0.9f),
            new GardenFlower(1319,868,1.00f,1,3,-8,0.3f),
            new GardenFlower(1410,895,0.55f,1,2,-10,0.7f),

            new GardenFlower(196,877,0.72f,2,1,15,0.5f),
            new GardenFlower(84,892,0.40f,2,2,-5,0.8f),
            new GardenFlower(1281,800,0.38f,2,4,11,0.2f),
            new GardenFlower(1384,884,0.94f,2,4,12,0.7f),
            new GardenFlower(867,889,0.45f,2,3,-7,1.0f)
        };
        static readonly GardenFlower[] GardenForeground={
            new GardenFlower(241,849,1.05f,2,0,-12,0.9f),
            new GardenFlower(1207,873,1.26f,2,4,9,0.6f),
            new GardenFlower(558,893,0.78f,2,1,-10,1.1f),
            new GardenFlower(958,895,0.52f,2,2,12,0.4f)
        };
        Texture2D[] gardenFlowerTextures;

        readonly struct GardenFlower
        {
            public readonly Vector2 Root;
            public readonly float Size,Delay,Lean;
            public readonly int Stage,Palette;

            public GardenFlower(float x,float y,float size,int stage,int palette,float lean,float delay)
            {
                Root=new Vector2(x,y);Size=size;Stage=stage;Palette=palette;Lean=lean;Delay=delay;
            }
        }

        // Each flower stays a single image while it grows and leans.
        void InitializeGardenArt()
        {
            if(gardenFlowerTextures!=null)return;
            System.Array.Sort(GardenBackground,(a,b)=>a.Root.y.CompareTo(b.Root.y));
            System.Array.Sort(GardenForeground,(a,b)=>a.Root.y.CompareTo(b.Root.y));
            gardenFlowerTextures=new Texture2D[GardenPalette.Length];
            for(int i=0;i<gardenFlowerTextures.Length;i++)
                gardenFlowerTextures[i]=CreateGardenFlowerTexture(i);
        }

        void DestroyGardenArt()
        {
            if(gardenFlowerTextures==null)return;
            foreach(Texture2D texture in gardenFlowerTextures)
                if(texture!=null)Destroy(texture);
            gardenFlowerTextures=null;
        }

        void DrawGardenFlowers() => DrawGardenLayer(GardenBackground);

        // A few close flowers frame the finished garden without making a fence across it.
        void DrawGardenForeground()
        {
            if(garden.IsComplete)DrawGardenLayer(GardenForeground);
        }

        void DrawGardenLayer(GardenFlower[] flowers)
        {
            if(gardenFlowerTextures==null||garden.BloomCount==0)return;
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                GUI.color=Color.white;
                foreach(GardenFlower flower in flowers)
                {
                    if(flower.Stage>=garden.BloomCount)continue;
                    float age=playTime-garden.BloomStartedAt(flower.Stage)-flower.Delay;
                    if(age<=0)continue;
                    float t=Mathf.Clamp01(age/1.2f);
                    float growth=t*t*t*(t*(t*6-15)+10);
                    if(growth<0.015f)continue;
                    float size=flower.Size*growth;
                    // The whole sprite grows upward from a fixed root, with no alpha animation.
                    Rect rect=new Rect(flower.Root.x-GardenSpriteWidth*0.5f*size,
                        flower.Root.y-GardenRootY*size,GardenSpriteWidth*size,GardenSpriteHeight*size);
                    float breeze=GardenAtmosphere.BreezeAngle(garden,playTime,flower.Root.x)*growth;
                    GUI.matrix=originalMatrix*GameLayout.RotationAround(flower.Root,flower.Lean+breeze);
                    GUI.DrawTexture(rect,gardenFlowerTextures[flower.Palette],ScaleMode.StretchToFill,true);
                }
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        static Texture2D CreateGardenFlowerTexture(int palette)
        {
            const int width=128,height=192;
            const float scale=2;
            var pixels=new Color32[width*height];
            Color stemColor=Rgb(0x6F9C70),leafColor=Rgb(0x8AB67D),petalColor=Rgb(GardenPalette[palette]);
            Color centerColor=Rgb(palette==2 ? 0xE6BC5B : 0xFFF0AE);
            Vector2 root=new Vector2(32,GardenRootY),head=new Vector2(32,26);
            Vector2 middle=new Vector2(32+(palette%2==0 ? 3 : -3),62);
            Vector2 upperLeaf=Vector2.Lerp(head,middle,0.78f);
            Vector2 lowerLeaf=Vector2.Lerp(middle,root,0.42f);
            Vector2 rightTip=upperLeaf+new Vector2(19,-11),leftTip=lowerLeaf+new Vector2(-20,-10);
            int petalCount=palette==2 ? 7 : palette==1 ? 6 : 5;
            float petalRadius=palette==2 ? 7.2f : palette==1 ? 8 : 9.4f;
            var petals=new Vector2[petalCount];
            for(int i=0;i<petals.Length;i++)
            {
                float angle=i*Mathf.PI*2/petalCount-Mathf.PI/2+(palette-2)*0.035f;
                petals[i]=head+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*11.5f;
            }
            for(int y=0;y<height;y++)
                for(int x=0;x<width;x++)
                {
                    Vector2 point=new Vector2((x+0.5f)/scale,(height-y-0.5f)/scale);
                    Color pixel=new Color(1,1,1,0);
                    float stem=Mathf.Min(GardenSegmentDistance(point,root,middle),GardenSegmentDistance(point,middle,head))-1.7f;
                    PaintGardenPixel(ref pixel,stemColor,GardenCoverage(stem*scale));
                    float leaves=Mathf.Min(GardenLeafDistance(point,upperLeaf,rightTip,4.7f),
                        GardenLeafDistance(point,lowerLeaf,leftTip,5.1f));
                    PaintGardenPixel(ref pixel,leafColor,GardenCoverage(leaves*scale));
                    float petalDistance=float.PositiveInfinity;
                    for(int i=0;i<petals.Length;i++)
                        petalDistance=Mathf.Min(petalDistance,Vector2.Distance(point,petals[i])-petalRadius);
                    PaintGardenPixel(ref pixel,petalColor,GardenCoverage(petalDistance*scale));
                    PaintGardenPixel(ref pixel,centerColor,GardenCoverage((Vector2.Distance(point,head)-6.5f)*scale));
                    pixels[y*width+x]=pixel;
                }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="NAISPO Garden Flower "+palette,
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp,
                anisoLevel=0,
                hideFlags=HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            return texture;
        }

        static float GardenSegmentDistance(Vector2 point,Vector2 start,Vector2 end)
        {
            Vector2 segment=end-start;
            float t=Mathf.Clamp01(Vector2.Dot(point-start,segment)/segment.sqrMagnitude);
            return Vector2.Distance(point,start+segment*t);
        }

        static float GardenLeafDistance(Vector2 point,Vector2 attachment,Vector2 tip,float radius)
        {
            Vector2 delta=tip-attachment;
            float halfLength=delta.magnitude*0.5f+1;
            Vector2 axis=delta.normalized,relative=point-(attachment+tip)*0.5f;
            float along=Vector2.Dot(relative,axis),across=Vector2.Dot(relative,new Vector2(-axis.y,axis.x));
            float k0=Mathf.Sqrt(along*along/(halfLength*halfLength)+across*across/(radius*radius));
            float k1=Mathf.Sqrt(along*along/Mathf.Pow(halfLength,4)+across*across/Mathf.Pow(radius,4));
            return k1>0.000001f ? k0*(k0-1)/k1 : -radius;
        }

        static float GardenCoverage(float signedPixelDistance) => Mathf.Clamp01(0.5f-signedPixelDistance);

        static void PaintGardenPixel(ref Color pixel,Color layer,float coverage)
        {
            if(coverage<=0)return;
            // Union the silhouette coverage; color transitions never lower an existing opaque area.
            float alpha=Mathf.Max(pixel.a,coverage);
            pixel=Color.Lerp(pixel,layer,coverage/alpha);
            pixel.a=alpha;
        }
    }
}
