using System.Collections.Generic;
using UnityEngine;

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
        public bool useFullRoster;
        public bool hasFinished;
        public Sprite floorSprite, wallSprite, enemySprite, projectileSprite, exitSprite;
        public Camera gameCamera;
        public AudioClip titleMusic;

        enum ScreenState { Title, Options, StageSelect, Loading, Playing, Cleared, Defeat, Victory, GameOver }
        sealed class Member { public CharacterDefinition definition; public bool lost, escaped; public float dashCooldown; }
        sealed class PlayerState
        {
            public int boomerangLaunches, score;
            public int id, active = -1, fallenMember = -1, selectionIndex;
            public bool awaitingSelection;
            public Vector2 selectionScroll;
            public readonly List<Member> party = new List<Member>();
            public Vector2 position, facing = Vector2.up, dashDirection;
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
            public bool carryingFallen => carriedAlly != null;
            public bool HasActive => active >= 0 && active < party.Count && !party[active].lost && !party[active].escaped;
            public Color Tint => id == 0 ? new Color(.55f, .85f, 1) : new Color(1, .7f, .35f);
        }
        public struct PlayerInput
        {
            public Vector2 movement;
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
            public bool vulnerable = true, triggered, waterConfused;
            public SpriteRenderer visual;
        }
        sealed class Shot
        {
            public Vector2 position, velocity, origin, launchDirection;
            public float boomerangSide;
            public float remaining, age, orbitAngle, travel;
            public bool hostile, elevated, special, returning;
            public int damage;
            public SpriteRenderer visual;
            public WeaponDefinition weapon;
            public Sprite[] animationFrames;
            public float animationFramesPerSecond;
            public Sprite impactSprite;
            public Vector2 impactSize;
            public PlayerState owner;
            public CharacterDefinition character;
            public readonly Dictionary<Enemy, float> hits = new Dictionary<Enemy, float>();
        }
        readonly List<PlayerState> players = new List<PlayerState>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
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
        UnityEngine.Object[] persistentResources;
        bool restartMusic, musicPaused;
#if UNITY_WEBGL && !UNITY_EDITOR
        bool titleMusicInteractionHandled;
#endif
        public int EnemyCount => enemies.Count;
        public bool IsPlaying => state == ScreenState.Playing;

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
            if (streamStageMedia) { BeginStageMediaLoad(); return; }
            BuildStage();
        }

        void BuildStage()
        {
            ResetMobileInput();
            restartMusic = true;
            if (arena != null) { arena.gameObject.SetActive(false); Destroy(arena.gameObject); }
            arena = new GameObject("Arena (generated from editable stage asset)").transform;
            arena.SetParent(transform); enemies.Clear(); shots.Clear(); blastVisuals.Clear(); extraCaptives.Clear(); hazardStates.Clear();
            stage = stages[stageIndex]; remainingTime = stage.timeLimit;
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
                p.selectionIndex = Mathf.Max(0, p.active);
                if (p.awaitingSelection) p.active = -1;
                p.position = SpawnPosition(p); p.facing = Vector2.up;
            }
            if (stage.background != null) Visual("Original stage terrain", stage.background, Vector2.zero, stage.size, Color.white, -10);
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
                var enemy = new Enemy { position = spawn.position, data = spawn, health = DifficultyRules.EnemyHealth(campaignDifficulty, spawn.health, spawn.boss), cooldown = 1 + enemies.Count * .17f, spawnIndex = enemies.Count };
                enemy.visual = Visual(spawn.boss ? "Boss " + spawn.behavior : spawn.behavior.ToString(), spawn.sprite != null ? spawn.sprite : enemySprite,
                    enemy.position, spawn.sprite != null ? spawn.visualSize : new Vector2(.65f, .8f),
                    spawn.sprite != null ? Color.white : spawn.elevated ? new Color(.6f, .8f, 1) : spawn.melee ? Color.white : new Color(1, .75f, .35f), 2);
                enemy.visual.enabled = spawn.spawnDelay <= 0;
                enemy.model = PS2EnemyVisual.Bind(enemy.visual, spawn);
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
                gameCamera.rect = new Rect(0, 0, 1, 1);
                gameCamera.clearFlags = CameraClearFlags.SolidColor;
                gameCamera.orthographicSize = Mathf.Max(stage.size.y / 2 + 3.7f,
                    (stage.size.x / 2 + .7f) / gameCamera.aspect);
                gameCamera.transform.position = new Vector3(0, -.35f, -10);
            }
            if (hudView != null) hudView.InvalidateLayout();
            state = ScreenState.Playing; message = stage.noExit ? "Eliminem todos os inimigos e o chefe." : "Eliminem os inimigos ou levem todas as equipes ate EXIT.";
            CheckTeamOutcome();
            FinishRestoredStage();
        }

        void LateUpdate()
        {
            EnsureMusicSource();
            bool menuMusic = state == ScreenState.Title || state == ScreenState.Options;
            bool stageMusic = stage != null && state == ScreenState.Playing;
            bool previewMusic = state == ScreenState.StageSelect && stages != null &&
                stageSelectIndex >= 0 && stageSelectIndex < stages.Length && stages[stageSelectIndex] != null;
            bool loop = menuMusic || stageMusic || previewMusic;
            AudioClip clip = menuMusic ? titleMusic : previewMusic ? stages[stageSelectIndex].stageMusic :
                stageMusic ? stage.stageMusic :
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
            bool shouldPause = stageMusic && paused;
            if (shouldPause != musicPaused)
            {
                if (shouldPause) musicSource.Pause();
                else musicSource.UnPause();
                musicPaused = shouldPause;
            }
            RefreshTitleUI();
            RefreshStageSelectUI();
            RefreshHud();
        }

        void EnsureMusicSource()
        {
            if (musicSource != null) return;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0;
            musicSource.priority = 32;
            if (gameCamera != null && FindObjectOfType<AudioListener>() == null)
                gameCamera.gameObject.AddComponent<AudioListener>();
        }

        void OnDisable()
        {
            CancelHudPortraitLoad();
            if (hudView != null) { Destroy(hudView.gameObject); hudView = null; }
            if (titleView != null) { Destroy(titleView.gameObject); titleView = null; }
            if (stageSelectView != null) { Destroy(stageSelectView.gameObject); stageSelectView = null; }
            CancelStageMediaRequests();
            ResetMobileInput();
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

        void SpawnEnemyEffect(string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            if (sprite == null) return;
            var renderer = Visual(name, sprite, position,
                size.sqrMagnitude > 0 ? size : Vector2.one * .35f, Color.white, 7);
            renderer.gameObject.AddComponent<EnemyEffectLifetime>().Initialize(renderer);
        }

        void Update()
        {
            PollBindings();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == ScreenState.Title && !titleMusicInteractionHandled &&
                (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0))
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
            if (state == ScreenState.Victory && !pendingEndingScore)
            {
                endingMessageElapsed += Time.unscaledDeltaTime;
                if (endingMessageElapsed >= EndingMessageDuration ||
                    Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape) ||
                    MenuDown(GameAction.Fire) || Input.GetMouseButtonDown(0) ||
                    (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
                    ReturnToMenu();
                return;
            }
            if (state == ScreenState.Defeat || state == ScreenState.GameOver) { UpdateContinueInput(); return; }
            if (state != ScreenState.Playing)
            {
                if (Input.GetKeyDown(KeyCode.Return) || MenuDown(GameAction.Fire)) Continue();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || MenuDown(GameAction.Pause)) paused = !paused;
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
            UpdateEnemies(dt);
            if (state != ScreenState.Playing) return;
            UpdateHazards();
            if (state != ScreenState.Playing) return;
            UpdateShots(dt);
            if (state == ScreenState.Playing && enemies.Count == 0) ClearStage();
        }

        void UpdatePlayer(PlayerState p, PlayerInput input, float dt)
        {
            foreach (var member in p.party) member.dashCooldown = Mathf.Max(0, member.dashCooldown - dt);
            p.dashProtectedThisFrame = false;
            p.fireCooldown -= dt; p.specialCooldown -= dt; p.invulnerability -= dt;
            if (!p.HasActive) { p.dashRequested = false; return; }
            if (input.switchMember) SwitchMember(p);
            var direction = input.movement.normalized;
            if (direction != Vector2.zero) p.facing = direction;
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
            else p.position = ArenaRules.Move(p.position, direction * character.speed * (p.carrying || p.carryingFallen ? .8f : 1) * dt, .23f, stage.size, movementWalls);
            p.player.transform.position = p.position;
            Color tint = p.Tint * character.characterTint;
            if (p.dashProtectedThisFrame) tint = Color.Lerp(tint, Color.white, .7f);
            if (p.invulnerability > 0 && Mathf.FloorToInt(p.invulnerability * 12) % 2 == 0) tint.a = .35f;
            p.player.color = tint;
            if (p.modelBinding != null && p.modelBinding.Model != null)
                p.modelBinding.Model.Tick(dt, p.facing, direction != Vector2.zero, p.dashRemaining > 0, p.player.enabled);
            if (input.fire && p.fireCooldown <= 0)
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
        }
        void SetMember(PlayerState p, int index)
        {
            CancelDash(p); p.awaitingSelection = false; p.active = index;
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
        void UpdateEnemies(float dt)
        {
            foreach (var enemy in enemies)
            {
                if (stage.timeLimit - remainingTime < enemy.data.spawnDelay) continue;
                enemy.visual.enabled = true;
                var before = enemy.position;
                bool frozen = enemy.frozen > 0;
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
                if (!enemy.data.sentry && (enemy.data.melee || (target.position - enemy.position).sqrMagnitude > 3))
                    enemy.position = ArenaRules.Move(enemy.position, direction * enemy.data.speed * dt, .25f, stage.size, movementWalls);
                enemy.visual.transform.position = enemy.position; enemy.cooldown -= dt;
                if (!enemy.data.melee && enemy.cooldown <= 0)
                {
                    PlayEnemyAttackSound(enemy);
                    Fire(enemy.position, direction, true, enemy.data.elevated, 3.2f, 16, 1, new Color(1, .35f, .25f), enemy);
                    enemy.cooldown = enemy.data.shotInterval;
                    if(enemy.model != null) enemy.model.Attack();
                }
                foreach (var p in players)
                    if (p.HasActive && !enemy.data.elevated && (p.position - enemy.position).sqrMagnitude < .25f && HitPlayer(p))
                        PlayEnemyAttackSound(enemy);
                UpdateEnemyModel(enemy, before, dt);
                if (state != ScreenState.Playing) return;
            }
        }
        void UpdateEnemyModel(Enemy enemy, Vector2 before, float dt)
        {
            if(enemy.model == null)return;
            var target=ClosestPlayer(enemy.position);
            var facing=target == null ? Vector2.down : (target.position-enemy.position).normalized;
            enemy.model.Tick(dt,facing,(enemy.position-before).sqrMagnitude>.000001f);
            if (enemy.waterConfused && enemy.frozen > 0 && !enemy.data.boss && enemy.model.rig != null)
                enemy.model.rig.localRotation *= Quaternion.Euler(0, enemy.confusionAngle, 0);
        }
        void Fire(Vector2 origin, Vector2 direction, bool hostile, bool elevated, float speed, float range,
            int damage, Color color, Enemy source = null, AudioClip playerSound = null)
        {
            var shot = new Shot { position = origin, origin = origin, launchDirection = direction.normalized, velocity = direction * speed, remaining = range, hostile = hostile, elevated = elevated, damage = damage };
            var effects = source != null ? source.model : null;
            var frames = effects != null ? effects.projectileFrames : null;
            Sprite sprite = frames != null && frames.Length > 0 && frames[0] != null ? frames[0] :
                effects != null && effects.projectileSprite != null ? effects.projectileSprite : projectileSprite;
            Vector2 size = effects != null && effects.projectileSize.sqrMagnitude > 0 ? effects.projectileSize :
                Vector2.one * (elevated ? .24f : .16f);
            Color tint = effects != null ? color * effects.projectileTint : color;
            shot.visual = Visual(hostile ? "Enemy shot" : "Player shot", sprite, origin, size, tint, 6);
            shot.animationFrames = frames;
            shot.animationFramesPerSecond = effects != null ? effects.projectileFramesPerSecond : 0;
            shot.impactSprite = effects != null ? effects.impactSprite : null;
            shot.impactSize = effects != null ? effects.effectSize : Vector2.zero;
            if (effects != null && effects.muzzleFlashSprite != null)
                SpawnEnemyEffect("Enemy muzzle flash", effects.muzzleFlashSprite, origin, effects.effectSize);
            shots.Add(shot);
            if (!hostile) PlaySfx(playerSound != null ? playerSound : shotSound);
        }
        void UpdateShots(float dt)
        {
            UpdateBlastVisuals(dt);
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i]; bool remove = false;
                if (shot.weapon != null)
                {
                    if (UpdateWeaponShot(shot, dt)) { Destroy(shot.visual.gameObject); shots.RemoveAt(i); }
                    continue;
                }
                int steps = Mathf.Max(1, Mathf.CeilToInt(shot.velocity.magnitude * dt / .08f));
                shot.age += dt;
                Vector2 delta = shot.velocity * dt / steps;
                for (int s = 0; s < steps && !remove; s++)
                {
                    shot.position += delta; shot.remaining -= delta.magnitude;
                    if (shot.hostile && TryReflectEnemyShot(shot)) delta = shot.velocity * dt / steps;
                    remove = shot.remaining <= 0 || Mathf.Abs(shot.position.x) > stage.size.x / 2 || Mathf.Abs(shot.position.y) > stage.size.y / 2;
                    if (!shot.elevated) foreach (var wall in stage.walls) if (ArenaRules.Intersects(wall, shot.position, .06f)) { remove = true; break; }
                    if (remove) break;
                    if (shot.hostile)
                    {
                        foreach (var p in players)
                            if (p.HasActive && (shot.position - p.position).sqrMagnitude < .09f)
                            { HitPlayer(p); remove = true; break; }
                    }
                    else for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        var enemy = enemies[e];
                        if (!enemy.visual.enabled || !enemy.vulnerable || (enemy.data.elevated && !shot.elevated)) continue;
                        if (!ShotTouchesEnemy(enemy, shot.position, .12f)) continue;
                        enemy.lastHitPlayer=shot.owner; DamageEnemy(enemy, shot.damage, 0); remove = true;
                        break;
                    }
                }
                shot.visual.transform.position = shot.position;
                if (shot.animationFrames != null && shot.animationFrames.Length > 0)
                {
                    var frame = shot.animationFrames[Mathf.FloorToInt(shot.age * shot.animationFramesPerSecond) % shot.animationFrames.Length];
                    if (frame != null) shot.visual.sprite = frame;
                }
                if (remove)
                {
                    if (shot.impactSprite != null) SpawnEnemyEffect("Enemy shot impact", shot.impactSprite, shot.position, shot.impactSize);
                    Destroy(shot.visual.gameObject); shots.RemoveAt(i);
                }
                if (state != ScreenState.Playing) return;
            }
        }
        void ClearStage()
        {
            if (state != ScreenState.Playing) return;
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
                if (IronButton(new Rect(340, paused ? 313 : 430, 320, 45), paused ? "CONTINUAR" : state == ScreenState.Title ? "JOGAR [ENTER]" : state == ScreenState.Cleared ? "CONTINUAR [ENTER]" : "JOGAR NOVAMENTE [ENTER]"))
                { if (paused) paused = false; else Continue(); }
                if(state==ScreenState.Cleared||state==ScreenState.Victory)
                {if(IronButton(new Rect(340,484,320,40),"Salvar e voltar para o menu"))OpenSaveSlots(true);}
                else if (IronButton(new Rect(340, paused ? 373 : 484, 320, paused ? 45 : 35), "Menu Principal")) ReturnToMenu();
            }
            GUI.matrix = old;
        }
    }
}
