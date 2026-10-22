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
        float stageScoreElapsed;
        bool pendingEndingScore;

        void ResetStageScorePresentation()
        {
            stageScoreEntries.Clear();
            stageScoreElapsed = 0;
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
                AddSpecialStageBonus("TODOS OS INIMIGOS DERROTADOS", 20000);
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
            for (int i = 0; i < stageScoreEntries.Count; i++)
            {
                GUI.Label(new Rect(245, 251 + i * 38, 370, 33),
                    stageScoreEntries[i].label, GoldMenuText(labelStyle));
                GUI.Label(new Rect(610, 251 + i * 38, 145, 33),
                    StageScorePoints(i), GoldMenuText(labelStyle));
            }
        }

        void DrawMobileStageScoreSummary()
        {
            for (int i = 0; i < stageScoreEntries.Count; i++)
                GUILayout.Label(stageScoreEntries[i].label + "   " + StageScorePoints(i),
                    GoldMenuText(mobileLabel), GUILayout.Height(34));
        }
    }
}
