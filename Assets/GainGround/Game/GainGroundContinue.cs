using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        const string HighScoreKey = "GainGround.HighScore";
        public int HighScore { get; private set; }
        int continueCount;
        float continueElapsed, gameOverElapsed;
        void LoadHighScore() { HighScore = Mathf.Max(0, PlayerPrefs.GetInt(HighScoreKey, 0)); }
        void UpdateHighScore()
        {
            int best = HighScore;
            foreach (var p in players) best = Mathf.Max(best, p.score);
            if (best <= HighScore) return;
            HighScore = best;
            PlayerPrefs.SetInt(HighScoreKey, best);
            PlayerPrefs.Save();
        }
        void BeginContinue(string reason)
        {
            UpdateHighScore();
            state = ScreenState.Defeat; paused = false; message = reason;
            continueCount = 10; continueElapsed = 0; gameOverElapsed = 0;
            ResetMobileInput();
        }
        void UpdateContinueInput()
        {
            bool special = ActionDown(0,GameAction.Special);
            bool dash = ActionDown(0,GameAction.Dash);
            if(players.Count>1){special|=ActionDown(1,GameAction.Special);dash|=ActionDown(1,GameAction.Dash);}
            AdvanceContinue(Time.unscaledDeltaTime, dash, special);
        }
        void AdvanceContinue(float dt, bool dash, bool special)
        {
            if (state == ScreenState.GameOver)
            {
                gameOverElapsed += dt;
                if (gameOverElapsed >= 2) ReturnToMenu();
                return;
            }
            if (state != ScreenState.Defeat) return;
            if (special) { RestartCurrentStage(); return; }
            continueElapsed += Mathf.Max(0,dt);
            int seconds = Mathf.FloorToInt(continueElapsed);
            continueElapsed -= seconds;
            continueCount = Mathf.Max(0,continueCount - seconds - (dash ? 1 : 0));
            if (continueCount == 0) { state = ScreenState.GameOver; gameOverElapsed = 0; }
        }
        void RestartCurrentStage()
        {
            UpdateHighScore();
            // Keep the campaign mode, difficulty and stage; reset both scores/parties.
            ResetCampaignParty(false); score = 0; LoadStage();
        }
        void DrawContinueScreen(Rect bounds)
        {
            DrawSelectionPanel(bounds);
            float x = bounds.x + 20, width = bounds.width - 40;
            var heading = new GUIStyle(SelectionHeading) { fontSize = 36 };
            if (state == ScreenState.GameOver) heading.normal.textColor = Color.red;
            GUI.Label(new Rect(x,bounds.y+30,width,65), state == ScreenState.GameOver ? "GAME OVER" : "CONTINUE?  " + continueCount, heading);
            GUI.Label(new Rect(x,bounds.y+100,width,50), "RECORDE: " + HighScore, SelectionHeading);
            if (state == ScreenState.GameOver) return;
            GUI.Label(new Rect(x,bounds.y+150,width,70), "Especial: continuar na mesma fase\nDash: diminuir o contador", SelectionHeading);
            if (IronButton(new Rect(x,bounds.y+230,width,48), "ESPECIAL — CONTINUAR")) RestartCurrentStage();
            if (IronButton(new Rect(x,bounds.y+286,width,48), "DASH — CONTADOR -1")) AdvanceContinue(0,true,false);
        }
    }
}
