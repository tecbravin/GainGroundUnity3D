using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        sealed class RescueState
        {
            public CaptiveSpawn data;
            public CharacterDefinition character;
            public SpriteRenderer visual;
            public PlayerState carrier;
            public bool secured;
        }
        sealed class HazardState { public HazardSpawn data; public SpriteRenderer visual; }
        sealed class DestructibleStageObject
        {
            public StageObjectSpawn data;
            public SpriteRenderer visual;
            public int health;
        }
        readonly List<RescueState> extraCaptives = new List<RescueState>();
        readonly List<HazardState> hazardStates = new List<HazardState>();
        readonly List<DestructibleStageObject> destructibleObjects = new List<DestructibleStageObject>();
        Rect[] movementWalls = new Rect[0];

        void BuildCampaignObjects()
        {
            destructibleObjects.Clear();
            if (stage.objects != null)
                foreach (var item in stage.objects)
                    if (item != null && item.prefab != null)
                    {
                        var visual = SpawnStageObject(item.prefab, item.area, item.rotationDegrees, item.modelScale);
                        if (item.health > 0)
                            destructibleObjects.Add(new DestructibleStageObject {
                                data = item, visual = visual, health = item.health });
                    }
            RebuildMovementWalls();
            foreach (var enemy in enemies)
                if (enemy.data.behavior == EnemyBehavior.Computer)
                    enemy.vulnerable = !enemies.Exists(other => other.data.behavior == EnemyBehavior.Barrier);
            AddEligibleCaptives();
            foreach (var data in stage.hazards)
                hazardStates.Add(new HazardState { data = data,
                    visual = data.prefab != null ? SpawnStageObject(data.prefab, data.area) :
                        Visual("Energy barrier", data.sprite != null ? data.sprite : floorSprite,
                            data.area.center, data.area.size, Color.green, 3) });
        }

        SpriteRenderer SpawnStageObject(GameObject prefab, Rect area, float rotationDegrees = 0,
            Vector3 modelScale = default)
        {
            var instance = Instantiate(prefab, arena);
            instance.name = prefab.name;
            instance.transform.position = new Vector3(area.center.x, area.center.y, 0);
            instance.transform.localRotation = Quaternion.Euler(0, 0, rotationDegrees);
            var visual = instance.GetComponent<SpriteRenderer>();
            if (visual == null && modelScale != Vector3.zero)
                instance.transform.localScale = modelScale;
            if (visual == null || visual.sprite == null) return visual;
            if (visual.drawMode == SpriteDrawMode.Tiled)
                visual.size = area.size;
            else
                instance.transform.localScale = new Vector3(
                    area.width / visual.sprite.bounds.size.x,
                    area.height / visual.sprite.bounds.size.y, 1);
            return visual;
        }

        void UpdateStageTeleports()
        {
            if (stage.objects == null || stage.objects.Length < 2) return;
            foreach (var player in players)
            {
                if (!player.HasActive) continue;
                StageObjectSpawn entered = null;
                foreach (var item in stage.objects)
                    if (item != null && item.teleportGroup > 0 &&
                        Vector2.Distance(player.position, item.area.center) <
                        Mathf.Min(item.area.width, item.area.height) * .32f)
                    { entered = item; break; }
                if (entered == null) { player.teleportArmed = true; continue; }
                if (!player.teleportArmed) continue;
                foreach (var destination in stage.objects)
                    if (destination != null && destination != entered &&
                        destination.teleportGroup == entered.teleportGroup)
                    {
                        player.position = destination.area.center;
                        player.player.transform.position = player.position;
                        player.teleportArmed = false;
                        break;
                    }
            }
        }

        void AddEligibleCaptives()
        {
            foreach (var data in stage.additionalCaptives)
            {
                if (data.character == null || players.Count < data.minimumPlayers ||
                    extraCaptives.Exists(captive => captive.data == data)) continue;
                var character = RandomRescue(data.character);
                var rescue = new RescueState {
                    data = data, character = character,
                    visual = Visual("Rescue " + character.displayName, character.sprite, data.position,
                        new Vector2(.45f, .6f), new Color(.7f, 1, 1), 3)
                };
                extraCaptives.Add(rescue);
                PS2VisualBinding.Bind(rescue.visual, character);
            }
        }

        void RebuildMovementWalls()
        {
            var walls = new List<Rect>(stage.walls);
            foreach (var item in destructibleObjects)
                if (item.health > 0) walls.Add(item.data.area);
            foreach (var e in enemies)
                if (e.health > 0 && e.data.obstacleSize.sqrMagnitude > 0)
                    walls.Add(new Rect(e.position - e.data.obstacleSize / 2, e.data.obstacleSize));
            movementWalls = walls.ToArray();
        }

        bool TryDamageStageObject(Vector2 point, int damage)
        {
            if (damage <= 0) return false;
            for (int i = destructibleObjects.Count - 1; i >= 0; i--)
            {
                var item = destructibleObjects[i];
                if (item.health <= 0 || !ArenaRules.Intersects(item.data.area, point, .08f)) continue;
                DamageStageObject(item, damage);
                return true;
            }
            return false;
        }

        bool StageObjectBlocks(Vector2 point)
        {
            foreach (var item in destructibleObjects)
                if (item.health > 0 && ArenaRules.Intersects(item.data.area, point, .06f)) return true;
            return false;
        }

        void DamageStageObject(DestructibleStageObject item, int damage)
        {
            item.health -= damage;
            PlaySfx(impactSound);
            if (item.health <= 0)
            {
                if (item.visual != null) Destroy(item.visual.gameObject);
                destructibleObjects.Remove(item);
                RebuildMovementWalls();
            }
            else if (item.visual != null)
                item.visual.color = Color.Lerp(new Color(.75f, .48f, .38f), Color.white,
                    (float)item.health / Mathf.Max(1, item.data.health));
        }

        void DamageStageObjectsInRadius(Vector2 center, float radius, int damage)
        {
            if (damage <= 0) return;
            for (int i = destructibleObjects.Count - 1; i >= 0; i--)
            {
                var item = destructibleObjects[i];
                Vector2 nearest = new Vector2(Mathf.Clamp(center.x, item.data.area.xMin, item.data.area.xMax),
                    Mathf.Clamp(center.y, item.data.area.yMin, item.data.area.yMax));
                if ((nearest - center).sqrMagnitude <= radius * radius) DamageStageObject(item, damage);
            }
        }

        void UpdateAdditionalRescues(PlayerState p)
        {
            foreach (var rescue in extraCaptives)
            {
                if (rescue.secured) continue;
                if (!p.carrying && rescue.carrier == null && Vector2.Distance(p.position, rescue.visual.transform.position) < .65f)
                {
                    rescue.carrier = p; p.additionalCaptive = rescue; p.carrying = true;
                    message = "P" + (p.id+1) + ": leve " + rescue.character.displayName + " ate EXIT.";
                }
                if (rescue.carrier == p) rescue.visual.transform.position = p.position - p.facing * .5f;
            }
        }

        void SecureAdditionalRescue(PlayerState p)
        {
            var rescue = p.additionalCaptive;
            if (rescue == null || rescue.secured || rescue.carrier != p) return;
            rescue.secured = true; rescue.carrier = null;
            Destroy(rescue.visual.gameObject); p.additionalCaptive = null; p.carrying = false;
            p.party.Add(new Member { definition = rescue.character, escaped = true });
            AwardPoints(p,DifficultyRules.ScorePoints(campaignDifficulty,500));
            PlaySfx(rescue.character.rescueSound != null ? rescue.character.rescueSound : rescueSound);
            message = "P" + (p.id+1) + " resgatou " + rescue.character.displayName + ".";
        }

        void DropAdditionalRescue(PlayerState p)
        {
            if (p.additionalCaptive == null) return;
            var rescue = p.additionalCaptive;
            rescue.carrier = null; rescue.visual.transform.position = p.position;
            p.additionalCaptive = null; p.carrying = false;
        }

        void UpdateHazards()
        {
            foreach (var hazard in hazardStates)
            {
                bool active = false;
                foreach (int index in hazard.data.controllers)
                    foreach (var e in enemies)
                        if (e.spawnIndex == index && e.health > 0) active = true;
                if (hazard.visual == null) continue;
                hazard.visual.enabled = active;
                if (!active) continue;
                hazard.visual.color = hazard.data.prefab != null ?
                    new Color(1, 1, 1, .8f + .2f * Mathf.Sin(remainingTime * 15)) :
                    Color.Lerp(Color.green, Color.white, .25f + .2f * Mathf.Sin(remainingTime * 15));
                foreach (var p in players)
                    if (p.HasActive && ArenaRules.Intersects(hazard.data.area,p.position,.2f)) HitPlayer(p);
            }
        }

        static bool ShotTouchesEnemy(Enemy e, Vector2 position, float radius)
        {
            if (e.data.obstacleSize.sqrMagnitude > 0)
                return ArenaRules.Intersects(new Rect(e.position-e.data.obstacleSize/2,e.data.obstacleSize),position,radius);
            return (e.position-position).sqrMagnitude <= Mathf.Pow(e.data.hitRadius+radius,2);
        }

        bool UpdateCampaignEnemy(Enemy e, float dt)
        {
            var behavior = e.data.behavior;
            if (behavior == EnemyBehavior.Legacy && e.walkingPattern == EnemyWalkingPattern.UseBehavior) return false;
            e.age += dt;
            if (e.frozen > 0)
            {
                e.frozen = Mathf.Max(0,e.frozen-dt); e.visual.color = Color.cyan;
                return true;
            }
            e.visual.color = Color.white;
            var target = ClosestPlayer(e.position);
            if (target == null) return true;
            if (behavior == EnemyBehavior.Barrier || behavior == EnemyBehavior.Generator) return true;
            e.vulnerable = true;
            if (behavior == EnemyBehavior.Computer)
            {
                foreach (var other in enemies)
                    if (other.data.behavior == EnemyBehavior.Barrier) { e.vulnerable = false; break; }
                e.visual.color = e.vulnerable ? Color.white : new Color(.4f,.7f,1);
            }
            if (behavior == EnemyBehavior.Wizard || behavior == EnemyBehavior.Burrow)
            {
                float cycle = e.age % 4;
                if (behavior == EnemyBehavior.Wizard && e.data.teleportOnVanishDistance > 0 &&
                    cycle < Mathf.Repeat(e.age - dt, 4f))
                {
                    e.position = WizardTeleportDestination(e.position, target.position,
                        e.data.teleportOnVanishDistance, e.data.hitRadius);
                    e.visual.transform.position = e.position;
                }
                e.vulnerable = cycle >= 1;
                e.visual.color = new Color(1,1,1,e.vulnerable ? 1 : .2f);
                if (!e.vulnerable) return true;
            }
            Vector2 direction = (target.position-e.position).normalized;
            if (behavior == EnemyBehavior.BurstArcher || behavior == EnemyBehavior.WallArcher)
            {
                Vector2 archerPatrolGoal = target.position;
                bool patrolMove = e.walkingPattern == EnemyWalkingPattern.Patrol &&
                    TryGetPatrolGoal(e, target.position, out archerPatrolGoal);
                bool chase = e.walkingPattern == EnemyWalkingPattern.TowardPlayers || e.patrolChasing;
                if (e.walkingPattern != EnemyWalkingPattern.UseBehavior &&
                    e.walkingPattern != EnemyWalkingPattern.Standing && !e.data.elevated &&
                    (e.walkingPattern != EnemyWalkingPattern.Patrol || patrolMove))
                {
                    Vector2 goal = e.walkingPattern == EnemyWalkingPattern.Patrol ? archerPatrolGoal : chase ? target.position :
                        e.walkingPattern == EnemyWalkingPattern.BackAndForth
                            ? e.data.position + Vector2.down * (e.chargeReturning ? 0 : e.walkingDistance)
                            : e.data.position + Vector2.right * (e.chargeReturning ? e.walkingDistance : -e.walkingDistance);
                    Vector2 delta = e.walkingPattern == EnemyWalkingPattern.Patrol ?
                        Vector2.ClampMagnitude(goal - e.position, e.moveSpeed * dt) :
                        (goal - e.position).normalized * e.moveSpeed * dt;
                    Vector2 next = ArenaRules.Move(e.position, delta,
                        .23f, stage.size, movementWalls);
                    if (!chase && e.walkingPattern != EnemyWalkingPattern.Patrol &&
                        ((next-e.position).sqrMagnitude < .00001f || Vector2.Distance(next, goal) < .06f))
                        e.chargeReturning = !e.chargeReturning;
                    if (e.patrolReturning && Vector2.Distance(next, e.data.position) < .06f)
                    { next = e.data.position; e.patrolReturning = false; e.patrolTrail = null; }
                    e.position = next; e.visual.transform.position = next;
                    if (e.walkingPattern == EnemyWalkingPattern.Patrol) RecordPatrolPosition(e);
                }
                e.cooldown -= dt;
                if (e.cooldown <= 0)
                {
                    var visible = behavior == EnemyBehavior.WallArcher ? target : ClosestVisiblePlayer(e);
                    if (visible != null)
                    {
                        direction = (visible.position - e.position).normalized;
                        ShootFan(e, direction, 1, 0, false);
                        if (e.model != null) e.model.Attack();
                        if (behavior == EnemyBehavior.BurstArcher)
                        {
                            if (e.burstShotsRemaining <= 0) e.burstShotsRemaining = 6;
                            e.burstShotsRemaining--;
                            e.cooldown = e.burstShotsRemaining > 0 ? .25f : e.data.shotInterval;
                        }
                        else e.cooldown = e.data.shotInterval;
                    }
                }
                return true;
            }
            bool move = behavior == EnemyBehavior.Chaser || behavior == EnemyBehavior.Slime ||
                behavior == EnemyBehavior.Orb || behavior == EnemyBehavior.Spider ||
                behavior == EnemyBehavior.Ambusher || behavior == EnemyBehavior.ChargeKnight;
            var pattern = e.walkingPattern == EnemyWalkingPattern.UseBehavior && behavior == EnemyBehavior.ChargeKnight
                ? EnemyWalkingPattern.BackAndForth : e.walkingPattern;
            bool patrol = pattern == EnemyWalkingPattern.BackAndForth || pattern == EnemyWalkingPattern.SideToSide;
            Vector2 patrolGoal = target.position;
            if (pattern == EnemyWalkingPattern.Standing) move = false;
            else if (pattern == EnemyWalkingPattern.TowardPlayers) move = true;
            else if (pattern == EnemyWalkingPattern.Patrol)
            {
                move = TryGetPatrolGoal(e, target.position, out patrolGoal);
                direction = (patrolGoal - e.position).normalized;
            }
            else if (patrol)
            {
                move = true;
                Vector2 goal = pattern == EnemyWalkingPattern.BackAndForth
                    ? e.data.position + Vector2.down * (e.chargeReturning ? 0 : e.walkingDistance)
                    : e.data.position + Vector2.right * (e.chargeReturning ? e.walkingDistance : -e.walkingDistance);
                direction = (goal - e.position).normalized;
            }
            if (e.data.triggerDistance > 0)
                e.triggered |= Vector2.Distance(target.position,e.position) <= e.data.triggerDistance;
            else e.triggered = true;
            if (behavior == EnemyBehavior.Tank && pattern != EnemyWalkingPattern.Patrol)
            {
                // The fourth-round tank emerges from the hangar before holding the center.
                if (e.data.spawnDelay > 0 && e.position.y > 0)
                { direction = Vector2.down; move = true; }
            }
            if (move && (pattern == EnemyWalkingPattern.Patrol || e.triggered) && !e.data.elevated)
            {
                Vector2 delta = pattern == EnemyWalkingPattern.Patrol ?
                    Vector2.ClampMagnitude(patrolGoal - e.position, e.moveSpeed * dt) : direction * e.moveSpeed * dt;
                if (behavior == EnemyBehavior.Orb)
                {
                    e.position += delta;
                    e.position = new Vector2(Mathf.Clamp(e.position.x,-stage.size.x/2+.3f,stage.size.x/2-.3f),
                        Mathf.Clamp(e.position.y,-stage.size.y/2+.3f,stage.size.y/2-.3f));
                }
                else
                {
                    var next = ArenaRules.Move(e.position,delta,
                        Mathf.Min(.23f,e.data.hitRadius),stage.size,movementWalls);
                    if (patrol && ((next-e.position).sqrMagnitude < .00001f ||
                        Vector2.Distance(next, pattern == EnemyWalkingPattern.BackAndForth
                            ? e.data.position + Vector2.down * (e.chargeReturning ? 0 : e.walkingDistance)
                            : e.data.position + Vector2.right * (e.chargeReturning ? e.walkingDistance : -e.walkingDistance)) < .06f))
                        e.chargeReturning = !e.chargeReturning;
                    if (!patrol && (next-e.position).sqrMagnitude < .00001f)
                    {
                        var tangent = new Vector2(-direction.y,direction.x) * (e.spawnIndex%2 == 0 ? 1 : -1);
                        next = ArenaRules.Move(e.position,tangent*e.moveSpeed*dt,
                            Mathf.Min(.23f,e.data.hitRadius),stage.size,movementWalls);
                    }
                    e.position=next;
                }
                if (e.patrolReturning && Vector2.Distance(e.position, e.data.position) < .06f)
                { e.position = e.data.position; e.patrolReturning = false; e.patrolTrail = null; }
                if (pattern == EnemyWalkingPattern.Patrol) RecordPatrolPosition(e);
            }
            e.visual.transform.position=e.position;
            if (behavior == EnemyBehavior.Computer && e.age >= e.nextLaserSphereVolley)
            {
                ShootComputerLaserSpheres(e);
                e.nextLaserSphereVolley = e.age + 8f;
            }
            e.cooldown-=dt;
            if (behavior == EnemyBehavior.Giant && e.walkingPattern == EnemyWalkingPattern.Patrol &&
                e.cooldown <= 0 && e.triggered)
            {
                var visiblePlayer = ClosestVisiblePlayer(e);
                if (visiblePlayer != null)
                {
                    direction = (visiblePlayer.position - e.position).normalized;
                    if (e.model != null) e.model.Attack();
                    SpawnElectricShock(e.position, 3f);
                    foreach (var player in players)
                        if (player.HasActive && Vector2.Distance(player.position, e.position) <= 3f && HitPlayer(player))
                            PlayEnemyAttackSound(e);
                    e.cooldown = e.data.shotInterval;
                    e.volley++;
                }
            }
            if (!e.data.melee && e.cooldown<=0 && e.triggered)
            {
                var visible = ClosestVisiblePlayer(e);
                if (visible != null)
                {
                    direction = (visible.position - e.position).normalized;
                    int count = 1; float spread=0; bool radial=false;
                    switch (behavior)
                    {
                        case EnemyBehavior.Spread: case EnemyBehavior.Spider: count=3; spread=15; break;
                        case EnemyBehavior.Dragon: case EnemyBehavior.Sorcerer: count=5; spread=14; break;
                        case EnemyBehavior.Giant when e.walkingPattern != EnemyWalkingPattern.Patrol: count=7; spread=12; break;
                        case EnemyBehavior.Tank: case EnemyBehavior.Orb: count=8; radial=true; break;
                        case EnemyBehavior.Computer: count=8; radial=e.vulnerable; spread=20; break;
                    }
                    ShootFan(e,direction,count,spread,radial);
                    if(e.model != null)e.model.Attack();
                    e.cooldown=e.data.shotInterval;
                    e.volley++;
                }
            }
            foreach (var p in players)
                if (p.HasActive && !e.data.elevated && e.vulnerable && ShotTouchesEnemy(e,p.position,.2f) && HitPlayer(p))
                    PlayEnemyAttackSound(e);
            return true;
        }

        bool TryGetPatrolGoal(Enemy enemy, Vector2 playerPosition, out Vector2 goal)
        {
            float rangeSquared = enemy.patrolDistance * enemy.patrolDistance;
            float separationSquared = (playerPosition - enemy.position).sqrMagnitude;
            if (separationSquared <= rangeSquared)
            {
                if (enemy.patrolTrail == null)
                    enemy.patrolTrail = new List<Vector2>(32) { enemy.position };
                else if (enemy.patrolReturning)
                    enemy.patrolTrail.Add(enemy.position);
                enemy.patrolChasing = true;
                enemy.patrolReturning = false;
            }
            else if (enemy.patrolChasing && separationSquared >= 4f * rangeSquared)
            {
                enemy.patrolChasing = false;
                enemy.patrolReturning = true;
            }
            if (enemy.patrolReturning)
            {
                var trail = enemy.patrolTrail;
                while (trail != null && trail.Count > 0 &&
                    (trail[trail.Count - 1] - enemy.position).sqrMagnitude <= .0036f)
                    trail.RemoveAt(trail.Count - 1);
                goal = trail != null && trail.Count > 0 ? trail[trail.Count - 1] : enemy.data.position;
                if ((goal - enemy.position).sqrMagnitude <= .0025f)
                {
                    enemy.position = enemy.data.position;
                    enemy.visual.transform.position = enemy.position;
                    enemy.patrolReturning = false;
                    enemy.patrolTrail = null;
                    return false;
                }
                return true;
            }
            goal = playerPosition;
            return enemy.patrolChasing;
        }

        static void RecordPatrolPosition(Enemy enemy)
        {
            if (!enemy.patrolChasing || enemy.patrolTrail == null) return;
            var trail = enemy.patrolTrail;
            if (trail.Count == 0 || (trail[trail.Count - 1] - enemy.position).sqrMagnitude >= .0025f)
                trail.Add(enemy.position);
        }

        Vector2 WizardTeleportDestination(Vector2 origin, Vector2 target, float distance, float hitRadius)
        {
            Vector2 forward = (target - origin).normalized;
            if (forward.sqrMagnitude < .001f) return origin;
            // Teleporting crosses walls, but the destination itself must remain walkable.
            for (int angle = 0; angle <= 90; angle += 15)
                for (int side = -1; side <= 1; side += 2)
                {
                    float radians = angle * side * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(
                        forward.x * Mathf.Cos(radians) - forward.y * Mathf.Sin(radians),
                        forward.x * Mathf.Sin(radians) + forward.y * Mathf.Cos(radians));
                    Vector2 candidate = origin + direction * distance;
                    if (!ArenaRules.Blocked(candidate, hitRadius, stage.size, movementWalls)) return candidate;
                }
            return ArenaRules.Move(origin, forward * distance, hitRadius, stage.size, movementWalls);
        }

        void ShootFan(Enemy e, Vector2 direction, int count, float spread, bool radial)
        {
            PlayEnemyAttackSound(e);
            for (int i=0;i<count;i++)
            {
                float angle = radial ? i*360f/count + (e.volley%2)*22.5f : (i-(count-1)*.5f)*spread;
                var d=(Vector2)(Quaternion.Euler(0,0,angle)*(radial?Vector2.up:direction));
                float range = e.data.behavior == EnemyBehavior.WallArcher ? stage.size.y * .5f : 22f;
                Fire(e.position,d,true,e.data.elevated || e.data.behavior == EnemyBehavior.WallArcher,e.data.boss?3.6f:3.2f,range,1,
                    e.data.behavior==EnemyBehavior.Orb?Color.cyan:new Color(1,.35f,.15f),e);
            }
        }

        void ShootComputerLaserSpheres(Enemy computer)
        {
            var visible = ClosestVisiblePlayer(computer);
            if (visible == null) return;
            PlayEnemyAttackSound(computer);
            float sideOffset = computer.data.visualSize.x * .5f + .25f;
            for (int side = -1; side <= 1; side += 2)
            {
                // The side platforms occupy y >= 5; launch just below them so the spheres
                // do not collide with the boss arena walls on their first frame.
                Vector2 origin = computer.position + Vector2.right * (side * sideOffset) +
                    Vector2.down * .5f;
                Vector2 direction = (visible.position - origin).normalized;
                if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
                Fire(origin, direction, true, false, 1.6f, 24f, 1, Color.white, computer);
                var shot = shots[shots.Count - 1];
                shot.computerLaserSphere = true;
                shot.nextLaserSphereTurn = .2f;
                shot.directionalSprite = false;
                shot.animationFrames = null;
                var sprite = computer.model != null ? computer.model.laserSphereSprite : null;
                if (sprite != null)
                {
                    shot.visual.sprite = sprite;
                    shot.visual.color = Color.white;
                    shot.visual.transform.localScale = new Vector3(
                        .65f / sprite.bounds.size.x, .65f / sprite.bounds.size.y, 1);
                }
            }
        }
    }
}
