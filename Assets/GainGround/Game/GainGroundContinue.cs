using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        const string HighScoreKey = "GainGround.HighScore";
        public int HighScore { get; private set; }
        int recordToBeat;
        bool continueNewRecord;
        int continueCount;
        float continueElapsed, gameOverElapsed;
        GUIStyle continueHeading, gameOverHeading;
        [Header("Continue screen")]
        public AudioClip gameOverSound;
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
            CancelStageIntro();
            continueNewRecord = false;
            foreach (var player in players)
                if (player.score > recordToBeat) { continueNewRecord = true; break; }
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
                if (gameOverElapsed >= Mathf.Max(2f, gameOverSound != null ? gameOverSound.length : 0f) + 4f)
                    ReturnToMenu();
                return;
            }
            if (state != ScreenState.Defeat) return;
            if (special) { RestartCurrentStage(); return; }
            continueElapsed += Mathf.Max(0,dt);
            int seconds = Mathf.FloorToInt(continueElapsed);
            continueElapsed -= seconds;
            continueCount = Mathf.Max(0,continueCount - seconds - (dash ? 1 : 0));
            if (continueCount == 0)
            {
                state = ScreenState.GameOver;
                gameOverElapsed = 0;
                PlaySfx(gameOverSound);
            }
        }
        void RestartCurrentStage()
        {
            UpdateHighScore();
            recordToBeat = HighScore;
            // Keep the campaign mode, difficulty and stage; reset both scores/parties.
            ResetCampaignParty(false); score = 0; LoadStage();
        }
        void DrawContinueScreen(Rect bounds)
        {
            DrawSelectionPanel(bounds);
            float x = bounds.x + 20, width = bounds.width - 40;
            var baseHeading = SelectionHeading;
            if (continueHeading == null || continueHeading.font != baseHeading.font)
            {
                continueHeading = new GUIStyle(baseHeading) { fontSize = 36 };
                gameOverHeading = new GUIStyle(continueHeading);
                gameOverHeading.normal.textColor = Color.red;
                gameOverHeading.hover.textColor = Color.red;
                gameOverHeading.active.textColor = Color.red;
                gameOverHeading.focused.textColor = Color.red;
            }
            var heading = state == ScreenState.GameOver ? gameOverHeading : continueHeading;
            float headingY = bounds.y + Mathf.Min(30f, bounds.height * .08f);
            float headingHeight = Mathf.Min(65f, bounds.height * .17f);
            float recordY = bounds.y + Mathf.Min(100f, bounds.height * .3f);
            GUI.Label(new Rect(x,headingY,width,headingHeight), state == ScreenState.GameOver ? "GAME OVER" : "CONTINUE?  " + continueCount, heading);
            GUI.Label(new Rect(x,recordY,width,42), "RECORDE: " + HighScore, SelectionHeading);
            float buttonHeight = Mathf.Min(48f, Mathf.Max(36f, bounds.height * .14f));
            float buttonGap = Mathf.Min(10f, bounds.height * .03f);
            float lastButtonY = bounds.yMax - Mathf.Max(20f, bounds.height * .05f) - buttonHeight;
            float firstButtonY = lastButtonY - buttonGap - buttonHeight;
            if (continueNewRecord)
                GUI.Label(new Rect(x, Mathf.Min(recordY + 46f, firstButtonY - 40f), width, 34f),
                    "NOVO RECORDE!!!", SelectionHeading);
            if (state == ScreenState.GameOver) return;
            float buttonInset = Mathf.Min(bounds.width * .085f + 20f, bounds.width * .35f);
            float buttonX = bounds.x + buttonInset;
            float buttonWidth = bounds.width - 2f * buttonInset;
            if (IronButton(new Rect(buttonX,firstButtonY,buttonWidth,buttonHeight), "ESPECIAL — CONTINUAR")) RestartCurrentStage();
            if (IronButton(new Rect(buttonX,lastButtonY,buttonWidth,buttonHeight), "DASH — CONTADOR -1")) AdvanceContinue(0,true,false);
        }
    }
}
