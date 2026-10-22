using UnityEngine;

namespace GainGround.Game
{
    // Brief visual for the optional muzzle and impact sprites configured on enemy prefabs.
    public sealed class EnemyEffectLifetime : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        float age;

        public void Initialize(SpriteRenderer renderer) { spriteRenderer = renderer; }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= .22f) { Destroy(gameObject); return; }
            if (spriteRenderer == null) return;
            var color = spriteRenderer.color;
            color.a = 1f - age / .22f;
            spriteRenderer.color = color;
        }
    }
}
