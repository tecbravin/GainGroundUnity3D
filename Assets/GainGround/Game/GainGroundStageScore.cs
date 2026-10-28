using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        const float StageScorePresentationDuration = 2.4f;
        const float StageScoreEntryDuration = .52f;
        const float StageScoreEntryInterval = .57f;

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
            mobileGameScroll = Vector2.zero;

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
                    scoreCountEndTime = i * StageScoreEntryInterval + StageScoreEntryDuration;
        }

        void TickScoreCountSound()
        {
            if (scoreCountSound == null || stageScoreElapsed >= scoreCountEndTime ||
                stageScoreElapsed < nextScoreCountSound) return;
            bool counting = false;
            for (int i = 0; i < stageScoreEntries.Count; i++)
            {
                float elapsed = stageScoreElapsed - i * StageScoreEntryInterval;
                if (stageScoreEntries[i].points > 0 && elapsed >= 0 && elapsed < StageScoreEntryDuration)
                {
                    counting = true;
                    break;
                }
            }
            if (!counting) return;
            PlaySfx(scoreCountSound);
            nextScoreCountSound = stageScoreElapsed + .06f;
        }

        string StageScorePoints(int index)
        {
            var entry = stageScoreEntries[index];
            float progress = Mathf.Clamp01((stageScoreElapsed - index * StageScoreEntryInterval) /
                StageScoreEntryDuration);
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
                alignment = TextAnchor.MiddleCenter
            };
            int fontSize = bonusLabelStyle.fontSize;

            for (int i = 0; i < stageScoreEntries.Count; i++)
            {
                var labelRect = new Rect(255, 248 + i * 37, 315, 34);
                var content = new GUIContent(stageScoreEntries[i].label);
                bonusLabelStyle.fontSize = fontSize;
                while (bonusLabelStyle.fontSize > 6 &&
                       bonusLabelStyle.CalcSize(content).x > labelRect.width)
                    bonusLabelStyle.fontSize--;

                GUI.Label(labelRect, content, bonusLabelStyle);
                var pointsRect = new Rect(580, labelRect.y, 130, labelRect.height);
                bonusPointsStyle.fontSize = fontSize;
                var finalPoints = new GUIContent("+" + stageScoreEntries[i].points.ToString("N0"));
                while (bonusPointsStyle.fontSize > 6 &&
                       bonusPointsStyle.CalcSize(finalPoints).x > pointsRect.width)
                    bonusPointsStyle.fontSize--;
                GUI.Label(pointsRect, StageScorePoints(i), bonusPointsStyle);
            }
        }

        float MobileStageScoreSummaryHeight(float width)
        {
            var style = GoldMenuText(mobileLabel);
            float height = 0;
            for (int i = 0; i < stageScoreEntries.Count; i++)
            {
                height += Mathf.Max(30, style.CalcHeight(new GUIContent(stageScoreEntries[i].label), width)) + 3;
                height += Mathf.Max(28, style.CalcHeight(new GUIContent("+" + stageScoreEntries[i].points.ToString("N0")), width)) + 12;
            }
            return height;
        }

        float DrawMobileStageScoreSummary(float y, float width)
        {
            var style = GoldMenuText(mobileLabel);
            for (int i = 0; i < stageScoreEntries.Count; i++)
            {
                string label = stageScoreEntries[i].label;
                float labelHeight = Mathf.Max(30, style.CalcHeight(new GUIContent(label), width));
                GUI.Label(new Rect(0, y, width, labelHeight), label, style);
                y += labelHeight + 3;
                string points = StageScorePoints(i);
                float pointsHeight = Mathf.Max(28, style.CalcHeight(new GUIContent(points), width));
                GUI.Label(new Rect(0, y, width, pointsHeight), points, style);
                y += pointsHeight + 12;
            }
            return y;
        }
    }
}
