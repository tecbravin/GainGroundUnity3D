using UnityEngine;

namespace GainGround.Game
{
    // Brief visual for the optional muzzle and impact sprites configured on enemy prefabs.
    public sealed class EnemyEffectLifetime : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        GainGroundGame owner;
        Sprite[] frames;
        float framesPerSecond;
        float age;

        public void Initialize(SpriteRenderer renderer, GainGroundGame game, Sprite[] animationFrames = null,
            float animationFramesPerSecond = 45)
        {
            spriteRenderer = renderer; owner = game; age = 0;
            frames = animationFrames; framesPerSecond = animationFramesPerSecond;
        }

        void OnDisable()
        {
            frames = null;
            spriteRenderer = null;
            owner = null;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= .22f)
            {
                if (owner != null) owner.ReleaseEnemyEffectVisual(spriteRenderer);
                else Destroy(gameObject);
                return;
            }
            if (spriteRenderer == null) return;
            if (frames != null && frames.Length > 0)
            {
                int frame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age * framesPerSecond));
                if (frames[frame] != null) spriteRenderer.sprite = frames[frame];
            }
            var color = spriteRenderer.color;
            color.a = 1f - age / .22f;
            spriteRenderer.color = color;
        }
    }
}
