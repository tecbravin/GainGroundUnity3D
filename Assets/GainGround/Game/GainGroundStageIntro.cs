using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        [Header("Apresentação da câmera no início da fase")]
        [SerializeField] bool showStageIntro = true;
        [Min(.1f)] [SerializeField] float introFocusSeconds = .7f;
        [Min(.1f)] [SerializeField] float introTravelSeconds = .9f;
        [Min(.1f)] [SerializeField] float introReturnSeconds = 1.1f;
        [Range(.35f, 1f)] [SerializeField] float introZoomFraction = .58f;

        readonly List<Vector2> introTargets = new List<Vector2>(3);
        bool stageIntroRequested;
        bool stageIntroPlaying;
        int introSegment;
        float introSegmentTime, introFullCameraSize, introFocusedCameraSize;
        int introScreenWidth, introScreenHeight;

        void BeginStageIntro()
        {
            introTargets.Clear();
            if (!stageIntroRequested || !showStageIntro || !enabled || !Application.isPlaying ||
                gameCamera == null || stage == null) return;

            if (stage.introCameraTargets != null && stage.introCameraTargets.Length > 0)
            {
                foreach (var target in stage.introCameraTargets)
                    introTargets.Add(target);
            }
            else BuildAutomaticIntroTargets();

            if (introTargets.Count == 0) return;
            introFullCameraSize = gameCamera.orthographicSize;
            introFocusedCameraSize = FocusedCameraSize(introFullCameraSize);
            introScreenWidth = Screen.width;
            introScreenHeight = Screen.height;
            introSegment = 0;
            introSegmentTime = 0f;
            stageIntroPlaying = true;
            ApplyStageIntroCamera();
        }

        void BuildAutomaticIntroTargets()
        {
            if (stage.enemies == null) return;
            var sums = new Vector2[3];
            var counts = new int[3];
            var bosses = new Vector2[3];
            var hasBoss = new bool[3];
            float height = Mathf.Max(1f, stage.size.y);
            foreach (var spawn in stage.enemies)
            {
                if (spawn == null || spawn.spawnDelay > 0f) continue;
                int band = Mathf.Clamp(Mathf.FloorToInt(
                    (spawn.position.y / height + .5f) * 3f), 0, 2);
                sums[band] += spawn.position;
                counts[band]++;
                if (spawn.boss) { bosses[band] = spawn.position; hasBoss[band] = true; }
            }
            // Like the PS2 opening sweep, show distant enemies before returning to the start.
            for (int band = 2; band >= 0; band--)
                if (counts[band] > 0)
                    introTargets.Add(hasBoss[band] ? bosses[band] : sums[band] / counts[band]);
        }

        float FocusedCameraSize(float fullSize)
        {
            return Mathf.Min(fullSize, Mathf.Max(2.8f, fullSize * introZoomFraction));
        }

        void AdvanceStageIntro(float deltaTime)
        {
            if (!stageIntroPlaying) return;
            introSegmentTime += Mathf.Max(0f, deltaTime);
            while (stageIntroPlaying)
            {
                bool hold = introSegment % 2 == 0;
                bool returning = introSegment == introTargets.Count * 2 - 1;
                float duration = hold ? introFocusSeconds :
                    returning ? introReturnSeconds : introTravelSeconds;
                if (introSegmentTime < duration) break;
                introSegmentTime -= duration;
                introSegment++;
                if (introSegment >= introTargets.Count * 2) CancelStageIntro();
            }
        }

        void ApplyStageIntroCamera()
        {
            if (!stageIntroPlaying || gameCamera == null || stage == null) return;
            if (introScreenWidth != Screen.width || introScreenHeight != Screen.height)
            {
                introScreenWidth = Screen.width;
                introScreenHeight = Screen.height;
                introFullCameraSize = gameCamera.orthographicSize;
                introFocusedCameraSize = FocusedCameraSize(introFullCameraSize);
            }

            int current = introSegment / 2;
            bool hold = introSegment % 2 == 0;
            bool returning = introSegment == introTargets.Count * 2 - 1;
            float duration = hold ? introFocusSeconds :
                returning ? introReturnSeconds : introTravelSeconds;
            float progress = hold ? 0f : Mathf.Clamp01(introSegmentTime / duration);
            float eased = progress * progress * (3f - 2f * progress);
            Vector2 start = introTargets[current];
            Vector2 end = returning ? Vector2.zero :
                hold ? start : introTargets[current + 1];
            float cameraSize = returning ?
                Mathf.Lerp(introFocusedCameraSize, introFullCameraSize, eased) :
                introFocusedCameraSize;
            Vector2 focus = Vector2.Lerp(start, end, eased);
            float xLimit = Mathf.Max(0f, stage.size.x * .5f - cameraSize * gameCamera.aspect);
            float yLimit = Mathf.Max(0f, stage.size.y * .5f - cameraSize);
            focus.x = Mathf.Clamp(focus.x, -xLimit, xLimit);
            focus.y = Mathf.Clamp(focus.y, -yLimit, yLimit);
            gameCamera.orthographicSize = cameraSize;
            gameCamera.transform.position = new Vector3(focus.x, focus.y, -10f);
        }

        void CancelStageIntro()
        {
            if (!stageIntroPlaying) return;
            stageIntroPlaying = false;
            introTargets.Clear();
            if (gameCamera == null) return;
            gameCamera.orthographicSize = introFullCameraSize;
            gameCamera.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
