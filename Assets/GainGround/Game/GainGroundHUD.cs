using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        [Header("PS2 HUD artwork")]
        public Texture2D hudScore, hudPlayerFrame, hudTimeEnemy;
        GUIStyle hudNumber, hudName, hudCaption;

        void EnsureHUDStyles()
        {
            if (hudNumber != null) return;
            hudNumber = new GUIStyle(labelStyle) { fontSize=27, fontStyle=FontStyle.Bold, wordWrap=false };
            hudName = new GUIStyle(labelStyle) { alignment=TextAnchor.MiddleLeft, fontSize=16, wordWrap=false };
            hudCaption = new GUIStyle(smallStyle) { alignment=TextAnchor.MiddleLeft, fontSize=12 };
        }
        static void HUDRect(Rect rect, Color color)
        {
            var previous=GUI.color; GUI.color=color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=previous;
        }
        static void HUDTexture(Rect rect,Texture2D texture,Rect uv)
        {
            if(texture!=null) GUI.DrawTextureWithTexCoords(rect,texture,uv,true);
        }
        void DrawPS2Header()
        {
            EnsureHUDStyles();
            HUDRect(new Rect(15,8,970,80),new Color(.035f,.045f,.06f,.96f));
            HUDTexture(new Rect(28,12,190,68),hudScore,new Rect(0,0,.5f,1));
            GUI.Label(new Rect(46,30,155,30),score.ToString("D6"),hudNumber);
            GUI.Label(new Rect(230,15,155,25),"PONTOS DA EQUIPE",hudCaption);
            GUI.Label(new Rect(230,40,155,30),players.Count==2?"2 JOGADORES":"1 JOGADOR",hudCaption);
            HUDTexture(new Rect(397,9,206,76),hudTimeEnemy,new Rect(0,0,1,1));
            var previous=GUI.color;
            if(remainingTime<=20) GUI.color=new Color(1,.35f,.25f);
            GUI.Label(new Rect(403,36,89,32),Mathf.Max(0,Mathf.CeilToInt(remainingTime)).ToString("D3"),hudNumber);
            GUI.color=previous;
            GUI.Label(new Rect(510,36,87,32),enemies.Count.ToString("D2"),hudNumber);
            GUI.Label(new Rect(625,12,345,31),stage.displayName,hudName);
            GUI.Label(new Rect(625,43,345,27),(ClassicMode?"CLASSIC  |  ":"")+"P / Esc: pausa    R: reiniciar",hudCaption);
            HUDRect(new Rect(15,90,970,23),new Color(.025f,.03f,.04f,.9f));
            GUI.Label(new Rect(22,90,956,23),message,smallStyle);
        }
        void DrawPS2Player(PlayerState p,float x)
        {
            HUDRect(new Rect(x,585,480,108),new Color(.045f,.05f,.065f,.97f));
            HUDTexture(new Rect(x,585,480,108),hudPlayerFrame,new Rect(p.id*.5f,0,.5f,1));
            var hero=p.HasActive?p.party[p.active].definition:null;
            if(hero!=null && hero.hudPortrait!=null)
                GUI.DrawTexture(new Rect(x+7,588,69,86),hero.hudPortrait,ScaleMode.ScaleToFit,true);
            else if(hero!=null && hero.sprite!=null)
            {
                var sprite=hero.sprite;var t=sprite.texture;var r=sprite.textureRect;
                HUDTexture(new Rect(x+22,602,40,53),t,new Rect(r.x/t.width,r.y/t.height,r.width/t.width,r.height/t.height));
            }
            string status=p.awaitingSelection?"SELECIONE O CAMPEÃO":hero!=null?hero.displayName:
                p.party.Exists(m=>!m.lost)?"EQUIPE EVACUADA":"AGUARDANDO RESGATE";
            GUI.Label(new Rect(x+85,589,275,25),"P"+(p.id+1)+"  "+status,hudName);
            int available=0,escaped=0;
            foreach(var member in p.party) { if(member.escaped) escaped++;else if(!member.lost) available++; }
            GUI.Label(new Rect(x+85,615,285,20),"PTS "+p.score+" | EQUIPE "+available+" | SALVOS "+escaped+(p.carrying?"   |   RESGATANDO":""),hudCaption);
            // Compact roster remains readable with all 20 characters and repeated rescues.
            for(int i=0;i<Mathf.Min(p.party.Count,30);i++)
            {
                var member=p.party[i];float bx=x+85+i*9;
                HUDRect(new Rect(bx,642,7,10),member.lost?new Color(.3f,.3f,.3f):member.escaped?new Color(.3f,.9f,.55f):p.Tint);
                if(i==p.active) HUDRect(new Rect(bx,655,7,2),Color.white);
            }
            float cooldown=p.HasActive?p.party[p.active].dashCooldown:0;
            bool enabled=GUI.enabled;
            GUI.enabled=state==ScreenState.Playing && !paused && p.HasActive && cooldown<=0 && p.dashRemaining<=0 && hero.Distancia>0;
            if(IronButton(new Rect(x+365,596,105,32),cooldown>0?"DASH "+cooldown.ToString("F1")+"s":"DASH ["+(p.id==0?"C":"L")+"]")) p.dashRequested=true;
            GUI.enabled=enabled;
            float ready=hero==null?0:hero.TempodeCooldown<=0?1:1-Mathf.Clamp01(cooldown/hero.TempodeCooldown);
            HUDRect(new Rect(x+366,633,103,5),new Color(.12f,.13f,.16f));
            HUDRect(new Rect(x+366,633,103*ready,5),p.Tint);
            
        }
    }
}
