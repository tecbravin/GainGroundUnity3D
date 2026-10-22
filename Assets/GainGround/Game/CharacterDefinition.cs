using UnityEngine;

namespace GainGround.Game
{
    public sealed class CharacterDefinition : ScriptableObject
    {
        public string displayName;
        public Sprite sprite;
        public Texture2D hudPortrait;
        [Tooltip("Optional 4x2 directional atlas ordered east, northeast, north, northwest, west, southwest, south, southeast.")]
        public Texture2D directionalSpriteSheet;
        [Tooltip("Original PS2 animated model. Empty retains the custom sprite.")]
        public PS2CharacterModel modelPrefab;
        [Min(0.1f)] public float speed = 3;
        [Min(0.05f)] public float shotInterval = 0.3f;
        [Min(1)] public float projectileSpeed = 8;
        [Min(0.5f)] public float range = 7;
        [Min(1)] public int damage = 1;
        public Color shotColor = Color.yellow;
        public bool specialPassesWalls;

        [Header("Original weapons (empty keeps custom character behavior)")]
        public WeaponDefinition normalWeapon;
        public WeaponDefinition specialWeapon;
        public Color characterTint = Color.white;

        [Header("Dash")]
        [Tooltip("Tempo de recarga em segundos, contado desde o inicio do dash.")]
        [Min(0)] public float TempodeCooldown = 2f;
        [Tooltip("Distancia em unidades do mundo. Zero desativa o dash.")]
        [Min(0)] public float Distancia = 2.5f;
        [Tooltip("Se marcado, o personagem fica invencivel durante o dash.")]
        public bool Invencivel = true;
    }
}
