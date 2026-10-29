using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame : MonoBehaviour
    {
        public StageDefinition[] stages;
        [Header("WebGL stage media (configured by the streaming build)")]
        public bool streamStageMedia;
        public StageMediaFiles[] webStageMedia;
        public CharacterPortraitFiles[] webCharacterPortraits;
        [Header("Local co-op")]
        [Range(1, 2)] public int playerCount = 2;
        [Tooltip("Player 1 roster. Use assets from Editable/Characters.")]
        public CharacterDefinition[] startingParty;
        [Tooltip("Player 2 roster. Empty uses the same starting classes, with independent lives and cooldowns.")]
        public CharacterDefinition[] secondStartingParty;
        [Tooltip("All available characters, referenced from Editable/Characters.")]
        public CharacterDefinition[] allCharacters;
        [Header("Movement")]
        [Min(.1f)] [SerializeField] float keyboardAccelerationSeconds = .3f;
        [Header("Character selection")]
        [Min(0f)] [SerializeField] float hitSelectionDelaySeconds = 1.6f;
        public bool useFullRoster;
        public bool hasFinished;
        public Sprite floorSprite, wallSprite, enemySprite, projectileSprite, exitSprite;
        public Camera gameCamera;
        public AudioClip titleMusic;
        [Header("Sequência final")]
        [Tooltip("Cartões narrativos antes da mensagem de conclusão. O asset em Resources/EndingSequence é usado se vazio.")]
        public GainGroundEndingSequence endingSequence;

        enum ScreenState { Title, Options, StageSelect, Loading, Playing, Cleared, Defeat, Victory, GameOver }
        sealed class Member { public CharacterDefinition definition; public bool lost, escaped; public float dashCooldown; }
        sealed class PlayerState
        {
            public int boomerangLaunches, score;
            public int id, active = -1, fallenMember = -1, selectionIndex;
            public bool awaitingSelection;
            public float selectionReadyAt;
            public Vector2 selectionScroll;
            public readonly List<Member> party = new List<Member>();
            public Vector2 position, facing = Vector2.up, dashDirection;
            public Vector2 lastMovementDirection;
            public float keyboardMovementRamp;
            public Vector2 dashTapDirection, previousDashTapDirection;
            public float dashTapStartedAt, previousDashTapReleasedAt;
            public bool dashTapHeld;
            public SpriteRenderer player, fallenAlly;
            public PS2VisualBinding modelBinding;
            public PlayerState carriedAlly, fallenCarrier;
            public RescueState additionalCaptive;
            public float fireCooldown, specialCooldown, invulnerability, dashRemaining, dashSpeed;
            public float swordSweepRemaining;
            public bool reflectingProjectiles;
            public WeaponDefinition swordSweepWeapon;
            public SpriteRenderer swordSweepVisual;
            public Sprite[] swordSweepFrames;
            public float swordSweepVisualDuration;
            public int currentFacingSprite = -1;
            public bool carrying, dashInvincible, dashProtectedThisFrame, dashRequested;
            public bool teleportArmed = true;
            public bool carryingFallen => carriedAlly != null;
            public bool HasActive => active >= 0 && active < party.Count && !party[active].lost && !party[active].escaped;
            public Color Tint => id == 0 ? new Color(.55f, .85f, 1) : new Color(1, .7f, .35f);
        }
        public struct PlayerInput
        {
            public Vector2 movement;
            public bool analogMovement;
            public bool fire, special, dash, switchMember;
        }
        sealed class Enemy
        {
            public Vector2 position;
            public EnemySpawn data;
            public int health, spawnIndex, volley;
            public PlayerState lastHitPlayer;
            public PS2EnemyVisual model;
            public float cooldown, frozen, age, confusionAngle;
            public float contactAttackCooldown;
            public int burstShotsRemaining;
            public bool chargeReturning;
            public EnemyWalkingPattern walkingPattern;
            public float walkingDistance, patrolDistance, moveSpeed;
            public float nextLaserSphereVolley = 8f;
            public EnemyPrefabDefaults prefabDefaults;
            public bool vulnerable = true, triggered, waterConfused;
            public bool patrolChasing, patrolReturning;
            public List<Vector2> patrolTrail;
            public int giantAttackPhase;
            public float giantAttackTimer, giantImpactThreshold;
            public bool giantImpactPending;
            public Vector2 giantAttackFacing;
            public SpriteRenderer visual;
            public Vector3 burrowBaseModelPosition;
        }
        sealed class DefeatedEnemyVisual
        {
            public SpriteRenderer anchor;
            public PS2EnemyVisual model;
        }
        sealed class Shot
        {
            public Vector2 position, velocity, origin, launchDirection;
            public float boomerangSide;
            public float remaining, age, orbitAngle, travel;
            public float trajectoryRange, blastRadius;
            public bool hostile, elevated, special, returning;
            public bool lobbedBomb;
            public bool directionalSprite;
            public float spinDegreesPerSecond;
            public bool computerLaserSphere;
            public float nextLaserSphereTurn;
            public int damage;
            public SpriteRenderer visual;
            public WeaponDefinition weapon;
            public Sprite[] animationFrames;
            public float animationFramesPerSecond;
            public Sprite impactSprite;
            public Sprite[] impactFrames;
            public float impactFramesPerSecond;
            public Vector2 impactSize;
            public PlayerState owner;
            public CharacterDefinition character;
            public readonly Dictionary<Enemy, float> hits = new Dictionary<Enemy, float>();
        }
        readonly List<PlayerState> players = new List<PlayerState>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<DefeatedEnemyVisual> defeatedEnemyVisuals = new List<DefeatedEnemyVisual>();
        readonly List<Shot> shots = new List<Shot>();
        readonly Stack<Shot> spareShots = new Stack<Shot>();
        readonly Stack<SpriteRenderer> spareShotVisuals = new Stack<SpriteRenderer>();
        readonly Stack<SpriteRenderer> spareEffectVisuals = new Stack<SpriteRenderer>();
        readonly Dictionary<GameObject, Sprite> effectPrefabSprites = new Dictionary<GameObject, Sprite>();
        Transform transientVisualPool;
        readonly Dictionary<long, List<Rect>> projectileWallCells = new Dictionary<long, List<Rect>>();
        readonly Dictionary<long, List<Enemy>> projectileEnemyCells = new Dictionary<long, List<Enemy>>();
        ScreenState state;
        Transform arena;
        SpriteRenderer captive;
        PlayerState captiveCarrier;
        StageDefinition stage;
        int stageIndex, score, pendingPlayerCount = 1, stageSelectIndex;
        float remainingTime;
        const float DashDuration = .16f;
        bool paused, rescued;
        string message = "";
        GUIStyle titleStyle, labelStyle, smallStyle, controlStyle;
        AudioSource musicSource;
        bool restartMusic, musicPaused;
#if UNITY_WEBGL && !UNITY_EDITOR
        bool titleMusicInteractionHandled;
#endif
        public int EnemyCount => enemies.Count;
        public bool IsPlaying => state == ScreenState.Playing;

        void OnEnable()
        {
            ResumePendingMediaUnload();
        }

        void Start()
        {
            InitializeMobileControls();
            LoadGuiAssets();
            if (stages == null || stages.Length == 0 || startingParty == null || startingParty.Length == 0)
            { Debug.LogError("Assign stages and starting party in the Inspector.", this); enabled = false; return; }
            InitializeTitleUI();
            InitializeStageSelectUI();
            InitializeHud();
            LoadSettings(); LoadHighScore(); hasFinished = hasFinished || PlayerPrefs.GetInt("GainGround.HasFinished", 0) == 1; RefreshSaves(); state = ScreenState.Title; BeginTitleIntro();
            EnsureMusicSource();
            musicSource.clip = titleMusic;
            musicSource.loop = true;
            ApplyAudioVolumes();
            if (titleMusic != null) musicSource.Play();
        }

        GameDifficulty campaignDifficulty;
        CharacterDefinition currentRescue;
        public void NewCampaign(int firstStage = 0)
        {
            DestroyEndingAnimation();
            recordToBeat = HighScore;
            campaignDifficulty = settings.Difficulty; campaignClassicMode = settings.ClassicMode;
            ResetCampaignParty();
            stageIndex = Mathf.Clamp(firstStage, 0, stages.Length - 1); score = 0; LoadStage();
        }

        void ResetCampaignParty(bool allowFullRoster = true)
        {
            players.Clear();
            for (int id = 0; id < Mathf.Clamp(playerCount, 1, 2); id++)
                players.Add(CreatePlayerState(id, allowFullRoster));
        }

        PlayerState CreatePlayerState(int id, bool allowFullRoster = true)
        {
            var p = new PlayerState { id = id };
            var roster = id == 1 && secondStartingParty != null && secondStartingParty.Length > 0 ? secondStartingParty : startingParty;
            if (allowFullRoster && campaignDifficulty == GameDifficulty.Normal && useFullRoster && allCharacters != null && allCharacters.Length > 0) roster = allCharacters;
            roster = DifficultyRules.StartingRoster(campaignDifficulty, roster, allCharacters);
            if (!campaignClassicMode) roster = RandomRoster(roster);
            foreach (var definition in roster)
                if (definition != null) p.party.Add(new Member { definition = definition });
            return p;
        }

        bool JoinSecondPlayer()
        {
            if (state != ScreenState.Playing || paused || stage == null || arena == null || players.Count != 1)
                return false;
            var p = CreatePlayerState(1);
            if (p.party.Count == 0) return false;
            players.Add(p);
            playerCount = 2;
            p.active = 0;
            p.awaitingSelection = campaignClassicMode;
            p.selectionIndex = 0;
            if (p.awaitingSelection) p.active = -1;
            p.position = SpawnPosition(p);
            p.facing = Vector2.up;
            p.invulnerability = 1.5f;
            p.player = Visual("Player 2", p.HasActive ? p.party[p.active].definition.sprite : null,
                p.position, new Vector2(.6f, .8f), p.Tint, 5);
            p.player.enabled = p.HasActive;
            if (p.HasActive)
            {
                SetPlayerCharacterVisual(p, p.party[p.active].definition);
                p.modelBinding = PS2VisualBinding.Bind(p.player, p.party[p.active].definition);
            }
            AddEligibleCaptives();
            if (p.awaitingSelection) BeginSelectionPortraitLoad();
            message = p.awaitingSelection ? "P2 entrou: escolha um personagem." : "P2 entrou na partida.";
            return true;
        }

        Vector2 SpawnPosition(PlayerState p)
        {
            if (players.Count == 1) return stage.start;
            // Stay on reachable terrain near the shared start; do not teleport across walls.
            var offset = Vector2.right * (p.id == 0 ? -.4f : .4f);
            return ArenaRules.Move(stage.start, offset, .23f, stage.size, stage.walls);
        }

        void LoadStage()
        {
            CancelStageIntro();
            if (streamStageMedia) { BeginStageMediaLoad(); return; }
            BuildStage();
        }

        void BuildStage()
        {
            ResetMobileInput();
            restartMusic = true;
            ClearShots();
            if (arena != null) { arena.gameObject.SetActive(false); Destroy(arena.gameObject); }
            arena = new GameObject("Arena (generated from editable stage asset)").transform;
            arena.SetParent(transform); enemies.Clear(); defeatedEnemyVisuals.Clear(); blastVisuals.Clear(); extraCaptives.Clear(); hazardStates.Clear();
            stage = stages[stageIndex]; remainingTime = stage.timeLimit;
            BuildProjectileWallCells();
            ResetStageScorePresentation();
            paused = false; rescued = false; captiveCarrier = null;
            foreach (var p in players)
            {
                p.fallenAlly = null; p.fallenMember = -1; p.carriedAlly = p.fallenCarrier = null;
                p.carrying = false; p.boomerangLaunches = 0; p.additionalCaptive = null; p.fireCooldown = p.specialCooldown = 0; p.invulnerability = 1.5f;
                p.swordSweepRemaining = 0; p.reflectingProjectiles = false; p.swordSweepWeapon = null; p.swordSweepVisual = null;
                CancelDash(p);
                foreach (var member in p.party) { member.escaped = false; member.dashCooldown = 0; }
                p.active = p.party.FindIndex(member => !member.lost);
                p.awaitingSelection = ClassicMode && p.active >= 0;
                p.selectionReadyAt = 0;
                p.selectionIndex = Mathf.Max(0, p.active);
                if (p.awaitingSelection) p.active = -1;
                p.position = SpawnPosition(p); p.facing = Vector2.up;
                p.teleportArmed = true;
            }
            if (stage.paintedLayout != null)
            {
                var painted = Instantiate(stage.paintedLayout, arena);
                painted.name = stage.displayName + " Tilemap";
                if (painted.collision != null) painted.collision.gameObject.SetActive(false);
                foreach (var marker in painted.GetComponentsInChildren<StagePaintMarker>(true))
                    marker.gameObject.SetActive(false);
            }
            else if (stage.background != null) Visual("Original stage terrain", stage.background, Vector2.zero, stage.size, Color.white, -10);
            else if (stage.terrainAtlas != null) arena.gameObject.AddComponent<OriginalStageTerrain>().Build(stage);
            else
            {
            for (int y = 0; y < Mathf.CeilToInt(stage.size.y); y++)
                for (int x = 0; x < Mathf.CeilToInt(stage.size.x); x++)
                    Visual("Floor", floorSprite, new Vector2(x + .5f - stage.size.x / 2, y + .5f - stage.size.y / 2), Vector2.one,
                        stage.floorColor * ((x + y) % 2 == 0 ? 1 : 0.92f), -10);
            foreach (var wall in stage.walls) Visual("Wall", wallSprite, wall.center, wall.size, Color.white, 0);
            Visual("EXIT", exitSprite, stage.exit.center, stage.exit.size, Color.white, -1);
            }
            foreach (var spawn in stage.enemies)
            {
                var defaults = spawn.modelPrefab != null ? spawn.modelPrefab.GetComponent<EnemyPrefabDefaults>() : null;
                int baseHealth = defaults != null ? Mathf.Max(spawn.health, defaults.health) : spawn.health;
                var enemy = new Enemy { position = spawn.position, data = spawn, prefabDefaults = defaults,
                    health = DifficultyRules.EnemyHealth(campaignDifficulty, baseHealth, spawn.boss),
                    moveSpeed = defaults != null ? Mathf.Max(spawn.speed, defaults.speed) : spawn.speed,
                    walkingPattern = spawn.walkingPattern != EnemyWalkingPattern.UseBehavior ? spawn.walkingPattern :
                        defaults != null ? defaults.walkingPattern : EnemyWalkingPattern.UseBehavior,
                    walkingDistance = spawn.walkingDistance > 0 ? spawn.walkingDistance :
                        defaults != null && defaults.walkingDistance > 0 ? defaults.walkingDistance : 4f,
                    patrolDistance = spawn.patrolDistance > 0 ? spawn.patrolDistance :
                        defaults != null && defaults.patrolDistance > 0 ? defaults.patrolDistance : 4f,
                    cooldown = spawn.initialShotDelay > 0 ? spawn.initialShotDelay : 1 + enemies.Count * .17f,
                    spawnIndex = enemies.Count };
                if (enemy.walkingPattern == EnemyWalkingPattern.Patrol && enemy.moveSpeed <= 0)
                    enemy.moveSpeed = .7f;
                enemy.visual = Visual(spawn.boss ? "Boss " + spawn.behavior : spawn.behavior.ToString(), spawn.sprite != null ? spawn.sprite : enemySprite,
                    enemy.position, spawn.sprite != null ? spawn.visualSize : new Vector2(.65f, .8f),
                    spawn.sprite != null ? Color.white : spawn.elevated ? new Color(.6f, .8f, 1) : spawn.melee ? Color.white : new Color(1, .75f, .35f), 2);
                enemy.visual.enabled = spawn.spawnDelay <= 0;
                enemy.model = PS2EnemyVisual.Bind(enemy.visual, spawn);
                if (enemy.model != null)
                    enemy.burrowBaseModelPosition = enemy.model.transform.localPosition;
                enemies.Add(enemy);
            }
            foreach (var p in players)
            {
                p.player = Visual("Player " + (p.id + 1), p.HasActive ? p.party[p.active].definition.sprite : null,
                    p.position, new Vector2(.6f, .8f), p.Tint, 4 + p.id);
                p.player.enabled = p.HasActive;
                if (p.HasActive) SetPlayerCharacterVisual(p, p.party[p.active].definition);
                p.modelBinding = p.HasActive ? PS2VisualBinding.Bind(p.player, p.party[p.active].definition) : null;
            }
            currentRescue = RandomRescue(stage.rescue);
            captive = currentRescue == null ? null : Visual("Rescue", currentRescue.sprite, stage.rescuePosition, new Vector2(.45f, .6f), new Color(.7f, 1, 1), 3);
            if (captive != null) PS2VisualBinding.Bind(captive, currentRescue);
            BuildCampaignObjects();
            if (gameCamera != null)
            {
                gameCamera.clearFlags = CameraClearFlags.SolidColor;
                gameCamera.orthographic = true;
                // PS2 maps are centered on their native terrain bounds and viewed from above.
                gameCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                if (hudView == null)
                {
                    gameCamera.rect = new Rect(0, 0, 1, 1);
                    gameCamera.orthographicSize = Mathf.Max((stage.size.y + 1f) * .5f,
                        (stage.size.x + 1f) / (2f * gameCamera.aspect));
                }
            }
            if (hudView != null)
            {
                hudView.InvalidateLayout();
                hudView.UpdateLayout(gameCamera, stage.size);
            }
            state = ScreenState.Playing; message = stage.noExit ? "Eliminem todos os inimigos e o chefe." : "Eliminem os inimigos ou levem todas as equipes ate EXIT.";
            CheckTeamOutcome();
            FinishRestoredStage();
            if (state == ScreenState.Playing) BeginStageIntro();
        }

        void LateUpdate()
        {
            EnsureMusicSource();
            bool menuMusic = state == ScreenState.Title || state == ScreenState.Options || state == ScreenState.StageSelect;
            bool stageMusic = stage != null && state == ScreenState.Playing;
            bool loop = menuMusic || stageMusic;
            AudioClip clip = menuMusic ? titleMusic : stageMusic ? stage.stageMusic :
                stage != null && (state == ScreenState.Cleared || state == ScreenState.Victory) ?
                    (stage.victoryMusic != null ? stage.victoryMusic : streamStageMedia ? stage.stageMusic : null) : null;
            if (restartMusic || musicSource.clip != clip || musicSource.loop != loop)
            {
                musicSource.Stop();
                musicSource.clip = clip;
                musicSource.loop = loop;
                musicPaused = false;
                restartMusic = false;
                if (clip != null) musicSource.Play();
            }
            ApplyAudioVolumes();
            RefreshClockWarning();
            bool shouldPause = stageMusic && paused;
            if (shouldPause != musicPaused)
            {
                if (shouldPause) musicSource.Pause();
                else musicSource.UnPause();
                musicPaused = shouldPause;
            }
            RefreshTitleUI();
            if (state != ScreenState.Title) ReleaseTitleArtwork();
            RefreshStageSelectUI();
            RefreshHud();
            ApplyStageIntroCamera();
        }

        void EnsureMusicSource()
        {
            if (musicSource != null) return;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0;
            musicSource.priority = 32;
            if (gameCamera != null && FindAnyObjectByType<AudioListener>() == null)
                gameCamera.gameObject.AddComponent<AudioListener>();
        }

        void OnDisable()
        {
            CancelStageIntro();
            if (clockSource != null) clockSource.Stop();
            clockPaused = false;
            clockCrossfade = 0f;
            CancelHudPortraitLoad();
            if (hudView != null) { Destroy(hudView.gameObject); hudView = null; }
            if (titleView != null) { Destroy(titleView.gameObject); titleView = null; }
            if (stageSelectView != null) { Destroy(stageSelectView.gameObject); stageSelectView = null; }
            CancelStageMediaRequests();
            SuspendMediaUnload();
            ResetMobileInput();
            if (endingBurst != null) Destroy(endingBurst);
            endingBurst = null;
            if (swordSliderTexture != null) Destroy(swordSliderTexture);
            swordSliderTexture = null;
            if (carouselArrowLeft != null) Destroy(carouselArrowLeft);
            if (carouselArrowRight != null) Destroy(carouselArrowRight);
            carouselArrowLeft = carouselArrowRight = null;
            swordSliderThumb = null;
            mobileButton = null;
            if (musicSource != null) musicSource.Stop();
            restartMusic = true;
        }

        SpriteRenderer Visual(string name, Sprite sprite, Vector2 point, Vector2 size, Color color, int order)
        {
            var obj = new GameObject(name); obj.transform.SetParent(arena);
            obj.transform.position = new Vector3(point.x, point.y, 0);
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            if (sprite != null) obj.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            return renderer;
        }

        void SpawnEnemyEffect(string name, Sprite sprite, Vector2 position, Vector2 size,
            Sprite[] frames = null, float framesPerSecond = 45)
        {
            if (sprite == null) return;
            var renderer = AcquireTransientVisual(spareEffectVisuals, name, sprite, position,
                size.sqrMagnitude > 0 ? size : Vector2.one * .35f, Color.white, 7);
            var lifetime = renderer.GetComponent<EnemyEffectLifetime>();
            if (lifetime == null) lifetime = renderer.gameObject.AddComponent<EnemyEffectLifetime>();
            lifetime.Initialize(renderer, this, frames, framesPerSecond);
        }

        SpriteRenderer SpawnLightningVisualPrefab(GameObject prefab, Vector2 position, Vector2 size,
            float rotationDegrees, int sortingOrder)
        {
            if (prefab == null || arena == null) return null;
            var instance = Instantiate(prefab, arena);
            var renderer = instance.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Destroy(instance);
                return null;
            }
            instance.transform.position = new Vector3(position.x, position.y, 0);
            instance.transform.rotation = Quaternion.Euler(0, 0, rotationDegrees);
            if (renderer.sprite != null)
            {
                var spriteSize = renderer.sprite.bounds.size;
                if (spriteSize.x > .001f && spriteSize.y > .001f)
                    instance.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1);
            }
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;
            return renderer;
        }

        Sprite SpriteFromEffectPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!effectPrefabSprites.TryGetValue(prefab, out var sprite))
            {
                var renderer = prefab.GetComponent<SpriteRenderer>();
                sprite = renderer != null ? renderer.sprite : null;
                effectPrefabSprites.Add(prefab, sprite);
            }
            return sprite;
        }

        Shot RentShot()
        {
            var shot = spareShots.Count > 0 ? spareShots.Pop() : new Shot();
            shot.position = shot.velocity = shot.origin = shot.launchDirection = Vector2.zero;
            shot.boomerangSide = shot.remaining = shot.age = shot.orbitAngle = shot.travel = 0;
            shot.trajectoryRange = shot.blastRadius = 0;
            shot.hostile = shot.elevated = shot.special = shot.returning = shot.directionalSprite = false;
            shot.lobbedBomb = false;
            shot.spinDegreesPerSecond = 0;
            shot.computerLaserSphere = false; shot.nextLaserSphereTurn = 0;
            shot.damage = 0; shot.visual = null; shot.weapon = null;
            shot.animationFrames = null; shot.animationFramesPerSecond = 0;
            shot.impactSprite = null; shot.impactFrames = null;
            shot.impactFramesPerSecond = 0; shot.impactSize = Vector2.zero;
            shot.owner = null; shot.character = null; shot.hits.Clear();
            return shot;
        }

        void ReleaseShot(Shot shot)
        {
            if (shot.visual != null) ReleaseTransientVisual(shot.visual, spareShotVisuals);
            shot.visual = null;
            shot.weapon = null; shot.owner = null; shot.character = null;
            shot.animationFrames = null; shot.impactSprite = null; shot.impactFrames = null;
            shot.hits.Clear();
            if (spareShots.Count < 256) spareShots.Push(shot);
        }

        void ClearShots()
        {
            foreach (var shot in shots) ReleaseShot(shot);
            shots.Clear();
        }

        SpriteRenderer AcquireShotVisual(string name, Sprite sprite, Vector2 position, Vector2 size, Color tint, int order)
        {
            var visual = AcquireTransientVisual(spareShotVisuals, name, sprite, position, size, tint, order);
            var oldGlow = visual.transform.Find("Projectile glow");
            if (oldGlow != null) oldGlow.gameObject.SetActive(false);
            return visual;
        }

        static void ConfigureProjectileGlow(SpriteRenderer visual, Sprite sprite, Vector2 size, Color tint)
        {
            if (visual == null || sprite == null) return;
            var child = visual.transform.Find("Projectile glow");
            var glow = child != null ? child.GetComponent<SpriteRenderer>() : null;
            if (glow == null)
            {
                var objectGlow = new GameObject("Projectile glow");
                objectGlow.transform.SetParent(visual.transform, false);
                glow = objectGlow.AddComponent<SpriteRenderer>();
            }
            glow.sprite = sprite;
            glow.color = tint;
            glow.sortingOrder = visual.sortingOrder - 1;
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localRotation = Quaternion.identity;
            glow.transform.localScale = new Vector3(
                size.x / (sprite.bounds.size.x * visual.transform.localScale.x),
                size.y / (sprite.bounds.size.y * visual.transform.localScale.y), 1);
            glow.gameObject.SetActive(true);
        }

        SpriteRenderer AcquireTransientVisual(Stack<SpriteRenderer> pool, string name, Sprite sprite,
            Vector2 position, Vector2 size, Color tint, int order)
        {
            var renderer = pool.Count > 0 ? pool.Pop() : new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.gameObject.name = name;
            renderer.transform.SetParent(arena);
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.identity;
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            renderer.enabled = true;
            renderer.transform.localScale = sprite != null
                ? new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1)
                : Vector3.one;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        void ReleaseTransientVisual(SpriteRenderer renderer, Stack<SpriteRenderer> pool)
        {
            if (renderer == null) return;
            if (pool.Count >= 256) { Destroy(renderer.gameObject); return; }
            if (transientVisualPool == null)
            {
                transientVisualPool = new GameObject("Transient visual pool").transform;
                transientVisualPool.SetParent(transform);
            }
            renderer.gameObject.SetActive(false);
            renderer.sprite = null;
            renderer.transform.SetParent(transientVisualPool);
            pool.Push(renderer);
        }

        internal void ReleaseEnemyEffectVisual(SpriteRenderer renderer)
            => ReleaseTransientVisual(renderer, spareEffectVisuals);

        static long ProjectileCellKey(int x, int y) => ((long)x << 32) | (uint)y;

        void BuildProjectileWallCells()
        {
            projectileWallCells.Clear();
            foreach (var wall in stage.walls)
                for (int x = Mathf.FloorToInt(wall.xMin - .06f); x <= Mathf.FloorToInt(wall.xMax + .06f); x++)
                    for (int y = Mathf.FloorToInt(wall.yMin - .06f); y <= Mathf.FloorToInt(wall.yMax + .06f); y++)
                    {
                        long key = ProjectileCellKey(x, y);
                        if (!projectileWallCells.TryGetValue(key, out var cells))
                        { cells = new List<Rect>(); projectileWallCells.Add(key, cells); }
                        cells.Add(wall);
                    }
        }

        bool ProjectileWallBlocked(Vector2 position)
        {
            if (StageObjectBlocks(position)) return true;
            if (!projectileWallCells.TryGetValue(ProjectileCellKey(Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y)), out var cells)) return false;
            foreach (var wall in cells)
                if (ArenaRules.Intersects(wall, position, .06f)) return true;
            return false;
        }

        void BuildProjectileEnemyCells()
        {
            foreach (var cells in projectileEnemyCells.Values) cells.Clear();
            foreach (var enemy in enemies)
            {
                if (!enemy.visual.enabled || !enemy.vulnerable) continue;
                float halfX = enemy.data.obstacleSize.sqrMagnitude > 0 ?
                    enemy.data.obstacleSize.x * .5f : enemy.data.hitRadius;
                float halfY = enemy.data.obstacleSize.sqrMagnitude > 0 ?
                    enemy.data.obstacleSize.y * .5f : Mathf.Max(enemy.data.hitRadius, enemy.data.verticalHitRadius);
                for (int x = Mathf.FloorToInt(enemy.position.x - halfX); x <= Mathf.FloorToInt(enemy.position.x + halfX); x++)
                    for (int y = Mathf.FloorToInt(enemy.position.y - halfY); y <= Mathf.FloorToInt(enemy.position.y + halfY); y++)
                    {
                        long key = ProjectileCellKey(x, y);
                        if (!projectileEnemyCells.TryGetValue(key, out var cells))
                        { cells = new List<Enemy>(); projectileEnemyCells.Add(key, cells); }
                        cells.Add(enemy);
                    }
            }
        }

        bool HasProjectileEnemyNearby(Vector2 position, float radius)
        {
            for (int x = Mathf.FloorToInt(position.x - radius); x <= Mathf.FloorToInt(position.x + radius); x++)
                for (int y = Mathf.FloorToInt(position.y - radius); y <= Mathf.FloorToInt(position.y + radius); y++)
                    if (projectileEnemyCells.TryGetValue(ProjectileCellKey(x, y), out var cells) && cells.Count > 0)
                        return true;
            return false;
        }

        void Update()
        {
            PollBindings();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == ScreenState.Title && !titleMusicInteractionHandled &&
                (AnyKeyDown || MousePressed(0) || TouchCount > 0))
            {
                titleMusicInteractionHandled = true;
                if (titleMusic != null && musicSource != null && musicSource.clip == titleMusic)
                    musicSource.Play();
            }
#endif
            if(bindingsOpen){UpdateBindingCapture();return;}
            UpdateMobileControls();
            if(saveBrowserOpen){UpdateSaveSlotsInput();return;}
            if (UpdateTitleIntro()) return;
            if (state == ScreenState.Loading) return;
            if (state == ScreenState.Title || state == ScreenState.Options || state == ScreenState.StageSelect)
            { UpdateMenuInput(); UpdateStagePreview(); return; }
            if (state == ScreenState.Cleared || (state == ScreenState.Victory && pendingEndingScore))
            {
                stageScoreElapsed += Time.unscaledDeltaTime;
                TickScoreCountSound();
                if (pendingEndingScore && stageScoreElapsed >= StageScorePresentationDuration)
                {
                    pendingEndingScore = false;
                    BeginEndingAnimation();
                    return;
                }
                if (pendingEndingScore) return;
            }
            if (endingAnimationPlaying) { TickEndingAnimation(Time.unscaledDeltaTime); return; }
            if (endingSequencePlaying) { TickEndingSequence(Time.unscaledDeltaTime); return; }
            if (state == ScreenState.Victory && !pendingEndingScore)
            {
                endingMessageElapsed += Time.unscaledDeltaTime;
                if (endingMessageElapsed >= EndingMessageDuration ||
                    IsKeyPressed(Key.Enter) || IsKeyPressed(Key.Escape) ||
                    MenuDown(GameAction.Fire) || MousePressed(0) ||
                    TouchPressed)
                    ReturnToMenu();
                return;
            }
            if (state == ScreenState.Defeat || state == ScreenState.GameOver) { UpdateContinueInput(); return; }
            if (state != ScreenState.Playing)
            {
                if (IsKeyPressed(Key.Enter) || MenuDown(GameAction.Fire)) Continue();
                return;
            }
            if (stageIntroPlaying) { AdvanceStageIntro(Time.unscaledDeltaTime); return; }
            if (IsKeyPressed(Key.Escape) || MenuDown(GameAction.Pause))
            {
                paused = !paused;
                mobileGameScroll = Vector2.zero;
                foreach (var p in players) ResetDirectionalDashTap(p);
            }
            bool secondJoined = !paused && players.Count == 1 &&
                (ActionDown(1, GameAction.Special) || mobileInputs[1].special) && JoinSecondPlayer();
            var first = ReadMappedControls(0); var second = ReadMappedControls(1);
            if (secondJoined) second = default;
            MergeMobileInput(ref first, 0);
            MergeMobileInput(ref second, 1);
            foreach (var p in players)
                if (p.awaitingSelection && !paused && UpdateSelectionInput(p))
                { if (p.id == 0) first = default; else second = default; }
            Tick(Mathf.Min(Time.deltaTime, .05f), first, second);
        }

        void Tick(float dt, PlayerInput first, PlayerInput second)
        {
            if (paused || state != ScreenState.Playing || WaitingForSelection) return;
            remainingTime -= dt;
            if (remainingTime <= 0) { remainingTime = 0; BeginContinue("O tempo acabou."); return; }
            foreach (var p in players) UpdatePlayer(p, p.id == 0 ? first : second, dt);
            if (state != ScreenState.Playing) return;
            UpdateStageTeleports();
            UpdateEnemies(dt);
            if (state != ScreenState.Playing) return;
            UpdateHazards();
            if (state != ScreenState.Playing) return;
            UpdateShots(dt);
            TickDefeatedEnemyVisuals(dt);
            if (state == ScreenState.Playing && enemies.Count == 0 && defeatedEnemyVisuals.Count == 0) ClearStage();
        }

        void TickDefeatedEnemyVisuals(float dt)
        {
            for (int i = defeatedEnemyVisuals.Count - 1; i >= 0; i--)
            {
                var defeated = defeatedEnemyVisuals[i];
                if (defeated.model == null || defeated.anchor == null || defeated.model.TickDeath(dt))
                {
                    if (defeated.anchor != null) Destroy(defeated.anchor.gameObject);
                    defeatedEnemyVisuals.RemoveAt(i);
                }
            }
        }

        void UpdatePlayer(PlayerState p, PlayerInput input, float dt)
        {
            foreach (var member in p.party) member.dashCooldown = Mathf.Max(0, member.dashCooldown - dt);
            p.dashProtectedThisFrame = false;
            p.fireCooldown -= dt; p.specialCooldown -= dt; p.invulnerability -= dt;
            if (!p.HasActive)
            {
                p.dashRequested = false;
                ResetMovementRamp(p);
                ResetDirectionalDashTap(p);
                return;
            }
            if (input.switchMember) SwitchMember(p);
            var direction = Vector2.ClampMagnitude(input.movement, 1f);
            if (direction != Vector2.zero) p.facing = direction.normalized;
            if (UpdateDirectionalDashTap(p, direction, Time.unscaledTime)) input.dash = true;
            var character = p.party[p.active].definition;
            UpdatePlayerFacingSprite(p, character);
            p.swordSweepRemaining = Mathf.Max(0, p.swordSweepRemaining - dt);
            if (p.swordSweepRemaining <= 0)
            {
                p.reflectingProjectiles = false;
                p.swordSweepWeapon = null;
                if (p.swordSweepVisual != null) { Destroy(p.swordSweepVisual.gameObject); p.swordSweepVisual = null; }
            }
            else UpdateSwordSweepVisual(p);
            if (input.dash || p.dashRequested) TryDash(p);
            p.dashRequested = false;
            if (p.dashRemaining > 0) MoveDash(p, dt);
            else
            {
                float intensity = MovementIntensity(p, direction, input.analogMovement, dt);
                p.position = ArenaRules.Move(p.position, direction.normalized * intensity *
                    character.speed * (p.carrying || p.carryingFallen ? .8f : 1) * dt,
                    .23f, stage.size, movementWalls);
            }
            p.player.transform.position = p.position;
            Color tint = p.Tint * character.characterTint;
            if (p.dashProtectedThisFrame) tint = Color.Lerp(tint, Color.white, .7f);
            if (p.invulnerability > 0 && Mathf.FloorToInt(p.invulnerability * 12) % 2 == 0) tint.a = .35f;
            p.player.color = tint;
            if (p.modelBinding != null && p.modelBinding.Model != null)
                p.modelBinding.Model.Tick(dt, p.facing, direction != Vector2.zero, p.dashRemaining > 0, p.player.enabled);
            if ((input.fire || settings.AutoFire) && p.fireCooldown <= 0)
            {
                if (character.normalWeapon != null && !string.IsNullOrEmpty(character.normalWeapon.label))
                {
                    if (FireWeapon(p, character.normalWeapon, false)) p.fireCooldown = character.normalWeapon.cooldown;
                }
                else
                {
                    Fire(p.position, p.facing, false, false, character.projectileSpeed, character.range,
                        character.damage, character.shotColor, null, character.normalAttackSound);
                    shots[shots.Count-1].owner=p;
                    p.fireCooldown = character.shotInterval;
                }
            }
            if (input.special && p.specialCooldown <= 0)
            {
                if (character.specialWeapon != null && !string.IsNullOrEmpty(character.specialWeapon.label))
                {
                    if (FireWeapon(p, character.specialWeapon, true)) p.specialCooldown = character.specialWeapon.cooldown;
                }
                else
                {
                    Fire(p.position, p.facing, false, character.specialPassesWalls, character.projectileSpeed * .7f,
                        character.range, character.damage * 2, Color.cyan, null, character.specialAttackSound);
                    shots[shots.Count-1].owner=p;
                    p.specialCooldown = 1.2f;
                }
            }
            UpdateRescues(p);
            if (stage.ContainsExit(p.position))
            {
                if (p.carrying) SecureRescue(p);
                if (p.carryingFallen) SecureFallenRescue(p);
                p.party[p.active].escaped = true; AwardPoints(p,100);
                if (SelectAvailable(p)) Respawn(p, 1.5f);
                CheckTeamOutcome();
            }
        }

        const float DashTapInterval = .32f;
        const float DashTapMaximumHold = .25f;

        static bool UpdateDirectionalDashTap(PlayerState player, Vector2 direction, float now)
        {
            // A tap starts only after the direction is deliberately pushed and ends at neutral.
            if (!player.dashTapHeld)
            {
                if (direction.sqrMagnitude < .55f * .55f) return false;
                player.dashTapHeld = true;
                player.dashTapDirection = direction.normalized;
                player.dashTapStartedAt = now;
                bool repeated = now - player.previousDashTapReleasedAt <= DashTapInterval &&
                    Vector2.Dot(player.dashTapDirection, player.previousDashTapDirection) >= .85f;
                player.previousDashTapReleasedAt = float.NegativeInfinity;
                return repeated;
            }
            if (direction.sqrMagnitude <= .2f * .2f)
            {
                player.dashTapHeld = false;
                if (now - player.dashTapStartedAt <= DashTapMaximumHold)
                {
                    player.previousDashTapDirection = player.dashTapDirection;
                    player.previousDashTapReleasedAt = now;
                }
                else player.previousDashTapReleasedAt = float.NegativeInfinity;
            }
            else if (direction.sqrMagnitude >= .55f * .55f)
                player.dashTapDirection = direction.normalized;
            return false;
        }

        static void ResetDirectionalDashTap(PlayerState player)
        {
            player.dashTapHeld = false;
            player.dashTapDirection = player.previousDashTapDirection = Vector2.zero;
            player.previousDashTapReleasedAt = float.NegativeInfinity;
        }

        float MovementIntensity(PlayerState player, Vector2 direction, bool analog, float dt)
        {
            if (analog)
            {
                ResetMovementRamp(player);
                return direction.magnitude;
            }
            if (direction.sqrMagnitude < .0001f)
            {
                ResetMovementRamp(player);
                return 0;
            }
            var heading = direction.normalized;
            if (Vector2.Dot(player.lastMovementDirection, heading) < .5f)
                player.keyboardMovementRamp = 0;
            player.lastMovementDirection = heading;
            float rampSeconds = keyboardAccelerationSeconds > 0 ? keyboardAccelerationSeconds : .3f;
            player.keyboardMovementRamp = Mathf.Min(1,
                player.keyboardMovementRamp + dt / rampSeconds);
            return player.keyboardMovementRamp;
        }

        static void ResetMovementRamp(PlayerState player)
        {
            player.keyboardMovementRamp = 0;
            player.lastMovementDirection = Vector2.zero;
        }

        void UpdateRescues(PlayerState p)
        {
            if (captive != null && !rescued)
            {
                if (captiveCarrier == null && !p.carrying && ((Vector2)captive.transform.position - p.position).sqrMagnitude < .5f)
                { captiveCarrier = p; p.carrying = true; message = "P" + (p.id + 1) + ": leve o novo aliado ate EXIT."; }
                if (captiveCarrier == p) captive.transform.position = p.position - p.facing * .5f;
            }
            UpdateAdditionalRescues(p);
            foreach (var owner in players)
            {
                if (owner.fallenAlly == null) continue;
                if (p.carriedAlly == null && owner.fallenCarrier == null && ((Vector2)owner.fallenAlly.transform.position - p.position).sqrMagnitude < .5f)
                {
                    p.carriedAlly = owner; owner.fallenCarrier = p;
                    message = "Recupere o personagem de P" + (owner.id + 1) + " em EXIT, sem bonus de pontos.";
                }
                if (owner.fallenCarrier == p) owner.fallenAlly.transform.position = p.position - p.facing * (p.carrying ? .9f : .5f);
            }
        }

        void TryDash(PlayerState p)
        {
            if (state != ScreenState.Playing || paused || !p.HasActive || p.dashRemaining > 0) return;
            var member = p.party[p.active];
            if (member.dashCooldown > 0 || member.definition.Distancia <= 0) return;
            ResetMovementRamp(p);
            PlaySfx(member.definition.dashSound != null ? member.definition.dashSound : dashSound);
            p.dashDirection = p.facing.normalized; p.dashSpeed = member.definition.Distancia / DashDuration;
            p.dashRemaining = DashDuration; p.dashInvincible = member.definition.Invencivel;
            member.dashCooldown = Mathf.Max(0, member.definition.TempodeCooldown);
        }
        void MoveDash(PlayerState p, float dt)
        {
            p.dashProtectedThisFrame = p.dashInvincible;
            float step = Mathf.Min(dt, p.dashRemaining);
            p.position = ArenaRules.Move(p.position, p.dashDirection * p.dashSpeed * step, .23f, stage.size, movementWalls);
            p.dashRemaining = Mathf.Max(0, p.dashRemaining - step);
        }
        void CancelDash(PlayerState p)
        {
            p.dashRemaining = 0; p.dashInvincible = p.dashProtectedThisFrame = p.dashRequested = false;
            ResetDirectionalDashTap(p);
        }
        void SetMember(PlayerState p, int index)
        {
            CancelDash(p); ResetMovementRamp(p); p.awaitingSelection = false; p.selectionReadyAt = 0; p.active = index;
            SetPlayerCharacterVisual(p, p.party[index].definition); p.player.enabled = true;
            p.modelBinding = PS2VisualBinding.Bind(p.player, p.party[index].definition);
        }
        bool SelectAvailable(PlayerState p)
        {
            int next = p.party.FindIndex(member => !member.lost && !member.escaped);
            if (next < 0) { CancelDash(p); p.awaitingSelection = false; p.active = -1; p.player.enabled = false; return false; }
            if (ClassicMode)
            {
                CancelDash(p); p.active = -1; p.player.enabled = false;
                p.awaitingSelection = true; p.selectionIndex = next;
                BeginSelectionPortraitLoad();
                return false;
            }
            SetMember(p, next); return true;
        }
        void SwitchMember(PlayerState p)
        {
            if (ClassicMode || !p.HasActive) return;
            for (int n = 1; n < p.party.Count; n++)
            {
                int next = (p.active + n) % p.party.Count;
                if (!p.party[next].lost && !p.party[next].escaped)
                {
                    var sound = p.party[next].definition.switchSound;
                    SetMember(p, next);
                    PlaySfx(sound != null ? sound : switchSound);
                    break;
                }
            }
        }
        void Respawn(PlayerState p, float protection)
        {
            p.position = SpawnPosition(p); p.player.transform.position = p.position; p.invulnerability = protection;
        }
        void SecureRescue(PlayerState p)
        {
            if (p.additionalCaptive != null) { SecureAdditionalRescue(p); return; }
            if (captiveCarrier != p || captive == null || rescued) return;
            PlaySfx(currentRescue != null && currentRescue.rescueSound != null ? currentRescue.rescueSound : rescueSound);
            p.carrying = false; captiveCarrier = null; rescued = true; Destroy(captive.gameObject); captive = null;
            p.party.Add(new Member { definition = currentRescue, escaped = true }); AwardPoints(p,DifficultyRules.ScorePoints(campaignDifficulty,500));
            message = "P" + (p.id + 1) + " recrutou um aliado para sua equipe na proxima fase.";
        }
        void RemoveFallen(PlayerState owner)
        {
            if (owner.fallenCarrier != null) owner.fallenCarrier.carriedAlly = null;
            owner.fallenCarrier = null;
            if (owner.fallenAlly != null) { owner.fallenAlly.gameObject.SetActive(false); Destroy(owner.fallenAlly.gameObject); }
            owner.fallenAlly = null; owner.fallenMember = -1;
        }
        bool HitPlayer(PlayerState p)
        {
            if (!p.HasActive || p.invulnerability > 0 || p.dashProtectedThisFrame || (p.dashRemaining > 0 && p.dashInvincible) || state != ScreenState.Playing) return false;
            var character = p.party[p.active].definition;
            PlaySfx(character.hitSound != null ? character.hitSound : hitSound);
            DropAdditionalRescue(p);
            // A carrier drops a partner's fallen character; ownership never changes.
            if (p.carriedAlly != null)
            {
                p.carriedAlly.fallenCarrier = null;
                p.carriedAlly.fallenAlly.transform.position = p.position;
                p.carriedAlly = null;
            }
            // Only the latest casualty in each player's own roster can be recovered.
            RemoveFallen(p);
            p.fallenMember = p.active; p.party[p.active].lost = true;
            p.fallenAlly = Visual("Fallen P" + (p.id + 1), p.party[p.active].definition.sprite, p.position, new Vector2(.45f, .6f), p.Tint, 3);
            PS2VisualBinding.Bind(p.fallenAlly, p.party[p.active].definition);
            message = "P" + (p.id + 1) + ": " + p.party[p.active].definition.displayName + " aguarda resgate em EXIT.";
            if (captiveCarrier == p)
            {
                captiveCarrier = null; p.carrying = false;
                if (captive != null) captive.transform.position = stage.rescuePosition;
            }
            if (SelectAvailable(p)) Respawn(p, 2);
            else if (p.awaitingSelection) p.selectionReadyAt = Time.unscaledTime + hitSelectionDelaySeconds;
            CheckTeamOutcome();
            return true;
        }
        void SecureFallenRescue(PlayerState carrier)
        {
            var owner = carrier.carriedAlly;
            if (owner == null || owner.fallenMember < 0) return;
            var character = owner.party[owner.fallenMember].definition;
            owner.party[owner.fallenMember].lost = false;
            owner.party[owner.fallenMember].escaped = true;
            RemoveFallen(owner);
            PlaySfx(character.rescueSound != null ? character.rescueSound : rescueSound);
            message = "Personagem de P" + (owner.id + 1) + " recuperado para a proxima fase, sem pontos!";
        }
        void CheckTeamOutcome()
        {
            if (state != ScreenState.Playing || players.Exists(p => p.HasActive || p.awaitingSelection)) return;
            if (players.Exists(p => p.party.Exists(m => !m.lost && m.escaped))) ClearStage();
            else { BeginContinue("Todos os personagens foram perdidos."); }
        }
        PlayerState ClosestPlayer(Vector2 origin)
        {
            PlayerState closest = null; float distance = float.PositiveInfinity;
            foreach (var p in players)
            {
                if (!p.HasActive) continue;
                float next = (p.position - origin).sqrMagnitude;
                if (next < distance) { closest = p; distance = next; }
            }
            return closest;
        }
        PlayerState ClosestVisiblePlayer(Enemy enemy)
        {
            PlayerState closest = null;
            float distance = float.PositiveInfinity;
            foreach (var player in players)
            {
                if (!player.HasActive) continue;
                float next = (player.position - enemy.position).sqrMagnitude;
                if (next >= distance || !HasEnemyLineOfSight(enemy, player.position)) continue;
                closest = player;
                distance = next;
            }
            return closest;
        }

        bool HasEnemyLineOfSight(Enemy enemy, Vector2 target)
        {
            foreach (var wall in movementWalls)
            {
                // Raised sentries stand inside their own platform's footprint.
                if (enemy.data.elevated && wall.Contains(enemy.position)) continue;
                if (ArenaRules.SegmentIntersects(wall, enemy.position, target)) return false;
            }
            return true;
        }
        void UpdateEnemies(float dt)
        {
            foreach (var enemy in enemies)
            {
                if (stage.timeLimit - remainingTime < enemy.data.spawnDelay) continue;
                enemy.visual.enabled = true;
                var before = enemy.position;
                bool frozen = enemy.frozen > 0;
                enemy.contactAttackCooldown = Mathf.Max(0, enemy.contactAttackCooldown - dt);
                if (enemy.waterConfused && frozen && !enemy.data.boss)
                {
                    enemy.confusionAngle = Mathf.Repeat(enemy.confusionAngle + 540f * dt, 360f);
                    if (enemy.model == null)
                        enemy.visual.transform.rotation = Quaternion.Euler(0, 0, enemy.confusionAngle);
                }
                else if (enemy.waterConfused)
                {
                    enemy.waterConfused = false;
                    enemy.confusionAngle = 0;
                    if (enemy.model == null) enemy.visual.transform.rotation = Quaternion.identity;
                }
                if (UpdateCampaignEnemy(enemy, dt)) { UpdateEnemyModel(enemy, before, frozen ? 0 : dt); continue; }
                if (enemy.frozen > 0)
                {
                    enemy.frozen = Mathf.Max(0, enemy.frozen - dt);
                    enemy.visual.color = Color.cyan;
                    UpdateEnemyModel(enemy, before, 0);
                    continue;
                }
                enemy.visual.color = enemy.data.sprite != null ? Color.white : enemy.data.elevated ? new Color(.6f, .8f, 1) : enemy.data.melee ? Color.white : new Color(1, .75f, .35f);
                var target = ClosestPlayer(enemy.position);
                if (target == null) continue;
                Vector2 direction = (target.position - enemy.position).normalized;
                if (!enemy.data.sentry && (enemy.data.melee ||
                    (target.position - enemy.position).sqrMagnitude > 3 ||
                    !HasEnemyLineOfSight(enemy, target.position)))
                    enemy.position = ArenaRules.Move(enemy.position, direction * enemy.data.speed * dt, .25f, stage.size, movementWalls);
                enemy.visual.transform.position = enemy.position; enemy.cooldown -= dt;
                if (!enemy.data.melee && enemy.cooldown <= 0)
                {
                    var visible = ClosestVisiblePlayer(enemy);
                    if (visible != null)
                    {
                        direction = (visible.position - enemy.position).normalized;
                        PlayEnemyAttackSound(enemy);
                        Fire(enemy.position, direction, true, enemy.data.elevated, 3.2f, 16, 1, new Color(1, .35f, .25f), enemy);
                        enemy.cooldown = enemy.data.shotInterval;
                        if(enemy.model != null) enemy.model.Attack();
                    }
                }
                foreach (var p in players)
                    if (p.HasActive && !enemy.data.elevated &&
                        (p.position - enemy.position).sqrMagnitude < .25f &&
                        TryEnemyContactAttack(enemy, p)) break;
                UpdateEnemyModel(enemy, before, dt);
                if (state != ScreenState.Playing) return;
            }
        }
        void UpdateEnemyModel(Enemy enemy, Vector2 before, float dt)
        {
            if(enemy.model == null)return;
            var target=ClosestPlayer(enemy.position);
            var facing=enemy.giantAttackPhase!=0&&enemy.giantAttackFacing.sqrMagnitude>.001f
                ? enemy.giantAttackFacing : enemy.patrolReturning ? (enemy.data.position-enemy.position).normalized :
                target == null ? Vector2.down : (target.position-enemy.position).normalized;
            enemy.model.Tick(dt,facing,(enemy.position-before).sqrMagnitude>.000001f);
            if (enemy.waterConfused && enemy.frozen > 0 && !enemy.data.boss && enemy.model.rig != null)
                enemy.model.rig.localRotation *= Quaternion.Euler(0, enemy.confusionAngle, 0);
        }
        Shot Fire(Vector2 origin, Vector2 direction, bool hostile, bool elevated, float speed, float range,
            int damage, Color color, Enemy source = null, AudioClip playerSound = null)
        {
            var shot = RentShot();
            shot.position = origin; shot.origin = origin; shot.launchDirection = direction.normalized;
            shot.velocity = direction * speed; shot.remaining = range; shot.hostile = hostile;
            shot.elevated = elevated; shot.damage = damage; shot.age = 0;
            var effects = source != null ? source.model : null;
            var overrideSprite = source != null ? source.data.projectileSprite : null;
            var frames = overrideSprite == null && effects != null ? effects.projectileFrames : null;
            Sprite prefabSprite = effects != null ? SpriteFromEffectPrefab(effects.projectileVisualPrefab) : null;
            shot.directionalSprite = overrideSprite == null && prefabSprite != null;
            shot.spinDegreesPerSecond = overrideSprite != null ? source.data.projectileSpinDegreesPerSecond : 0;
            Sprite sprite = overrideSprite != null ? overrideSprite : frames != null && frames.Length > 0 && frames[0] != null ? frames[0] :
                prefabSprite != null ? prefabSprite : effects != null && effects.projectileSprite != null ?
                    effects.projectileSprite : projectileSprite;
            Vector2 size = overrideSprite != null && source.data.projectileSize.sqrMagnitude > 0 ? source.data.projectileSize :
                effects != null && effects.projectileSize.sqrMagnitude > 0 ? effects.projectileSize :
                Vector2.one * (elevated ? .24f : .16f);
            Color tint = overrideSprite != null ? Color.white : effects != null && prefabSprite != null ? effects.projectileTint :
                effects != null ? color * effects.projectileTint : color;
            shot.visual = AcquireShotVisual(hostile ? "Enemy shot" : "Player shot", sprite, origin, size, tint, 6);
            if (effects != null && effects.projectileGlowSprite != null)
                ConfigureProjectileGlow(shot.visual, effects.projectileGlowSprite,
                    effects.projectileGlowSize, effects.projectileGlowTint);
            if (shot.directionalSprite)
                shot.visual.transform.rotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
            shot.animationFrames = frames;
            shot.animationFramesPerSecond = effects != null ? effects.projectileFramesPerSecond : 0;
            shot.impactSprite = effects != null ?
                SpriteFromEffectPrefab(effects.impactVisualPrefab) ?? effects.impactSprite : null;
            shot.impactFrames = effects != null ? effects.impactFrames : null;
            shot.impactFramesPerSecond = effects != null ? effects.impactFramesPerSecond : 45;
            shot.impactSize = effects != null ? effects.effectSize : Vector2.zero;
            if (effects != null)
            {
                var muzzle = SpriteFromEffectPrefab(effects.muzzleVisualPrefab) ?? effects.muzzleFlashSprite;
                if (muzzle != null) SpawnEnemyEffect("Enemy muzzle flash", muzzle, origin, effects.effectSize);
            }
            shots.Add(shot);
            if (!hostile) PlaySfx(playerSound != null ? playerSound : shotSound);
            return shot;
        }
        void UpdateShots(float dt)
        {
            UpdateBlastVisuals(dt);
            BuildProjectileEnemyCells();
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i]; bool remove = false;
                if (shot.weapon != null)
                {
                    if (UpdateWeaponShot(shot, dt))
                    {
                        var impact = SpriteFromEffectPrefab(shot.weapon.impactVisualPrefab);
                        if (impact != null) SpawnEnemyEffect("Player shot impact", impact, shot.position,
                            Vector2.one * Mathf.Max(.35f, shot.weapon.blastRadius * 2),
                            shot.weapon.impactFrames, shot.weapon.impactFramesPerSecond);
                        ReleaseShot(shot); shots.RemoveAt(i);
                    }
                    continue;
                }
                int steps = Mathf.Max(1, Mathf.CeilToInt(shot.velocity.magnitude * dt / .08f));
                shot.age += dt;
                if (shot.hostile && shot.computerLaserSphere && shot.age < 4f &&
                    shot.age >= shot.nextLaserSphereTurn)
                {
                    var target = ClosestPlayer(shot.position);
                    if (target != null)
                    {
                        Vector2 direction = target.position - shot.position;
                        if (direction.sqrMagnitude > .0001f)
                            shot.velocity = direction.normalized * shot.velocity.magnitude;
                    }
                    shot.nextLaserSphereTurn = shot.age + .2f;
                }
                Vector2 delta = shot.velocity * dt / steps;
                for (int s = 0; s < steps && !remove; s++)
                {
                    shot.position += delta; shot.remaining -= delta.magnitude;
                    if (shot.lobbedBomb) shot.travel += delta.magnitude;
                    if (shot.hostile && TryReflectEnemyShot(shot)) delta = shot.velocity * dt / steps;
                    remove = shot.remaining <= 0 || Mathf.Abs(shot.position.x) > stage.size.x / 2 || Mathf.Abs(shot.position.y) > stage.size.y / 2;
                    if (shot.lobbedBomb && (shot.remaining <= 0 || remove))
                    {
                        DetonateEnemyBomb(shot);
                        remove = true;
                        break;
                    }
                    if (!shot.hostile && TryDamageStageObject(shot.position, shot.damage)) remove = true;
                    if (!shot.elevated && ProjectileWallBlocked(shot.position))
                    {
                        remove = true;
                        if (shot.lobbedBomb) DetonateEnemyBomb(shot);
                    }
                    if (remove) break;
                    if (shot.hostile)
                    {
                        foreach (var p in players)
                            if (p.HasActive && (shot.position - p.position).sqrMagnitude <
                                (shot.computerLaserSphere ? .16f : .09f))
                            {
                                if (!shot.elevated && IsPlayerShelteredFromGroundShots(p.position)) continue;
                                if (shot.lobbedBomb) DetonateEnemyBomb(shot);
                                else HitPlayer(p);
                                remove = true; break;
                            }
                    }
                    else if (HasProjectileEnemyNearby(shot.position, .12f)) for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        var enemy = enemies[e];
                        if (!enemy.visual.enabled || !enemy.vulnerable || (enemy.data.elevated && !shot.elevated)) continue;
                        if (!ShotTouchesEnemy(enemy, shot.position, .12f)) continue;
                        enemy.lastHitPlayer=shot.owner; DamageEnemy(enemy, shot.damage, 0); remove = true;
                        break;
                    }
                }
                float arcHeight = shot.lobbedBomb && shot.trajectoryRange > 0
                    ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(shot.travel / shot.trajectoryRange)) * .72f : 0;
                shot.visual.transform.position = shot.position + Vector2.up * arcHeight;
                if (shot.directionalSprite)
                    shot.visual.transform.rotation = Quaternion.Euler(0, 0,
                        Mathf.Atan2(shot.velocity.y, shot.velocity.x) * Mathf.Rad2Deg - 90);
                else if (shot.spinDegreesPerSecond != 0)
                    shot.visual.transform.rotation = Quaternion.Euler(0, 0,
                        shot.age * shot.spinDegreesPerSecond);
                if (shot.animationFrames != null && shot.animationFrames.Length > 0)
                {
                    var frame = shot.animationFrames[Mathf.FloorToInt(shot.age * shot.animationFramesPerSecond) % shot.animationFrames.Length];
                    if (frame != null) shot.visual.sprite = frame;
                }
                if (remove)
                {
                    if (shot.impactSprite != null) SpawnEnemyEffect("Enemy shot impact", shot.impactSprite,
                        shot.position, shot.impactSize, shot.impactFrames, shot.impactFramesPerSecond);
                    ReleaseShot(shot); shots.RemoveAt(i);
                }
                if (state != ScreenState.Playing) return;
            }
        }
        void ClearStage()
        {
            if (state != ScreenState.Playing) return;
            CancelStageIntro();
            BuildStageScore();
            state = stageIndex == stages.Length - 1 ? ScreenState.Victory : ScreenState.Cleared;
            BeginVictoryMusicDownload();
            pendingEndingScore = state == ScreenState.Victory;
            message = rescued ? "Fase concluida com resgate." : stage.rescue != null ? "Fase concluida." : "Fase concluida.";
        }
        void Continue()
        {
            if (state == ScreenState.Title) StartGame(1);
            else if (state == ScreenState.Cleared) { stageIndex++; LoadStage(); }
            else if (state == ScreenState.Defeat) NewCampaign();
            else if (state == ScreenState.Victory) ReturnToMenu();
        }

        void OnGUI()
        {
            if (UsesMobileControls) { DrawMobileGUI(); return; }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                smallStyle = new GUIStyle(labelStyle) { fontSize = 13 };
                controlStyle = new GUIStyle(smallStyle) { fontSize = 11 };
            }
            var old = GUI.matrix; float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f);
            var origin = new Vector2((Screen.width - 1000 * scale) / 2, (Screen.height - 700 * scale) / 2);
            GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
            if(saveBrowserOpen){DrawSaveSlots(new Vector2(1000,700),false);GUI.matrix=old;return;}
            if (state == ScreenState.Loading) { DrawStageLoading(new Rect(215, 175, 570, 365)); GUI.matrix = old; return; }
            if (state == ScreenState.Title && titleView != null) { GUI.matrix = old; return; }
            if (state == ScreenState.StageSelect && stageSelectView != null && !bindingsOpen)
            { GUI.matrix = old; return; }
            if (state == ScreenState.Title || state == ScreenState.Options || state == ScreenState.StageSelect)
            {
                DrawMenu(); GUI.matrix = old; return;
            }
            if (stage == null) { GUI.matrix = old; return; }
            if (endingAnimationPlaying || (state == ScreenState.Victory && !pendingEndingScore))
            { DrawEndingScreen(new Vector2(1000,700)); GUI.matrix = old; return; }
            foreach (var p in players)
            {
                var color = GUI.color;
                if (p.HasActive && gameCamera != null)
                {
                    Vector3 screen = gameCamera.WorldToScreenPoint(p.position + Vector2.up * .65f);
                    var point = (new Vector2(screen.x, Screen.height - screen.y) - origin) / scale;
                    GUI.color = p.Tint;
                    GUI.Label(new Rect(point.x - 22, point.y - 12, 44, 24), "P" + (p.id + 1), smallStyle);
                    GUI.color = color;
                }
            }
            if (state == ScreenState.Defeat || state == ScreenState.GameOver)
            { DrawContinueScreen(new Rect(215,175,570,365)); GUI.matrix = old; return; }
            if (state != ScreenState.Playing || paused)
            {
                DrawSelectionPanel(new Rect(215, 175, 570, 365));
                string heading = paused ? "PAUSA" : state == ScreenState.Title ? "GAIN GROUND" : state == ScreenState.Cleared ? "FASE CONCLUIDA" : state == ScreenState.Victory ? "CAMPANHA CONCLUIDA" : "FIM DE JOGO";
                GUI.Label(new Rect(230, 196, 540, 50), heading, SelectionHeading);
                if (!paused && (state == ScreenState.Cleared || pendingEndingScore)) DrawStageScoreSummary();
                if (pendingEndingScore) { GUI.matrix = old; return; }
                if (paused)
                {
                    GUI.Label(new Rect(310, 252, 120, 30), "BGM  " + settings.BGM, GoldMenuText(labelStyle));
                    SetPauseVolume(true, Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(450, 264, 220, 25),
                        settings.BGM, 0, 100, GUI.skin.horizontalSlider, SwordSliderThumb)));
                    GUI.Label(new Rect(310, 294, 120, 30), "SFX  " + settings.SFX, GoldMenuText(labelStyle));
                    SetPauseVolume(false, Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(450, 306, 220, 25),
                        settings.SFX, 0, 100, GUI.skin.horizontalSlider, SwordSliderThumb)));
                }
                if (IronButton(new Rect(340, paused ? 350 : 407, 320, paused ? 42 : 45), paused ? "CONTINUAR" : state == ScreenState.Title ? "JOGAR [ENTER]" : state == ScreenState.Cleared ? "CONTINUAR [ENTER]" : "JOGAR NOVAMENTE [ENTER]"))
                { if (paused) paused = false; else Continue(); }
                if (paused && IronButton(new Rect(340, 398, 320, 42),
                    "Tiro Automático: " + (settings.AutoFire ? "Ligado" : "Desligado"))) ToggleAutoFire();
                if(state==ScreenState.Cleared||state==ScreenState.Victory)
                {if(IronButton(new Rect(340,461,320,40),"Salvar e voltar para o menu"))OpenSaveSlots(true);}
                else if (IronButton(new Rect(340, paused ? 446 : 484, 320, paused ? 42 : 35), "Menu Principal")) ReturnToMenu();
                if (paused && !string.IsNullOrEmpty(settingsError))
                    GUI.Label(new Rect(280, 493, 440, 38), settingsError, GoldMenuText(smallStyle));
            }
            GUI.matrix = old;
        }
    }
}
