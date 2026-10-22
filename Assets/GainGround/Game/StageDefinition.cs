using System;
using UnityEngine;

namespace GainGround.Game
{
    public enum EnemyBehavior { Legacy, Chaser, Spread, Wizard, Slime, Burrow, Orb, Turret, Dragon, Giant, Sorcerer, Tank, Spider, Computer, Barrier, Generator }
    [Serializable] public sealed class CaptiveSpawn
    {
        public CharacterDefinition character;
        public Vector2 position;
        [Range(1,2)] public int minimumPlayers = 1;
    }
    [Serializable] public sealed class HazardSpawn
    {
        public Rect area;
        public Sprite sprite;
        public int[] controllers = new int[0];
    }
    [Serializable] public sealed class EnemySpawn
    {
        public Vector2 position;
        [Min(1)] public int health = 2;
        [Min(0)] public float speed = 0.7f;
        [Min(0.3f)] public float shotInterval = 2.2f;
        public bool sentry;
        public bool elevated;
        public bool melee;
        [Min(0)] public float spawnDelay;
        public EnemyBehavior behavior;
        public Sprite sprite;
        public PS2EnemyVisual modelPrefab;
        public Vector2 visualSize = new Vector2(.65f,.8f);
        [Min(.05f)] public float hitRadius = .23f;
        public Vector2 obstacleSize;
        public bool boss;
        public bool freezeImmune;
        public float triggerDistance;
    }

    [CreateAssetMenu(menuName = "Gain Ground/Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        public string displayName;
        [Header("Music")]
        public AudioClip stageMusic;
        public AudioClip victoryMusic;
        [Range(0, 1)] public float musicVolume = .65f;
        [Header("Arena")]
        [Min(10)] public float timeLimit = 120;
        public Vector2 size = new Vector2(16, 10);
        public Vector2 start = new Vector2(0, -4);
        public Rect exit = new Rect(-1, 3.9f, 2, 0.8f);
        public Color floorColor = new Color(0.14f, 0.23f, 0.22f);
        public Texture2D terrainAtlas;
        public Vector2Int atlasOrigin;
        // Source-map pixel rectangles occupied by actors, replaced with clean terrain tiles.
        public RectInt[] terrainPatches = new RectInt[0];
        public Vector2Int[] patchSources = new Vector2Int[0];
        public Rect[] walls = new Rect[0];
        public EnemySpawn[] enemies = new EnemySpawn[0];
        public CharacterDefinition rescue;
        public Vector2 rescuePosition = new Vector2(-6, 2);
        [Header("Complete Mega Drive campaign")]
        public Sprite background;
        public bool noExit;
        public Rect[] additionalExits = new Rect[0];
        public CaptiveSpawn[] additionalCaptives = new CaptiveSpawn[0];
        public HazardSpawn[] hazards = new HazardSpawn[0];
        public bool ContainsExit(Vector2 position)
        {
            if (noExit) return false;
            if (exit.Contains(position)) return true;
            foreach (var extra in additionalExits) if (extra.Contains(position)) return true;
            return false;
        }
    }
}
