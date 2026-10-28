using UnityEngine;
namespace GainGround.Game
{
    // The existing SpriteRenderer remains the gameplay visibility/position anchor.
    // forceRenderingOff hides only its drawing, preserving all death/rescue rules.
    public sealed class PS2VisualBinding : MonoBehaviour
    {
        public const float InGameCharacterScale = 1.2f;
        SpriteRenderer anchor;
        PS2CharacterModel prefab;
        public PS2CharacterModel Model { get; private set; }
        public static PS2VisualBinding Bind(SpriteRenderer anchor, CharacterDefinition hero)
        {
            var binding = anchor.GetComponent<PS2VisualBinding>();
            if (binding == null && hero.modelPrefab == null) return null;
            if (binding == null) binding = anchor.gameObject.AddComponent<PS2VisualBinding>();
            binding.anchor = anchor;
            if (binding.prefab == hero.modelPrefab && binding.Model != null) return binding;
            if (binding.Model != null) { binding.Model.gameObject.SetActive(false); Destroy(binding.Model.gameObject); }
            binding.prefab = hero.modelPrefab; binding.Model = null;
            anchor.forceRenderingOff = hero.modelPrefab != null;
            if (hero.modelPrefab != null)
            {
                binding.Model = Instantiate(hero.modelPrefab, anchor.transform);
                binding.Model.Tick(0, Vector2.down, false, false, anchor.enabled);
                binding.Sync();
            }
            return binding;
        }
        void LateUpdate() { Sync(); }
        public void Sync()
        {
            if (Model == null || anchor == null) return;
            var scale = anchor.transform.lossyScale;
            Model.transform.localScale = new Vector3(InGameCharacterScale / scale.x,
                InGameCharacterScale / scale.y, InGameCharacterScale / scale.z);
            Model.transform.localPosition = new Vector3(0, -.32f/scale.y, -.4f/scale.z);
            foreach (var renderer in Model.renderers)
            {
                renderer.enabled = anchor.enabled && anchor.color.a >= .5f;
                renderer.sortingOrder = anchor.sortingOrder;
            }
        }
    }
}
