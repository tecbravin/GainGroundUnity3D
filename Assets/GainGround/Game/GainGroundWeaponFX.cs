using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        Sprite[] waterFrames, blastFrames;
        Sprite detailedBoomerang;
        sealed class BlastVisual { public SpriteRenderer renderer; public float age; }
        readonly List<BlastVisual> blastVisuals = new List<BlastVisual>();

        void EnsureWeaponFX()
        {
            if (waterFrames != null) return;
            waterFrames = new Sprite[8]; blastFrames = new Sprite[8];
            for (int i=0;i<8;i++)
            {
                waterFrames[i]=CreateWeaponFX("Water "+i,32,(x,y)=>WaterPixel(x,y,i));
                blastFrames[i]=CreateWeaponFX("Blast "+i,48,(x,y)=>BlastPixel(x,y,i));
            }
            detailedBoomerang=CreateWeaponFX("Boomerang blade",32,(x,y)=>
            {
                float dx=x-15.5f, ridge=Mathf.Abs(dx)*.72f+8;
                float edge=Mathf.Abs(y-ridge);
                if(Mathf.Abs(dx)>13 || edge>3.3f) return Color.clear;
                if(edge>2.2f) return new Color(.12f,.19f,.26f);
                if(y>ridge+.4f) return new Color(.91f,.98f,1);
                if(Mathf.Abs(dx)<3) return new Color(.95f,.62f,.16f);
                return y<ridge-.9f?new Color(.26f,.49f,.65f):new Color(.56f,.79f,.88f);
            });
        }
        static Sprite CreateWeaponFX(string name,int size,System.Func<int,int,Color> pixel)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) {name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) colors[y*size+x]=pixel(x,y);
            texture.SetPixels(colors);texture.Apply(false,true);
            return Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,size);
        }
        static Color WaterPixel(int x,int y,int frame)
        {
            float dx=(x-15.5f)/15,dy=(y-13)/10f;
            float radius=Mathf.Sqrt(dx*dx+dy*dy),angle=Mathf.Atan2(dy,dx);
            float phase=frame*Mathf.PI/4;
            float rim=1+.035f*Mathf.Sin(angle*5+phase);
            Color color=Color.clear;
            if(radius<rim)
            {
                float wave=Mathf.Sin(radius*22-phase*2+Mathf.Sin(angle*3+phase));
                color=radius>rim-.09f?new Color(.32f,.87f,1,.85f):
                    wave>.7f?new Color(.58f,.95f,1,.9f):new Color(.04f,.36f+.12f*wave,.72f,.65f);
                if(radius>.4f && wave>.93f)color=new Color(.86f,1,1,.95f);
            }
            // Separate droplets above the pool make the freezing power readable.
            for(int i=0;i<3;i++)
            {
                float px=8+i*8+Mathf.Sin(phase+i)*2,py=24+Mathf.Sin(phase+i*2)*3;
                if(Mathf.Abs(x-px)<1.4f && Mathf.Abs(y-py)<2)color=new Color(.75f,.97f,1,.9f);
            }
            return color;
        }
        static Color BlastPixel(int x,int y,int frame)
        {
            float dx=x-23.5f,dy=y-23.5f,r=Mathf.Sqrt(dx*dx+dy*dy),angle=Mathf.Atan2(dy,dx);
            float t=frame/7f,outer=7+15*Mathf.Sin(t*Mathf.PI*.65f);
            float irregular=outer+Mathf.Sin(angle*7+frame*.7f)*2+Mathf.Sin(angle*11)*1.2f;
            if(r>irregular) return Color.clear;
            if(frame>=5)
            {
                if(r<outer*(.4f+.35f*t))return Color.clear;
                return new Color(.46f,.33f,.29f,(1-t)*1.8f);
            }
            if(r>irregular-2)return new Color(.67f,.16f,.06f,.9f);
            if(r>outer*.66f)return new Color(1,.36f,.05f);
            if(r>outer*.36f)return new Color(1,.76f,.15f);
            return new Color(1,1,.86f);
        }
        void AnimateWater(Shot shot)
        {
            EnsureWeaponFX();
            shot.visual.sprite=waterFrames[Mathf.FloorToInt(shot.age*12)%waterFrames.Length];
            shot.visual.color=new Color(1,1,1,Mathf.Min(1,(shot.weapon.duration-shot.age)*5));
        }
        void SpawnBlast(Vector2 position,float radius)
        {
            EnsureWeaponFX();
            if(blastVisuals.Count>=32)
            { if(blastVisuals[0].renderer!=null)Destroy(blastVisuals[0].renderer.gameObject);blastVisuals.RemoveAt(0); }
            var renderer=Visual("Animated explosion",blastFrames[0],position,Vector2.one*radius*2,Color.white,7);
            blastVisuals.Add(new BlastVisual {renderer=renderer});
        }
        void UpdateBlastVisuals(float dt)
        {
            for(int i=blastVisuals.Count-1;i>=0;i--)
            {
                var blast=blastVisuals[i];blast.age+=dt;
                if(blast.renderer==null || blast.age>=.48f)
                { if(blast.renderer!=null)Destroy(blast.renderer.gameObject);blastVisuals.RemoveAt(i);continue; }
                blast.renderer.sprite=blastFrames[Mathf.Min(7,(int)(blast.age/.06f))];
            }
        }
        void DisposeWeaponFX()
        {
            if(waterFrames!=null)foreach(var sprite in waterFrames) {Destroy(sprite.texture);Destroy(sprite);}
            if(blastFrames!=null)foreach(var sprite in blastFrames) {Destroy(sprite.texture);Destroy(sprite);}
            if(detailedBoomerang!=null) {Destroy(detailedBoomerang.texture);Destroy(detailedBoomerang);}
            waterFrames=blastFrames=null;detailedBoomerang=null;blastVisuals.Clear();
        }
    }
}
