using System;
using UnityEngine;

namespace GainGround.Game
{
    public enum WeaponDirection { Facing, North, Horizontal, SixWay }
    public enum ProjectileKind { Bullet, Spear, Arrow, Magic, Grenade, Boomerang, Flame, Rocket, Tornado, FireTrap, WaterTrap, Orbit }
    public enum WeaponMode { Projectile, MeleeArc, ReflectEnemyProjectiles }

    [Serializable]
    public sealed class WeaponDefinition
    {
        public string label;
        public AudioClip fireSound;
        public WeaponDirection direction;
        public ProjectileKind kind;
        [Min(.05f)] public float cooldown = .3f;
        [Min(.1f)] public float speed = 8;
        [Min(.1f)] public float range = 7;
        [Min(0)] public int damage = 1;
        [Min(.01f)] public float radius = .12f;
        [Min(1)] public int maxActive = 3;
        public bool lobbed, pierces;
        [Min(0)] public float blastRadius, freezeSeconds;
        [Min(.1f)] public float duration = 2;
        public Color color = Color.yellow;
        [Header("Melee and deflection")]
        public WeaponMode mode;
        [Range(1, 360)] public float arcDegrees = 120;
        [Header("Optional animated visual")]
        public Texture2D effectSpriteSheet;
        [Tooltip("SpriteRenderer prefab used for this projectile when no animation sheet is set.")]
        public GameObject projectileVisualPrefab;
        [Tooltip("Optional original PS2 projectile animation frames.")]
        public Sprite[] projectileFrames;
        [Tooltip("SpriteRenderer prefab briefly shown when the projectile ends or hits.")]
        public GameObject impactVisualPrefab;
        [Tooltip("Optional original PS2 animation frames for the impact prefab.")]
        public Sprite[] impactFrames;
        [Min(1)] public float impactFramesPerSecond = 45;
        [Min(1)] public int effectColumns = 3, effectRows = 2;
        [Min(0)] public float effectDuration;
        [Min(.1f)] public float effectScale = 1;
        [Min(1)] public float projectileFramesPerSecond = 12;

        public int ProjectileCount => direction == WeaponDirection.SixWay ? 6 :
            kind == ProjectileKind.Orbit ? 4 : direction == WeaponDirection.Horizontal ? 2 : 1;

        public Vector2 Direction(Vector2 facing, int index)
        {
            if (direction == WeaponDirection.Horizontal) return index == 0 ? Vector2.left : Vector2.right;
            if (direction == WeaponDirection.North) return Vector2.up;
            if (direction == WeaponDirection.SixWay)
            {
                float angle = (-37.5f + index * 15) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            }
            return Quantize(facing);
        }

        public static Vector2 Quantize(Vector2 direction)
        {
            if (direction.sqrMagnitude < .001f) return Vector2.up;
            float angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI / 4)) * Mathf.PI / 4;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public bool HitsHeight(bool high, float progress)
        {
            if (!lobbed) return !high;
            // Ground targets can be hit once the projectile begins descending.
            return high ? progress >= .25f && progress <= .85f : progress >= .55f;
        }
    }
}
