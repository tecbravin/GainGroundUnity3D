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
        readonly List<RescueState> extraCaptives = new List<RescueState>();
        readonly List<HazardState> hazardStates = new List<HazardState>();
        Rect[] movementWalls = new Rect[0];

        void BuildCampaignObjects()
        {
            RebuildMovementWalls();
            if (stage.objects != null)
                foreach (var item in stage.objects)
                    if (item != null && item.prefab != null)
                        SpawnStageObject(item.prefab, item.area);
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

        SpriteRenderer SpawnStageObject(GameObject prefab, Rect area)
        {
            var instance = Instantiate(prefab, arena);
            instance.name = prefab.name;
            instance.transform.position = new Vector3(area.center.x, area.center.y, 0);
            var visual = instance.GetComponent<SpriteRenderer>();
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
            foreach (var e in enemies)
                if (e.health > 0 && e.data.obstacleSize.sqrMagnitude > 0)
                    walls.Add(new Rect(e.position - e.data.obstacleSize / 2, e.data.obstacleSize));
            movementWalls = walls.ToArray();
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
            if (behavior == EnemyBehavior.Legacy) return false;
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
                e.vulnerable = cycle >= 1;
                e.visual.color = new Color(1,1,1,e.vulnerable ? 1 : .2f);
                if (!e.vulnerable) return true;
            }
            Vector2 direction = (target.position-e.position).normalized;
            bool move = behavior == EnemyBehavior.Chaser || behavior == EnemyBehavior.Slime ||
                behavior == EnemyBehavior.Orb || behavior == EnemyBehavior.Spider;
            if (e.data.triggerDistance > 0)
                e.triggered |= Vector2.Distance(target.position,e.position) <= e.data.triggerDistance;
            else e.triggered = true;
            if (behavior == EnemyBehavior.Tank)
            {
                // The fourth-round tank emerges from the hangar before holding the center.
                if (e.data.spawnDelay > 0 && e.position.y > 0)
                { direction = Vector2.down; move = true; }
            }
            if (move && e.triggered && !e.data.elevated)
            {
                if (behavior == EnemyBehavior.Orb)
                {
                    e.position += direction*e.data.speed*dt;
                    e.position = new Vector2(Mathf.Clamp(e.position.x,-stage.size.x/2+.3f,stage.size.x/2-.3f),
                        Mathf.Clamp(e.position.y,-stage.size.y/2+.3f,stage.size.y/2-.3f));
                }
                else
                {
                    var next = ArenaRules.Move(e.position,direction*e.data.speed*dt,.23f,stage.size,movementWalls);
                    if ((next-e.position).sqrMagnitude < .00001f)
                    {
                        var tangent = new Vector2(-direction.y,direction.x) * (e.spawnIndex%2 == 0 ? 1 : -1);
                        next = ArenaRules.Move(e.position,tangent*e.data.speed*dt,.23f,stage.size,movementWalls);
                    }
                    e.position=next;
                }
            }
            e.visual.transform.position=e.position;
            e.cooldown-=dt;
            if (!e.data.melee && e.cooldown<=0 && e.triggered)
            {
                int count = 1; float spread=0; bool radial=false;
                switch (behavior)
                {
                    case EnemyBehavior.Spread: case EnemyBehavior.Spider: count=3; spread=15; break;
                    case EnemyBehavior.Dragon: case EnemyBehavior.Sorcerer: count=5; spread=14; break;
                    case EnemyBehavior.Giant: count=7; spread=12; break;
                    case EnemyBehavior.Tank: case EnemyBehavior.Orb: count=8; radial=true; break;
                    case EnemyBehavior.Computer: count=e.vulnerable?8:3; radial=e.vulnerable; spread=20; break;
                }
                ShootFan(e,direction,count,spread,radial);
                if(e.model != null)e.model.Attack();
                e.cooldown=e.data.shotInterval;
                e.volley++;
            }
            foreach (var p in players)
                if (p.HasActive && !e.data.elevated && e.vulnerable && ShotTouchesEnemy(e,p.position,.2f) && HitPlayer(p))
                    PlayEnemyAttackSound(e);
            return true;
        }

        void ShootFan(Enemy e, Vector2 direction, int count, float spread, bool radial)
        {
            PlayEnemyAttackSound(e);
            for (int i=0;i<count;i++)
            {
                float angle = radial ? i*360f/count + (e.volley%2)*22.5f : (i-(count-1)*.5f)*spread;
                var d=(Vector2)(Quaternion.Euler(0,0,angle)*(radial?Vector2.up:direction));
                Fire(e.position,d,true,e.data.elevated,e.data.boss?3.6f:3.2f,22,1,
                    e.data.behavior==EnemyBehavior.Orb?Color.cyan:new Color(1,.35f,.15f),e);
            }
        }
    }
}
