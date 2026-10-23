using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        const float EndingAnimationDuration = 7.5f;
        const float EndingMessageDuration = 5f;
        float endingElapsed;
        float endingMessageElapsed;
        bool endingAnimationPlaying;
        Transform endingRoot;
        SpriteRenderer endingComputerAnchor;
        PS2EnemyVisual endingComputer;
        readonly Transform[] endingHeroes = new Transform[3];
        readonly PS2CharacterModel[] endingHeroModels = new PS2CharacterModel[3];
        Texture2D endingBurst;
        GUIStyle endingMessageStyle;

        void BeginEndingAnimation()
        {
            endingElapsed = 0;
            endingMessageElapsed = 0;
            endingAnimationPlaying = true;
            if (arena != null) arena.gameObject.SetActive(false);
            if (gameCamera != null)
            {
                gameCamera.rect = new Rect(0, 0, 1, 1);
                gameCamera.clearFlags = CameraClearFlags.SolidColor;
                gameCamera.backgroundColor = new Color(.012f, .016f, .026f);
                gameCamera.transform.position = new Vector3(0, 0, -10);
                gameCamera.orthographic = true;
                gameCamera.orthographicSize = 4.5f;
            }

            if (endingRoot != null) Destroy(endingRoot.gameObject);
            endingRoot = new GameObject("PS2 Ending Animation").transform;
            endingRoot.SetParent(transform, false);
            var systemSpawn = FindEndingComputerSpawn();
            if (systemSpawn != null && systemSpawn.modelPrefab != null)
            {
                endingComputerAnchor = MakeEndingAnchor("Gain Ground System", systemSpawn.sprite,
                    new Vector3(0, 1.4f, 0), systemSpawn.visualSize, 120);
                endingComputer = PS2EnemyVisual.Bind(endingComputerAnchor, systemSpawn);
                if (endingComputer != null) endingComputer.turnWithTarget = false;
            }

            for (int i = 0; i < endingHeroes.Length; i++)
            {
                if (startingParty == null || i >= startingParty.Length || startingParty[i] == null) continue;
                var hero = startingParty[i];
                float x = (i - 1) * 1.15f;
                var anchor = MakeEndingAnchor("Ending Hero " + hero.displayName, hero.sprite,
                    new Vector3(x, -2.5f, 0), new Vector2(.75f, .9f), 130 + i);
                endingHeroes[i] = anchor.transform;
                endingHeroModels[i] = PS2VisualBinding.Bind(anchor, hero)?.Model;
            }
        }

        EnemySpawn FindEndingComputerSpawn()
        {
            if (stages == null) return null;
            foreach (var candidateStage in stages)
            {
                if (candidateStage == null || candidateStage.enemies == null) continue;
                foreach (var spawn in candidateStage.enemies)
                    if (spawn != null && spawn.behavior == EnemyBehavior.Computer) return spawn;
            }
            return null;
        }

        SpriteRenderer MakeEndingAnchor(string name, Sprite sprite, Vector3 position, Vector2 size, int order)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(endingRoot, false);
            actor.transform.localPosition = position;
            actor.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        void TickEndingAnimation(float dt)
        {
            endingElapsed += Mathf.Max(0, dt);
            float approach = Mathf.Clamp01(endingElapsed / 2f);
            for (int i = 0; i < endingHeroes.Length; i++)
            {
                var hero = endingHeroes[i];
                if (hero == null) continue;
                float x = (i - 1) * 1.15f;
                hero.localPosition = new Vector3(x, Mathf.Lerp(-2.5f, -1.65f, Mathf.SmoothStep(0, 1, approach)), 0);
                if (endingHeroModels[i] != null)
                    endingHeroModels[i].Tick(dt, new Vector2(-x, 3), endingElapsed < 2f, false, true);
            }

            if (endingComputerAnchor != null)
            {
                float shake = endingElapsed >= 2.1f && endingElapsed < 4.5f ? Mathf.Lerp(.025f, .18f, Mathf.PingPong(endingElapsed * 10, 1)) : 0;
                endingComputerAnchor.transform.localPosition = new Vector3(Mathf.Sin(endingElapsed * 43) * shake, 1.4f, 0);
                if (endingElapsed >= 4.4f) endingComputerAnchor.enabled = false;
                if (endingComputer != null) endingComputer.Tick(dt, Vector2.down, false);
            }

            if (endingElapsed >= 2.1f && endingElapsed < 4.5f && endingElapsed - dt < 2.1f)
                PlaySfx(explosionSound != null ? explosionSound : impactSound);
            if (endingElapsed >= EndingAnimationDuration)
            {
                hasFinished = true;
                PlayerPrefs.SetInt("GainGround.HasFinished", 1);
                PlayerPrefs.Save();
                endingAnimationPlaying = false;
                message = "Você terminou o jogo! Divirta-se agora com todos os personagens!";
            }
        }

        void DrawEndingScreen(Vector2 size)
        {
            if (state == ScreenState.Victory && !endingAnimationPlaying)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, size.x, size.y), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (endingMessageStyle == null)
                {
                    endingMessageStyle = new GUIStyle(GUI.skin.label)
                    {
                        font = SelectionHeading.font,
                        fontSize = Mathf.RoundToInt(size.y * .045f),
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        fontStyle = FontStyle.Bold
                    };
                    endingMessageStyle.normal.textColor = new Color(1f, .79f, .28f);
                }
                GUI.Label(new Rect(size.x * .12f, size.y * .34f, size.x * .76f, size.y * .32f),
                    "Você terminou o jogo!\nDivirta-se agora com todos os personagens!", endingMessageStyle);
                return;
            }

            float fade = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.2f, EndingAnimationDuration, endingElapsed));
            if (endingBurst == null) endingBurst = MakeEndingBurst();
            if (endingElapsed >= 2.1f && endingElapsed < 4.8f)
            {
                float progress = Mathf.InverseLerp(2.1f, 4.8f, endingElapsed);
                float diameter = Mathf.Lerp(size.y * .1f, size.y * 1.8f, progress);
                var flashColor = new Color(1f, .52f, .12f, (1f - progress) * .88f);
                GUI.color = flashColor;
                GUI.DrawTexture(new Rect(size.x * .5f - diameter * .5f, size.y * .42f - diameter * .5f, diameter, diameter), endingBurst);
                GUI.color = Color.white;
            }
            if (fade > 0)
            {
                GUI.color = new Color(0, 0, 0, fade);
                GUI.DrawTexture(new Rect(0, 0, size.x, size.y), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        Texture2D MakeEndingBurst()
        {
            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            { name = "Ending Explosion Glow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
            {
                float dx = (x + .5f) / resolution * 2 - 1;
                float dy = (y + .5f) / resolution * 2 - 1;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy)), 2.2f);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            return texture;
        }

        void DestroyEndingAnimation()
        {
            if (endingRoot != null) Destroy(endingRoot.gameObject);
            endingRoot = null; endingComputer = null; endingComputerAnchor = null;
            endingAnimationPlaying = false;
            endingMessageElapsed = 0;
            for (int i = 0; i < endingHeroes.Length; i++) { endingHeroes[i] = null; endingHeroModels[i] = null; }
        }
    }
}
