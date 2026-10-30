using System.Collections.Generic;
using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        const int MeadowWidth=1440,MeadowHeight=410;
        static readonly Rect MeadowRect=new Rect(0,490,1440,410);
        Texture2D[] meadowTextures;

        readonly struct MeadowPatch
        {
            public readonly Vector2 Center,Radius;
            public readonly Color Color;
            public readonly float Cos,Sin,Phase,Strength;
            public readonly bool Near;

            public MeadowPatch(float x,float y,float rx,float ry,float angle,int color,float strength,bool near,float phase)
            {
                Center=new Vector2(x,y);Radius=new Vector2(rx,ry);Color=Rgb(color);
                Cos=Mathf.Cos(angle*Mathf.Deg2Rad);Sin=Mathf.Sin(angle*Mathf.Deg2Rad);
                Strength=strength;Near=near;Phase=phase;
            }
        }

        // A faint wash connects small flower clusters. The individual marks become
        // smaller and less distinct with distance, with open grass between the groups.
        static readonly MeadowPatch[][] MeadowPatches={
            new[]{
                new MeadowPatch(96,590,130,53,-14,0xE79AAE,0.58f,false,0.4f),
                new MeadowPatch(1375,575,115,48,18,0xB8A1D6,0.56f,false,2.1f),
                new MeadowPatch(51,756,130,84,-20,0xF3D074,0.66f,true,1.2f),
                new MeadowPatch(1387,778,128,82,21,0xE79AAE,0.64f,true,3.5f)
            },
            new[]{
                new MeadowPatch(270,610,98,49,-26,0xB8A1D6,0.62f,false,3.3f),
                new MeadowPatch(1218,624,90,48,24,0xF3D074,0.60f,false,0.6f),
                new MeadowPatch(146,834,140,80,-11,0xE79AAE,0.66f,true,4.5f),
                new MeadowPatch(1265,829,138,78,17,0x8DC6B7,0.64f,true,1.8f)
            },
            new[]{
                new MeadowPatch(22,623,93,43,9,0x8DC6B7,0.53f,false,2.7f),
                new MeadowPatch(1408,645,85,50,-13,0xE79AAE,0.57f,false,4.1f),
                new MeadowPatch(47,872,140,75,14,0xB8A1D6,0.65f,true,0.8f),
                new MeadowPatch(1389,877,142,78,-16,0xF3D074,0.66f,true,2.9f)
            }
        };

        void InitializeMeadowArt()
        {
            if(meadowTextures!=null)return;
            int count=MeadowWidth*MeadowHeight;
            var farMask=new float[count];
            var nearMask=new float[count];
            for(int y=0;y<MeadowHeight;y++)
                for(int x=0;x<MeadowWidth;x++)
                {
                    Vector2 point=MeadowPixelPoint(x,y);
                    float far=Mathf.Max(MeadowHillMask(point,new Vector2(125,725),new Vector2(315,235)),
                        MeadowHillMask(point,new Vector2(1385,725),new Vector2(315,215)));
                    float near=Mathf.Max(MeadowHillMask(point,new Vector2(155,805),new Vector2(365,155)),
                        MeadowHillMask(point,new Vector2(1335,832),new Vector2(325,140)));
                    int index=y*MeadowWidth+x;
                    // The nearer hills have already been painted over the far ones.
                    farMask[index]=far*(1-near);
                    nearMask[index]=near;
                }
            meadowTextures=new Texture2D[MeadowPatches.Length];
            for(int stage=0;stage<meadowTextures.Length;stage++)
                meadowTextures[stage]=CreateMeadowTexture(stage,farMask,nearMask);
        }

        void DestroyMeadowArt()
        {
            if(meadowTextures==null)return;
            foreach(Texture2D texture in meadowTextures)
                if(texture!=null)Destroy(texture);
            meadowTextures=null;
        }

        static Texture2D CreateMeadowTexture(int stage,float[] farMask,float[] nearMask)
        {
            var pixels=new Color32[MeadowWidth*MeadowHeight];
            MeadowPatch[] patches=MeadowPatches[stage];
            for(int y=0;y<MeadowHeight;y++)
                for(int x=0;x<MeadowWidth;x++)
                {
                    int index=y*MeadowWidth+x;
                    Color pixel=new Color(1,1,1,0);
                    if(farMask[index]>0||nearMask[index]>0)
                    {
                        Vector2 point=MeadowPixelPoint(x,y);
                        float red=0,green=0,blue=0,weight=0,alpha=0;
                        foreach(MeadowPatch patch in patches)
                        {
                            float mask=patch.Near ? nearMask[index] : farMask[index];
                            if(mask<=0)continue;
                            float coverage=MeadowPatchCoverage(point,patch)*patch.Strength*mask*0.32f;
                            red+=patch.Color.r*coverage;green+=patch.Color.g*coverage;blue+=patch.Color.b*coverage;
                            weight+=coverage;alpha=Mathf.Max(alpha,coverage);
                        }
                        if(weight>0)pixel=new Color(red/weight,green/weight,blue/weight,alpha);
                    }
                    pixels[index]=pixel;
                }
            PaintMeadowPlants(pixels,patches,stage,farMask,nearMask);
            var texture=new Texture2D(MeadowWidth,MeadowHeight,TextureFormat.RGBA32,false)
            {
                name="NAISPO Meadow Colors "+stage,
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp,
                anisoLevel=0,
                hideFlags=HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            return texture;
        }

        static Vector2 MeadowPixelPoint(int x,int y) => new Vector2(x+0.5f,900-y-0.5f);

        readonly struct MeadowPlant
        {
            public readonly Vector2 Head;
            public readonly float Size,Lean,Turn;
            public readonly Color Color;
            public readonly int Kind;

            public MeadowPlant(Vector2 head,float size,float lean,float turn,Color color,int kind)
            {
                Head=head;Size=size;Lean=lean;Turn=turn;Color=color;Kind=kind;
            }
        }

        static void PaintMeadowPlants(Color32[] pixels,MeadowPatch[] patches,int stage,float[] farMask,float[] nearMask)
        {
            var random=new System.Random(49017+stage*2371);
            foreach(MeadowPatch patch in patches)
            {
                float[] mask=patch.Near ? nearMask : farMask;
                var plants=new List<MeadowPlant>();
                int groups=random.Next(3,6);
                for(int group=0;group<groups;group++)
                {
                    Vector2 center=new Vector2(MeadowRange(random,-0.65f,0.65f),MeadowRange(random,-0.58f,0.58f));
                    float spreadX=MeadowRange(random,0.14f,0.32f),spreadY=MeadowRange(random,0.13f,0.28f);
                    int count=random.Next(patch.Near ? 5 : 7,patch.Near ? 10 : 13);
                    for(int i=0,attempts=0;i<count&&attempts<count*8;attempts++)
                    {
                        Vector2 offset=new Vector2(center.x+MeadowRange(random,-1,1)*spreadX,
                            center.y+MeadowRange(random,-1,1)*spreadY);
                        if(offset.sqrMagnitude>0.92f)continue;
                        Vector2 local=Vector2.Scale(offset,patch.Radius);
                        Vector2 head=patch.Center+new Vector2(local.x*patch.Cos-local.y*patch.Sin,
                            local.x*patch.Sin+local.y*patch.Cos);
                        int x=Mathf.FloorToInt(head.x),y=Mathf.FloorToInt(900-head.y);
                        if(x<8||x>=MeadowWidth-8||y<8||y>=MeadowHeight-8||mask[y*MeadowWidth+x]<0.9f)continue;
                        // Keep the open path empty, rather than spending detail under its overlay.
                        Vector2 path=new Vector2((head.x-720)/550,(head.y-825)/165);
                        if(path.sqrMagnitude<1.06f)continue;
                        float depth=patch.Near ? Mathf.InverseLerp(690,900,head.y) : Mathf.InverseLerp(510,690,head.y);
                        float size=(patch.Near ? Mathf.Lerp(3.5f,7.5f,depth) : Mathf.Lerp(1.6f,3.6f,depth))
                            *MeadowRange(random,0.7f,1.25f);
                        bool crowded=false;
                        foreach(MeadowPlant other in plants)
                            if((other.Head-head).sqrMagnitude<Mathf.Pow((other.Size+size)*1.25f,2))
                            { crowded=true;break; }
                        if(crowded)continue;
                        Color color=Color.Lerp(patch.Color,Rgb(0xFFF2C7),MeadowRange(random,0.02f,0.30f));
                        if(random.NextDouble()<0.18)color=Rgb(0xFFF3CD);
                        color.a=patch.Near ? 0.95f : 0.77f;
                        plants.Add(new MeadowPlant(head,size,MeadowRange(random,-0.6f,0.6f),
                            MeadowRange(random,-Mathf.PI,Mathf.PI),color,random.Next(4)));
                        i++;
                    }
                }
                plants.Sort((a,b)=>a.Head.y.CompareTo(b.Head.y));
                foreach(MeadowPlant plant in plants)PaintMeadowPlant(pixels,mask,plant,patch.Near);
            }
        }

        static void PaintMeadowPlant(Color32[] pixels,float[] mask,MeadowPlant plant,bool near)
        {
            Vector2 head=plant.Head;
            float size=plant.Size;
            Color stem=Rgb(near ? 0x709C75 : 0x8BA984,near ? 0.68f : 0.38f);
            Vector2 root=head+new Vector2(plant.Lean*size,size*(near ? 2.6f : 1.4f));
            if(near)
            {
                MeadowStroke(pixels,mask,root,head,Mathf.Max(0.9f,size*0.16f),stem);
                Vector2 junction=Vector2.Lerp(root,head,0.42f);
                MeadowStroke(pixels,mask,junction,junction+new Vector2(size*0.95f,-size*0.7f),size*0.30f,stem);
                if(plant.Kind%2==0)
                    MeadowStroke(pixels,mask,root-Vector2.up*size*0.35f,
                        root+new Vector2(-size*0.9f,-size*1.1f),size*0.24f,stem);
            }
            // Short, tapered blades tie the clusters to the hillside without giving
            // every distant bloom a repeated miniature stem-and-leaf icon.
            if(plant.Kind==0||plant.Kind==3)
            {
                Vector2 grass=root+new Vector2(size*1.1f,size*0.15f);
                MeadowStroke(pixels,mask,grass,grass+new Vector2(-size*0.25f,-size*1.15f),size*0.18f,stem);
                MeadowStroke(pixels,mask,grass,grass+new Vector2(size*0.6f,-size*0.8f),size*0.22f,stem);
            }
            if(plant.Kind==3)
            {
                // A few unopened buds and side-facing flowers break up the round heads.
                MeadowDab(pixels,mask,head,size*0.48f,size*0.85f,plant.Turn*0.18f,plant.Color);
                return;
            }
            int petals=near ? (plant.Kind==0 ? 5 : 4) : 3;
            for(int petal=0;petal<petals;petal++)
            {
                float a=plant.Turn+petal*Mathf.PI*2/petals;
                Vector2 center=head+new Vector2(Mathf.Cos(a),Mathf.Sin(a)*(near ? 0.85f : 0.62f))*size*0.52f;
                MeadowDab(pixels,mask,center,size*0.53f,size*(near ? 0.48f : 0.35f),a,plant.Color);
            }
            if(near)
                MeadowDab(pixels,mask,head,size*0.23f,size*0.20f,0,Rgb(0xF4D077,0.94f));
            else if(plant.Kind==1)
                MeadowDab(pixels,mask,head,size*0.22f,size*0.20f,0,Rgb(0xFFF7D9,0.75f));
        }

        static void MeadowStroke(Color32[] pixels,float[] mask,Vector2 from,Vector2 to,float width,Color color)
        {
            Vector2 delta=to-from;
            MeadowDab(pixels,mask,(from+to)*0.5f,delta.magnitude*0.5f+0.5f,width*0.5f,
                Mathf.Atan2(delta.y,delta.x),color);
        }

        static void MeadowDab(Color32[] pixels,float[] mask,Vector2 center,float rx,float ry,float angle,Color color)
        {
            float radius=Mathf.Max(rx,ry)+1;
            int minX=Mathf.Max(0,Mathf.FloorToInt(center.x-radius));
            int maxX=Mathf.Min(MeadowWidth-1,Mathf.CeilToInt(center.x+radius));
            int minY=Mathf.Max(0,Mathf.FloorToInt(900-center.y-radius));
            int maxY=Mathf.Min(MeadowHeight-1,Mathf.CeilToInt(900-center.y+radius));
            float cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                int index=y*MeadowWidth+x;
                if(mask[index]<=0)continue;
                Vector2 p=MeadowPixelPoint(x,y)-center;
                float u=(p.x*cos+p.y*sin)/rx,v=(-p.x*sin+p.y*cos)/ry;
                float coverage=Mathf.Clamp01((1-Mathf.Sqrt(u*u+v*v))*Mathf.Min(rx,ry)+0.5f);
                float alpha=coverage*color.a*mask[index];
                if(alpha<=0)continue;
                Color old=pixels[index];
                float remaining=old.a*(1-alpha),combined=alpha+remaining;
                pixels[index]=new Color((color.r*alpha+old.r*remaining)/combined,
                    (color.g*alpha+old.g*remaining)/combined,(color.b*alpha+old.b*remaining)/combined,combined);
            }
        }

        static float MeadowRange(System.Random random,float min,float max)=>
            Mathf.Lerp(min,max,(float)random.NextDouble());

        static float MeadowHillMask(Vector2 point,Vector2 center,Vector2 radius)
        {
            Vector2 q=new Vector2((point.x-center.x)/radius.x,(point.y-center.y)/radius.y);
            // Match the 64px circle texture used for the existing hills, including its soft rim.
            // A small inward margin keeps bilinear sampling inside that visible silhouette.
            return Mathf.Clamp01(30.7f-32*q.magnitude);
        }

        static float MeadowPatchCoverage(Vector2 point,MeadowPatch patch)
        {
            Vector2 p=point-patch.Center;
            float x=p.x*patch.Cos+p.y*patch.Sin;
            float y=-p.x*patch.Sin+p.y*patch.Cos;
            // Low-frequency contour bends make a loose painted area, without speckles or stripes.
            float u=(x+Mathf.Sin(y*0.021f+patch.Phase)*patch.Radius.x*0.05f)/patch.Radius.x;
            float v=(y+Mathf.Sin(x*0.017f+patch.Phase*1.7f)*patch.Radius.y*0.08f)/patch.Radius.y;
            float edge=Mathf.Clamp01((1-u*u-v*v)/0.92f);
            return edge*edge*(3-2*edge);
        }
    }
}
