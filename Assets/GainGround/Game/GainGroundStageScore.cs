using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        const float StageScorePresentationDuration = 2.4f;

        struct StageScoreEntry
        {
            public string label;
            public int points;
        }

        readonly List<StageScoreEntry> stageScoreEntries = new List<StageScoreEntry>(4);
        [Header("Stage score audio")]
        public AudioClip scoreCountSound;
        float stageScoreElapsed;
        float scoreCountEndTime;
        float nextScoreCountSound;
        bool pendingEndingScore;

        void ResetStageScorePresentation()
        {
            stageScoreEntries.Clear();
            stageScoreElapsed = 0;
            scoreCountEndTime = 0;
            nextScoreCountSound = 0;
            pendingEndingScore = false;
        }

        void AddStageScore(string label, int points)
        {
            stageScoreEntries.Add(new StageScoreEntry { label = label, points = points });
            AwardPoints(null, points);
        }

        void AddSpecialStageBonus(string label, int basePoints)
        {
            AddStageScore(label, DifficultyRules.ScorePoints(campaignDifficulty, basePoints));
        }

        void BuildStageScore()
        {
            stageScoreEntries.Clear();
            stageScoreElapsed = 0;
            scoreCountEndTime = 0;
            nextScoreCountSound = 0;
            mobileScroll = Vector2.zero;

            int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingTime));
            AddStageScore("TEMPO " + seconds + "s × 100", seconds * 100);

            bool hasCaptives = currentRescue != null || extraCaptives.Count > 0;
            bool allRescued = (currentRescue == null || rescued) &&
                extraCaptives.TrueForAll(captive => captive.secured);
            if (hasCaptives && allRescued)
                AddSpecialStageBonus("TODOS RESGATADOS", 10000);

            bool allEscaped = players.Count > 0 &&
                players.TrueForAll(player => player.party.Count > 0 &&
                    player.party.TrueForAll(member => member.escaped));
            if (allEscaped)
                AddSpecialStageBonus("TODOS ESCAPARAM", 8000);

            if (stage.enemies.Length > 0 && enemies.Count == 0)
                AddSpecialStageBonus("TODOS INIMIGOS DERROTADOS", 20000);

            for (int i = 0; i < stageScoreEntries.Count; i++)
                if (stageScoreEntries[i].points > 0)
                    scoreCountEndTime = i * .22f + .8f;
        }

        void TickScoreCountSound()
        {
            if (scoreCountSound == null || stageScoreElapsed >= scoreCountEndTime ||
                stageScoreElapsed < nextScoreCountSound) return;
            PlaySfx(scoreCountSound);
            nextScoreCountSound = stageScoreElapsed + .06f;
        }

        string StageScorePoints(int index)
        {
            var entry = stageScoreEntries[index];
            float progress = Mathf.Clamp01((stageScoreElapsed - index * .22f) / .8f);
            int displayed = Mathf.RoundToInt(entry.points * Mathf.SmoothStep(0, 1, progress));
            return "+" + displayed.ToString("N0");
        }

        void DrawStageScoreSummary()
        {
            var bonusLabelStyle = new GUIStyle(GoldMenuText(labelStyle))
            {
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            var bonusPointsStyle = new GUIStyle(bonusLabelStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            int fontSize = bonusLabelStyle.fontSize;

            for (int i = 0; i < stageScoreEntries.Count; i++)
            {
                var labelRect = new Rect(245, 251 + i * 42, 350, 34);
                var content = new GUIContent(stageScoreEntries[i].label);
                bonusLabelStyle.fontSize = fontSize;
                while (bonusLabelStyle.fontSize > 6 &&
                       bonusLabelStyle.CalcSize(content).x > labelRect.width)
                    bonusLabelStyle.fontSize--;

                GUI.Label(labelRect, content, bonusLabelStyle);
                var pointsRect = new Rect(610, labelRect.y, 145, labelRect.height);
                bonusPointsStyle.fontSize = fontSize;
                var finalPoints = new GUIContent("+" + stageScoreEntries[i].points.ToString("N0"));
                while (bonusPointsStyle.fontSize > 6 &&
                       bonusPointsStyle.CalcSize(finalPoints).x > pointsRect.width)
                    bonusPointsStyle.fontSize--;
                GUI.Label(pointsRect, StageScorePoints(i), bonusPointsStyle);
            }
        }

        void DrawMobileStageScoreSummary()
        {
            for (int i = 0; i < stageScoreEntries.Count; i++)
                GUILayout.Label(stageScoreEntries[i].label + "   " + StageScorePoints(i),
                    GoldMenuText(mobileLabel), GUILayout.MinHeight(34));
        }
    }
}
