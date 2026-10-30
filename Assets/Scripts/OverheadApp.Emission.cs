using UnityEngine;
using UnityEngine.Rendering;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        struct SpiritHalo
        {
            public Texture2D Texture,Bloom;
            public Rect Bounds;
        }

        readonly SpiritHalo[] spiritHalos=new SpiritHalo[6];

        void InitializeSpiritEmission()
        {
            // Headless tests do not have a graphics device. In the player, all six
            // expressions are prepared before play, never on a food/light arrival.
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            for(int side=0;side<2;side++)
            {
                Monster monster=side==0 ? leftMonster : rightMonster;
                for(int expression=0;expression<3;expression++)
                {
                    Texture2D face=monster.Faces[expression];
                    if(face==null)continue;
                    bool leftHappy=side==0&&expression==1&&face!=monster.Faces[0];
                    Rect crop=leftHappy ? LeftHappyCrop : monster.Crop;
                    Rect sprite=MonsterFaceRect(monster.Crop,crop,leftHappy ? LeftHappyOffset : Vector2.zero);
                    spiritHalos[side*3+expression]=CreateSpiritHalo(face,crop,sprite);
                }
            }
        }

        static SpiritHalo CreateSpiritHalo(Texture2D source,Rect crop,Rect sprite)
        {
            // Read only a small crop through the GPU; the original illustrations stay
            // non-readable. Padding lets the blurred silhouette extend beyond the PNG.
            // Leave enough transparent space for the light to spread well beyond the
            // body, rather than looking like a thin outline or a stretched duplicate.
            const int padding=64;
            int contentWidth=Mathf.CeilToInt(sprite.width*0.5f);
            int contentHeight=Mathf.CeilToInt(sprite.height*0.5f);
            int width=contentWidth+padding*2,height=contentHeight+padding*2;
            RenderTexture previous=RenderTexture.active;
            RenderTexture small=RenderTexture.GetTemporary(contentWidth,contentHeight,0,
                RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Texture2D readable=null;
            Color32[] pixels;
            try
            {
                Graphics.Blit(source,small,new Vector2(crop.width/source.width,crop.height/source.height),
                    new Vector2(crop.x/source.width,1-crop.yMax/source.height));
                RenderTexture.active=small;
                readable=new Texture2D(contentWidth,contentHeight,TextureFormat.RGBA32,false,true);
                readable.ReadPixels(new Rect(0,0,contentWidth,contentHeight),0,0,false);
                pixels=readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(small);
                if(readable!=null)Destroy(readable);
            }

            var alpha=new float[width*height];
            var scratch=new float[alpha.Length];
            for(int y=0;y<contentHeight;y++)for(int x=0;x<contentWidth;x++)
                alpha[(y+padding)*width+x+padding]=pixels[y*contentWidth+x].a/255f;
            // Three small separable box blurs approximate a soft glow in linear time.
            // These arrays and texture are created once; drawing only reuses the result.
            for(int pass=0;pass<3;pass++)
            {
                BlurSpiritAlpha(alpha,scratch,width,height,true,3);
                BlurSpiritAlpha(scratch,alpha,width,height,false,3);
            }
            Texture2D texture=CreateSpiritGlowTexture(alpha,width,height,"Rim "+source.name);
            // A separate, much softer silhouette gives the impression of emitted light.
            // Both blur sizes are baked here, before the first food is spawned.
            for(int pass=0;pass<3;pass++)
            {
                BlurSpiritAlpha(alpha,scratch,width,height,true,15);
                BlurSpiritAlpha(scratch,alpha,width,height,false,15);
            }
            Texture2D bloom=CreateSpiritGlowTexture(alpha,width,height,"Bloom "+source.name);
            float padX=padding*sprite.width/contentWidth,padY=padding*sprite.height/contentHeight;
            return new SpiritHalo
            {
                Texture=texture,Bloom=bloom,
                Bounds=new Rect(sprite.x-padX,sprite.y-padY,sprite.width+2*padX,sprite.height+2*padY)
            };
        }

        static Texture2D CreateSpiritGlowTexture(float[] alpha,int width,int height,string name)
        {
            var colors=new Color32[alpha.Length];
            for(int i=0;i<colors.Length;i++)colors[i]=new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha[i]*255));
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="NAISPO Spirit "+name,
                filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave
            };
            texture.SetPixels32(colors);
            texture.Apply(false,true);
            return texture;
        }

        static void BlurSpiritAlpha(float[] source,float[] target,int width,int height,bool horizontal,int radius)
        {
            int diameter=radius*2+1;
            int length=horizontal ? width : height,lines=horizontal ? height : width;
            int stride=horizontal ? 1 : width;
            for(int line=0;line<lines;line++)
            {
                int start=horizontal ? line*width : line;
                float sum=0;
                for(int position=0;position<=radius;position++)sum+=source[start+position*stride];
                for(int position=0;position<length;position++)
                {
                    target[start+position*stride]=sum/diameter;
                    int remove=position-radius,add=position+radius+1;
                    if(remove>=0)sum-=source[start+remove*stride];
                    if(add<length)sum+=source[start+add*stride];
                }
            }
        }

        static Color SpiritLightColor(bool right) => right ? Rgb(0x90BAFF) : Rgb(0xFFB47B);

        void DrawSpiritHalo(MonsterVisualPose pose,bool right)
        {
            SpiritHalo halo=spiritHalos[(right ? 3 : 0)+pose.Expression];
            if(halo.Texture==null)return;
            Color previous=GUI.color;
            Color light=SpiritLightColor(right);
            float breath=0.93f+0.07f*Mathf.Sin(playTime*1.5f+(right ? 1.7f : 0));
            try
            {
                // Color diffuses into the air while the pale near rim reads as its
                // source. The illustration is drawn afterwards, preserving the face.
                Rect broad=halo.Bounds;
                broad.xMin-=22;broad.xMax+=22;broad.yMin-=22;broad.yMax+=22;
                GUI.color=WithAlpha(light,0.32f*breath);
                GUI.DrawTexture(broad,halo.Bloom,ScaleMode.StretchToFill,true);
                GUI.color=WithAlpha(light,0.88f*breath);
                GUI.DrawTexture(halo.Bounds,halo.Bloom,ScaleMode.StretchToFill,true);
                GUI.color=WithAlpha(right ? Rgb(0xDDF3FF) : Rgb(0xFFF0C9),breath);
                GUI.DrawTexture(halo.Bounds,halo.Texture,ScaleMode.StretchToFill,true);
            }
            finally { GUI.color=previous; }
        }

        void DrawSpiritFloorLight(MonsterVisualPose pose,bool right)
        {
            // The reflection stays on the floor as its owner hops, and fades with height.
            float heightFade=1-pose.Jump/90;
            Glow(new Rect(pose.GroundX-pose.Width*0.90f,MonsterFeetY-40,pose.Width*1.80f,116),
                WithAlpha(SpiritLightColor(right),0.45f*heightFade));
            Glow(new Rect(pose.GroundX-pose.Width*0.56f,MonsterFeetY-17,pose.Width*1.12f,46),
                WithAlpha(right ? Rgb(0xDDF3FF) : Rgb(0xFFF0C9),0.23f*heightFade));
        }

        void DestroySpiritEmission()
        {
            for(int i=0;i<spiritHalos.Length;i++)
            {
                if(spiritHalos[i].Texture!=null)Destroy(spiritHalos[i].Texture);
                if(spiritHalos[i].Bloom!=null)Destroy(spiritHalos[i].Bloom);
            }
        }
    }
}
