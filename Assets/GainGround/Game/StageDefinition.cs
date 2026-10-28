using System;
using UnityEngine;

namespace GainGround.Game
{
    public enum EnemyBehavior { Legacy, Chaser, Spread, Wizard, Slime, Burrow, Orb, Turret, Dragon, Giant, Sorcerer, Tank, Spider, Computer, Barrier, Generator, ChargeKnight, Ambusher, BurstArcher, WallArcher }
    public enum EnemyWalkingPattern { UseBehavior, Standing, TowardPlayers, BackAndForth, SideToSide, Patrol }
    [Serializable] public sealed class CaptiveSpawn
    {
        public CharacterDefinition character;
        public Vector2 position;
        [Range(1,2)] public int minimumPlayers = 1;
    }
    [Serializable] public sealed class HazardSpawn
    {
        [HideInInspector] public string editorId;
        public Rect area;
        public Sprite sprite;
        public GameObject prefab;
        public int[] controllers = new int[0];
    }
    [Serializable] public sealed class StageObjectSpawn
    {
        public string id;
        public GameObject prefab;
        public Rect area;
        [Tooltip("Rotation of the placed object in the stage plane, in degrees.")]
        public float rotationDegrees;
        [Tooltip("Optional scale for a 3D stage model. Zero uses the prefab's original scale.")]
        public Vector3 modelScale;
        [Tooltip("Matching positive IDs form a pair of teleport tiles. Zero is visual only.")]
        public int teleportGroup;
        [Min(0), Tooltip("Positive values make this object a destructible obstacle.")]
        public int health;
    }
    [Serializable] public sealed class EnemySpawn
    {
        public Vector2 position;
        [Min(1)] public int health = 2;
        [Min(0)] public float speed = 0.7f;
        [Min(0.3f)] public float shotInterval = 2.2f;
        [Min(0), Tooltip("Tempo até o primeiro disparo. Zero usa o escalonamento padrão da fase.")]
        public float initialShotDelay;
        public bool sentry;
        public bool elevated;
        public bool melee;
        [Min(0)] public float spawnDelay;
        public EnemyBehavior behavior;
        [Tooltip("Movement override. Patrol starts chasing within patrolDistance and returns to spawn when the player is farther than twice that range.")]
        public EnemyWalkingPattern walkingPattern;
        [Min(0), Tooltip("Patrol distance in world units; zero uses the prefab/default distance.")]
        public float walkingDistance;
        [Min(0), Tooltip("For Patrol: activation range in world units. Retreat starts beyond twice this range. Zero uses the prefab value or 4.")]
        public float patrolDistance;
        public Sprite sprite;
        public PS2EnemyVisual modelPrefab;
        [Header("Projectile overrides (empty uses the enemy prefab)")]
        public Sprite projectileSprite;
        public Vector2 projectileSize;
        public float projectileSpinDegreesPerSecond;
        public Vector2 visualSize = new Vector2(.65f,.8f);
        [Min(.05f)] public float hitRadius = .23f;
        public Vector2 obstacleSize;
        public bool boss;
        public bool freezeImmune;
        public float triggerDistance;
        [Min(0), Tooltip("When this wizard vanishes, teleport this many world units toward the nearest player. Zero disables teleporting.")]
        public float teleportOnVanishDistance;
        [Header("Sound overrides (empty uses the enemy prefab)")]
        [Tooltip("Played once per projectile volley or successful contact hit. Takes priority over the model prefab.")]
        public AudioClip attackSound;
        [Tooltip("Played when this enemy takes damage. Takes priority over the model prefab.")]
        public AudioClip hitSound;
        [Tooltip("Played when this enemy is defeated. Takes priority over the model prefab.")]
        public AudioClip deathSound;
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
        [Header("Apresentação da fase")]
        [Tooltip("Pontos que a câmera visita antes de liberar o jogo. Vazio: percorre automaticamente os grupos de inimigos visíveis.")]
        public Vector2[] introCameraTargets = new Vector2[0];
        public CharacterDefinition rescue;
        public Vector2 rescuePosition = new Vector2(-6, 2);
        [Header("Complete Mega Drive campaign")]
        public Sprite background;
        [Tooltip("Layout Tilemap usado durante o jogo. A imagem de fundo fica apenas como prévia no seletor.")]
        public StagePaintLayout paintedLayout;
        public bool noExit;
        public Rect[] additionalExits = new Rect[0];
        public CaptiveSpawn[] additionalCaptives = new CaptiveSpawn[0];
        [Header("PS2 stage objects")]
        public StageObjectSpawn[] objects = new StageObjectSpawn[0];
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
