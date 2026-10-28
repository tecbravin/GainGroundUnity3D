using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        readonly Dictionary<ProjectileKind, Sprite> weaponSprites = new Dictionary<ProjectileKind, Sprite>();
        readonly Dictionary<int, Sprite> swordArcSprites = new Dictionary<int, Sprite>();
        readonly Dictionary<Texture2D, Sprite[]> directionSpriteSheets = new Dictionary<Texture2D, Sprite[]>();
        readonly Dictionary<Texture2D, Sprite[]> weaponEffectSheets = new Dictionary<Texture2D, Sprite[]>();

        void SetPlayerCharacterVisual(PlayerState p, CharacterDefinition character)
        {
            p.currentFacingSprite = -1;
            p.player.sprite = character.sprite;
            if (character.sprite != null)
                p.player.transform.localScale = new Vector3(
                    .6f * PS2VisualBinding.InGameCharacterScale / character.sprite.bounds.size.x,
                    .8f * PS2VisualBinding.InGameCharacterScale / character.sprite.bounds.size.y, 1);
            UpdatePlayerFacingSprite(p, character);
        }

        void UpdatePlayerFacingSprite(PlayerState p, CharacterDefinition character)
        {
            if (character.directionalSpriteSheet == null) return;
            int index = FacingSpriteIndex(p.facing);
            if (p.currentFacingSprite == index) return;
            if (!directionSpriteSheets.TryGetValue(character.directionalSpriteSheet, out var sprites))
            {
                sprites = CreateGridSprites(character.directionalSpriteSheet, 4, 2, 512,
                    "Wanderer direction", new Vector2(.5f, .5f));
                directionSpriteSheets.Add(character.directionalSpriteSheet, sprites);
            }
            if (index >= sprites.Length || sprites[index] == null) return;
            p.player.sprite = sprites[index];
            p.player.transform.localScale = new Vector3(
                .6f * PS2VisualBinding.InGameCharacterScale / sprites[index].bounds.size.x,
                .8f * PS2VisualBinding.InGameCharacterScale / sprites[index].bounds.size.y, 1);
            p.currentFacingSprite = index;
        }

        static int FacingSpriteIndex(Vector2 facing)
        {
            if (facing.sqrMagnitude < .001f) return 2;
            int index = Mathf.RoundToInt(Mathf.Atan2(facing.y, facing.x) * 4 / Mathf.PI);
            return (index + 8) % 8;
        }

        Sprite[] WeaponEffectSprites(WeaponDefinition weapon)
        {
            if (weapon.effectSpriteSheet == null)
                return weapon.projectileFrames != null && weapon.projectileFrames.Length > 0
                    ? weapon.projectileFrames : null;
            if (!weaponEffectSheets.TryGetValue(weapon.effectSpriteSheet, out var sprites))
            {
                sprites = CreateGridSprites(weapon.effectSpriteSheet, weapon.effectColumns, weapon.effectRows,
                    512, "Wanderer sword effect", new Vector2(.5f, .06f));
                weaponEffectSheets.Add(weapon.effectSpriteSheet, sprites);
            }
            return sprites;
        }

        static Sprite[] CreateGridSprites(Texture2D texture, int columns, int rows, float pixelsPerUnit, string name, Vector2 pivot)
        {
            columns = Mathf.Max(1, columns); rows = Mathf.Max(1, rows);
            int width = texture.width / columns, height = texture.height / rows;
            var sprites = new Sprite[columns * rows];
            for (int i = 0; i < sprites.Length; i++)
            {
                int column = i % columns, rowFromTop = i / columns;
                var rect = new Rect(column * width, texture.height - (rowFromTop + 1) * height, width, height);
                sprites[i] = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
                sprites[i].name = name + " " + i;
            }
            return sprites;
        }

        AudioClip CharacterWeaponSound(CharacterDefinition character, WeaponDefinition weapon, bool special)
        {
            var sound = special ? character.specialAttackSound : character.normalAttackSound;
            return sound != null ? sound : weapon.fireSound != null ? weapon.fireSound : shotSound;
        }

        bool FireWeapon(PlayerState p, WeaponDefinition weapon, bool special)
        {
            var character = p.party[p.active].definition;
            if (weapon.mode != WeaponMode.Projectile)
            {
                if (p.modelBinding != null && p.modelBinding.Model != null) p.modelBinding.Model.Attack(special);
                p.swordSweepWeapon = weapon;
                p.swordSweepVisualDuration = weapon.effectDuration > .01f ? weapon.effectDuration :
                    weapon.mode == WeaponMode.MeleeArc ? .24f : weapon.duration;
                p.swordSweepRemaining = weapon.mode == WeaponMode.MeleeArc ? p.swordSweepVisualDuration : weapon.duration;
                p.reflectingProjectiles = weapon.mode == WeaponMode.ReflectEnemyProjectiles;
                ShowSwordSweep(p, weapon);
                if (weapon.mode == WeaponMode.MeleeArc) DamageInSwordArc(p, weapon);
                PlaySfx(CharacterWeaponSound(character, weapon, special));
                return true;
            }
            int active = 0;
            foreach (var shot in shots)
                if (shot.owner == p && shot.character == character && shot.special == special) active++;
            if (active + weapon.ProjectileCount > weapon.maxActive) return false;
            if (p.modelBinding != null && p.modelBinding.Model != null) p.modelBinding.Model.Attack(special);
            for (int i = 0; i < weapon.ProjectileCount; i++)
            {
                var direction = weapon.Direction(p.facing, i);
                var shot = RentShot();
                shot.position = shot.origin = p.position;
                shot.velocity = direction * weapon.speed;
                shot.remaining = weapon.range; shot.weapon = weapon; shot.owner = p;
                shot.character = character; shot.special = special;
                shot.damage = weapon.damage; shot.elevated = weapon.lobbed;
                shot.launchDirection = direction;
                shot.boomerangSide = weapon.kind == ProjectileKind.Boomerang ?
                    (p.boomerangLaunches++ % 2 == 0 ? 1 : -1) : 0;
                shot.orbitAngle = i * Mathf.PI * .5f;
                if (weapon.kind == ProjectileKind.FireTrap || weapon.kind == ProjectileKind.WaterTrap)
                {
                    shot.position = ArenaRules.Move(p.position, direction * weapon.range, .1f, stage.size, stage.walls);
                    shot.velocity = Vector2.zero;
                }
                shot.animationFrames = WeaponEffectSprites(weapon);
                shot.animationFramesPerSecond = weapon.projectileFramesPerSecond;
                Sprite prefabSprite = SpriteFromEffectPrefab(weapon.projectileVisualPrefab);
                Sprite shotSprite = shot.animationFrames != null && shot.animationFrames.Length > 0 && shot.animationFrames[0] != null
                    ? shot.animationFrames[0] : prefabSprite != null ? prefabSprite : WeaponSprite(weapon.kind);
                shot.visual = AcquireShotVisual(weapon.label, shotSprite, shot.position,
                    WeaponSize(weapon), shot.animationFrames != null || prefabSprite != null ||
                    weapon.kind == ProjectileKind.WaterTrap || weapon.kind == ProjectileKind.Boomerang ? Color.white : weapon.color, 6);
                shots.Add(shot);
            }
            PlaySfx(CharacterWeaponSound(character, weapon, special));
            return true;
        }

        void DamageInSwordArc(PlayerState p, WeaponDefinition weapon)
        {
            Vector2 facing = p.facing.sqrMagnitude > .001f ? p.facing.normalized : Vector2.up;
            float minDot = Mathf.Cos(Mathf.Clamp(weapon.arcDegrees, 1, 360) * .5f * Mathf.Deg2Rad);
            for (int i = destructibleObjects.Count - 1; i >= 0; i--)
            {
                var item = destructibleObjects[i];
                Vector2 point = new Vector2(Mathf.Clamp(p.position.x, item.data.area.xMin, item.data.area.xMax),
                    Mathf.Clamp(p.position.y, item.data.area.yMin, item.data.area.yMax));
                Vector2 offset = point - p.position;
                if (offset.sqrMagnitude > weapon.range * weapon.range ||
                    offset.sqrMagnitude < .0001f || Vector2.Dot(facing, offset.normalized) < minDot) continue;
                DamageStageObject(item, weapon.damage);
            }
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                Vector2 offset = enemy.position - p.position;
                if (!enemy.visual.enabled || !enemy.vulnerable || offset.magnitude > weapon.range ||
                    offset.sqrMagnitude < .0001f || Vector2.Dot(facing, offset.normalized) < minDot) continue;
                if (!weapon.lobbed && WallBetween(p.position, enemy.position)) continue;
                enemy.lastHitPlayer = p;
                DamageEnemy(enemy, weapon.damage, weapon.freezeSeconds);
            }
        }

        void ShowSwordSweep(PlayerState p, WeaponDefinition weapon)
        {
            p.swordSweepFrames = WeaponEffectSprites(weapon);
            Sprite initialFrame = p.swordSweepFrames != null && p.swordSweepFrames.Length > 0
                ? p.swordSweepFrames[0] : SwordArcSprite(weapon.arcDegrees);
            if (p.swordSweepVisual == null)
                p.swordSweepVisual = Visual("Sword sweep", initialFrame, p.position,
                    Vector2.one * weapon.range * 2 * Mathf.Max(.1f, weapon.effectScale), Color.white, 7);
            p.swordSweepVisual.sprite = initialFrame;
            p.swordSweepVisual.transform.localScale = Vector3.one * weapon.range * 2 * Mathf.Max(.1f, weapon.effectScale);
            UpdateSwordSweepVisual(p);
        }

        void UpdateSwordSweepVisual(PlayerState p)
        {
            if (p.swordSweepVisual == null || p.swordSweepWeapon == null) return;
            p.swordSweepVisual.transform.position = p.position;
            float facingAngle = Mathf.Atan2(p.facing.y, p.facing.x) * Mathf.Rad2Deg - 90;
            float rotationProgress = p.reflectingProjectiles
                ? 1 - Mathf.Clamp01(p.swordSweepRemaining / p.swordSweepWeapon.duration) : 0;
            p.swordSweepVisual.transform.rotation = Quaternion.Euler(0, 0, facingAngle - 180 * rotationProgress);
            p.swordSweepVisual.transform.localScale = Vector3.one * p.swordSweepWeapon.range * 2 *
                Mathf.Max(.1f, p.swordSweepWeapon.effectScale);
            if (p.swordSweepFrames != null && p.swordSweepFrames.Length > 0)
            {
                float progress = 1 - Mathf.Clamp01(p.swordSweepRemaining / Mathf.Max(.01f, p.swordSweepVisualDuration));
                int frame = Mathf.Min(p.swordSweepFrames.Length - 1, Mathf.FloorToInt(progress * p.swordSweepFrames.Length));
                p.swordSweepVisual.sprite = p.swordSweepFrames[frame];
                p.swordSweepVisual.color = Color.white;
            }
            else p.swordSweepVisual.color = p.reflectingProjectiles ? new Color(.3f, 1, .8f, .72f) : new Color(1, .78f, .35f, .72f);
        }

        bool TryReflectEnemyShot(Shot shot)
        {
            foreach (var p in players)
            {
                if (!p.HasActive || !p.reflectingProjectiles || p.swordSweepWeapon == null) continue;
                Vector2 offset = shot.position - p.position;
                float range = p.swordSweepWeapon.range + .12f;
                if (offset.sqrMagnitude > range * range) continue;
                if (offset.sqrMagnitude > .0001f)
                {
                    float dot = Vector2.Dot(p.facing.normalized, offset.normalized);
                    if (dot < Mathf.Cos(p.swordSweepWeapon.arcDegrees * .5f * Mathf.Deg2Rad)) continue;
                }
                shot.hostile = false;
                shot.owner = p;
                shot.velocity = -shot.velocity;
                shot.remaining = Mathf.Max(.35f, Vector2.Distance(shot.position, shot.origin) + .15f);
                shot.travel = 0;
                shot.visual.color = new Color(.3f, 1, .8f, 1);
                var glow = shot.visual.transform.Find("Projectile glow");
                if (glow != null)
                    glow.GetComponent<SpriteRenderer>().color = new Color(.12f, 1, .8f, .7f);
                return true;
            }
            return false;
        }

        Sprite SwordArcSprite(float degrees)
        {
            int key = Mathf.Clamp(Mathf.RoundToInt(degrees), 1, 360);
            if (swordArcSprites.TryGetValue(key, out var existing)) return existing;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "Sword arc " + key };
            float halfAngle = key * .5f * Mathf.Deg2Rad;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - (size - 1) * .5f, dy = y - (size - 1) * .5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy) / (size * .5f);
                    float angle = Mathf.Abs(Mathf.Atan2(dx, dy));
                    float edgeFade = 1 - Mathf.SmoothStep(.82f, 1, radius);
                    bool arc = radius > .28f && radius < 1 && angle <= halfAngle;
                    texture.SetPixel(x, y, arc ? new Color(1, 1, 1, .8f * edgeFade) : Color.clear);
                }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            swordArcSprites.Add(key, sprite);
            return sprite;
        }

        static Vector2 WeaponSize(WeaponDefinition w)
        {
            switch (w.kind)
            {
                case ProjectileKind.Spear: case ProjectileKind.Arrow: return new Vector2(.12f, .55f);
                case ProjectileKind.Boomerang: return Vector2.one * .55f;
                case ProjectileKind.Bullet: return new Vector2(.1f, .26f);
                case ProjectileKind.Rocket: return new Vector2(.25f, .48f);
                case ProjectileKind.FireTrap: case ProjectileKind.WaterTrap: return Vector2.one * w.radius * 2;
                case ProjectileKind.Tornado: return new Vector2(.65f, .85f);
                case ProjectileKind.Flame: return new Vector2(.5f, .6f);
                default: return Vector2.one * .36f;
            }
        }

        static float ShotHitRadius(WeaponDefinition weapon)
        {
            if (!weapon.lobbed) return weapon.radius;
            return Mathf.Max(.3f, weapon.radius);
        }

        bool UpdateWeaponShot(Shot shot, float dt)
        {
            var w = shot.weapon;
            shot.age += dt;
            bool zone = w.kind == ProjectileKind.FireTrap || w.kind == ProjectileKind.WaterTrap;
            bool orbit = w.kind == ProjectileKind.Orbit;
            if (orbit && (!shot.owner.HasActive || shot.owner.party[shot.owner.active].definition != shot.character))
                return true;
            if ((zone || orbit || w.kind == ProjectileKind.Tornado) && shot.age >= w.duration) return true;
            // Guarantees finite lifetime even if a returning boomerang cannot reach a moving owner.
            if (shot.age > w.duration + 2 * w.range / Mathf.Max(.1f, w.speed) + 2) return true;
            int steps = Mathf.Max(1, Mathf.CeilToInt((orbit ? 8 : w.speed) * dt / .08f));
            for (int s = 0; s < steps; s++)
            {
                float step = dt / steps;
                Vector2 previous = shot.position;
                if (orbit)
                {
                    shot.orbitAngle += step * 5;
                    shot.position = shot.owner.position + new Vector2(Mathf.Cos(shot.orbitAngle), Mathf.Sin(shot.orbitAngle)) * w.range;
                }
                else if (!zone)
                {
                    if (w.kind == ProjectileKind.Boomerang && shot.remaining <= 0 && !shot.returning)
                    {
                        shot.returning = true; shot.hits.Clear();
                    }
                    if (shot.returning)
                    {
                        Vector2 target = shot.owner.HasActive ? shot.owner.position : shot.origin;
                        if (Vector2.Distance(target, shot.position) < .2f) return true;
                        shot.velocity = (target - shot.position).normalized * w.speed;
                    }
                    if (w.kind == ProjectileKind.Boomerang && !shot.returning)
                    {
                        float distance = Mathf.Min(w.range, shot.travel + w.speed * step);
                        float straight = w.range * .55f;
                        float radius = (w.range - straight) / Mathf.PI;
                        var left = new Vector2(-shot.launchDirection.y, shot.launchDirection.x) * shot.boomerangSide;
                        float arcAngle = Mathf.Clamp((distance - straight) / radius, 0, Mathf.PI);
                        shot.position = shot.origin + shot.launchDirection * (Mathf.Min(distance, straight) + radius * Mathf.Sin(arcAngle))
                            + left * radius * (1 - Mathf.Cos(arcAngle));
                        shot.velocity = (shot.launchDirection * Mathf.Cos(arcAngle) + left * Mathf.Sin(arcAngle)) * w.speed;
                    }
                    else shot.position += shot.velocity * step;
                    shot.remaining -= w.speed * step; shot.travel += w.speed * step;
                }
                if (!zone && !orbit && StageObjectBlocks(shot.position))
                {
                    if (w.blastRadius > 0) Explode(shot);
                    else TryDamageStageObject(shot.position, w.damage);
                    return true;
                }
                bool blocked = false;
                if (!w.lobbed && !zone) blocked = ProjectileWallBlocked(shot.position);
                bool outside = Mathf.Abs(shot.position.x) > stage.size.x / 2 || Mathf.Abs(shot.position.y) > stage.size.y / 2;
                if (blocked || outside)
                {
                    if (w.kind == ProjectileKind.Tornado)
                    {
                        // Reflect on the obstructed axis; keep the projectile outside the solid wall.
                        var xPoint = new Vector2(shot.position.x, previous.y);
                        bool xBlocked = Mathf.Abs(xPoint.x) > stage.size.x / 2;
                        xBlocked |= ProjectileWallBlocked(xPoint);
                        shot.velocity = xBlocked ? new Vector2(-shot.velocity.x, shot.velocity.y) : new Vector2(shot.velocity.x, -shot.velocity.y);
                        shot.position = previous;
                    }
                    else if (w.kind == ProjectileKind.Boomerang && !shot.returning)
                    {
                        shot.position = previous; shot.remaining = 0;
                        shot.returning = true; shot.hits.Clear();
                    }
                    else if (orbit) { shot.position = previous; continue; }
                    else { Explode(shot); return true; }
                }
                float progress = Mathf.Clamp01(shot.travel / w.range);
                float hitRadius = ShotHitRadius(w);
                if (HasProjectileEnemyNearby(shot.position, hitRadius))
                    for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        var enemy = enemies[e];
                        if (!enemy.visual.enabled || !enemy.vulnerable || !w.HitsHeight(enemy.data.elevated, progress)) continue;
                        if (!ShotTouchesEnemy(enemy, shot.position, hitRadius)) continue;
                        if (!w.lobbed && WallBetween(shot.position, enemy.position)) continue;
                        if (shot.hits.TryGetValue(enemy, out float nextHit) && shot.age < nextHit) continue;
                        if (w.blastRadius > 0) { Explode(shot); return true; }
                        enemy.lastHitPlayer=shot.owner; DamageEnemy(enemy, w.damage, w.freezeSeconds);
                        if (w.kind == ProjectileKind.WaterTrap && !enemy.data.boss && enemy.frozen > 0)
                            enemy.waterConfused = true;
                        shot.hits[enemy] = zone || orbit || w.kind == ProjectileKind.Flame ? shot.age + .4f : float.PositiveInfinity;
                        if (!w.pierces && !zone && !orbit) return true;
                    }
                if (!zone && !orbit && !shot.returning && shot.remaining <= 0 &&
                    w.kind != ProjectileKind.Boomerang && w.kind != ProjectileKind.Tornado)
                { Explode(shot); return true; }
            }
            float height = w.lobbed ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(shot.travel / w.range)) * .8f : 0;
            shot.visual.transform.position = shot.position + Vector2.up * height;
            float angle = Mathf.Atan2(shot.velocity.y, shot.velocity.x) * Mathf.Rad2Deg - 90;
            if (w.kind == ProjectileKind.Boomerang || w.kind == ProjectileKind.Orbit) angle = shot.age * 720;
            if (zone || w.kind == ProjectileKind.Tornado) angle = 0;
            if (w.projectileSpinDegreesPerSecond != 0) angle += shot.age * w.projectileSpinDegreesPerSecond;
            shot.visual.transform.rotation = Quaternion.Euler(0, 0, angle);
            if (w.kind == ProjectileKind.WaterTrap) AnimateWater(shot);
            else if (shot.animationFrames != null && shot.animationFrames.Length > 0)
            {
                var frame = shot.animationFrames[Mathf.FloorToInt(shot.age * shot.animationFramesPerSecond) % shot.animationFrames.Length];
                if (frame != null) shot.visual.sprite = frame;
            }
            return false;
        }

        void DamageEnemy(Enemy enemy, int damage, float freeze)
        {
            if (!enemy.vulnerable) return;
            bool defeated = enemy.health - damage <= 0;
            if (damage > 0 && (defeated || Time.unscaledTime >= nextImpactSound))
            {
                AudioClip hit = enemy.data.hitSound != null ? enemy.data.hitSound :
                    enemy.model != null ? enemy.model.hitSound : null;
                AudioClip death = enemy.data.deathSound != null ? enemy.data.deathSound :
                    enemy.model != null ? enemy.model.deathSound : null;
                var sound = defeated && death != null ? death : hit;
                PlaySfx(sound != null ? sound : impactSound);
                nextImpactSound = Time.unscaledTime + .06f;
            }
            if (!enemy.data.freezeImmune) enemy.frozen = Mathf.Max(enemy.frozen, freeze);
            enemy.health -= damage;
            if (enemy.health <= 0)
            {
                SpawnSplitEnemies(enemy);
                Destroy(enemy.visual.gameObject); enemies.Remove(enemy);
                AwardPoints(enemy.lastHitPlayer,DifficultyRules.ScorePoints(campaignDifficulty,enemy.data.boss ? 2000 : 200));
                if (enemy.data.obstacleSize.sqrMagnitude > 0) RebuildMovementWalls();
            }
        }

        void SpawnSplitEnemies(Enemy parent)
        {
            var defaults = parent.prefabDefaults;
            if (defaults == null || defaults.splitOnDeathPrefab == null) return;
            var childPrefab = defaults.splitOnDeathPrefab;
            var childDefaults = childPrefab.GetComponent<EnemyPrefabDefaults>();
            if (childDefaults == null)
            {
                Debug.LogError("Split enemy prefab needs EnemyPrefabDefaults: " + childPrefab.name);
                return;
            }
            float offset = defaults.splitOffset > 0 ? defaults.splitOffset : .5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 position = ArenaRules.Move(parent.position, Vector2.right * (side * offset),
                    .12f, stage.size, movementWalls);
                var source = parent.data;
                var spawn = new EnemySpawn
                {
                    position = position,
                    health = Mathf.Max(1, childDefaults.health),
                    speed = childDefaults.speed > 0 ? childDefaults.speed : parent.moveSpeed * 1.5f,
                    shotInterval = source.shotInterval,
                    melee = true,
                    behavior = EnemyBehavior.Slime,
                    walkingPattern = EnemyWalkingPattern.TowardPlayers,
                    sprite = source.sprite,
                    modelPrefab = childPrefab,
                    visualSize = source.visualSize * .5f,
                    hitRadius = Mathf.Max(.05f, source.hitRadius * .7f)
                };
                var child = new Enemy
                {
                    position = position, data = spawn, prefabDefaults = childDefaults,
                    health = DifficultyRules.EnemyHealth(campaignDifficulty, spawn.health, false),
                    moveSpeed = spawn.speed, walkingPattern = EnemyWalkingPattern.TowardPlayers,
                    spawnIndex = -1, cooldown = 1f
                };
                child.visual = Visual("MiniSlime", spawn.sprite != null ? spawn.sprite : enemySprite,
                    position, spawn.visualSize, Color.white, 2);
                child.model = PS2EnemyVisual.Bind(child.visual, spawn);
                enemies.Add(child);
            }
        }

        void PlayEnemyAttackSound(Enemy enemy)
        {
            var clip = enemy.data.attackSound != null ? enemy.data.attackSound :
                enemy.model != null ? enemy.model.attackSound : null;
            PlaySfx(clip);
        }

        void Explode(Shot shot)
        {
            var w = shot.weapon;
            if (w.blastRadius <= 0) return;
            PlaySfx(explosionSound);
            float damageRadius = Mathf.Max(w.blastRadius, ShotHitRadius(w));
            DamageStageObjectsInRadius(shot.position, damageRadius, w.damage);
            if (!HasProjectileEnemyNearby(shot.position, damageRadius))
            { SpawnBlast(shot.position, w.blastRadius); return; }
            float progress = Mathf.Clamp01(shot.travel / w.range);
            for (int e = enemies.Count - 1; e >= 0; e--)
            {
                var enemy = enemies[e];
                if (!enemy.visual.enabled || !enemy.vulnerable || !w.HitsHeight(enemy.data.elevated, progress)) continue;
                if (!ShotTouchesEnemy(enemy, shot.position, damageRadius)) continue;
                // Ground explosions cannot damage enemies on the other side of a wall.
                if (!w.lobbed && WallBetween(shot.position, enemy.position)) continue;
                enemy.lastHitPlayer=shot.owner; DamageEnemy(enemy, w.damage, w.freezeSeconds);
            }
            SpawnBlast(shot.position, w.blastRadius);
        }

        bool WallBetween(Vector2 a, Vector2 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .08f));
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(a, b, (float)i / steps);
                if (StageObjectBlocks(point)) return true;
                foreach (var wall in stage.walls)
                    if (wall.Contains(point)) return true;
            }
            return false;
        }

        Sprite WeaponSprite(ProjectileKind kind)
        {
            if (kind == ProjectileKind.Boomerang || kind == ProjectileKind.WaterTrap)
            { EnsureWeaponFX(); return kind == ProjectileKind.Boomerang ? detailedBoomerang : waterFrames[0]; }
            if (weaponSprites.TryGetValue(kind, out var sprite)) return sprite;
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = kind + " projectile" };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    bool fill;
                    switch (kind)
                    {
                        case ProjectileKind.Spear: case ProjectileKind.Arrow:
                            fill = Mathf.Abs(dx) < 1.6f || y > 10 && Mathf.Abs(dx) < (16 - y); break;
                        case ProjectileKind.Bullet: fill = Mathf.Abs(dx) < 3 && y > 1 && y < 15; break;
                        case ProjectileKind.Boomerang: fill = Mathf.Abs(dy - Mathf.Abs(dx) + 4) < 2; break;
                        case ProjectileKind.Rocket: fill = Mathf.Abs(dx) < (y > 11 ? 16 - y : y < 4 ? 6 : 3); break;
                        case ProjectileKind.Tornado: fill = Mathf.Abs(dx) < 2 + y * .35f && y % 4 < 2; break;
                        case ProjectileKind.Flame: case ProjectileKind.FireTrap:
                            fill = Mathf.Abs(dx) < 7 - y * .3f + Mathf.Sin(x * 2 + y) * 2; break;
                        case ProjectileKind.WaterTrap: fill = dx * dx / 49 + dy * dy / 30 < 1 || Mathf.Abs(dx) < 2 && y > 8; break;
                        case ProjectileKind.Orbit: fill = Mathf.Abs(dx) + Mathf.Abs(dy) < 7; break;
                        default: fill = dx * dx + dy * dy < 40; break;
                    }
                    texture.SetPixel(x, y, fill ? (Mathf.Abs(dx) < 2 ? Color.white : new Color(.75f, .75f, .75f)) : Color.clear);
                }
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
            weaponSprites.Add(kind, sprite);
            return sprite;
        }

        void OnDestroy()
        {
            DisposeWeaponFX();
            foreach (var sprite in weaponSprites.Values)
            { Destroy(sprite.texture); Destroy(sprite); }
            weaponSprites.Clear();
            foreach (var sprite in swordArcSprites.Values)
            { Destroy(sprite.texture); Destroy(sprite); }
            swordArcSprites.Clear();
            foreach (var sprites in directionSpriteSheets.Values)
                foreach (var sprite in sprites) if (sprite != null) Destroy(sprite);
            directionSpriteSheets.Clear();
            foreach (var sprites in weaponEffectSheets.Values)
                foreach (var sprite in sprites) if (sprite != null) Destroy(sprite);
            weaponEffectSheets.Clear();
        }
    }
}
