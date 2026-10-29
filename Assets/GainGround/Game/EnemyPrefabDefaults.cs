using UnityEngine;

namespace GainGround.Game
{
    public sealed class EnemyPrefabDefaults : MonoBehaviour
    {
        [Min(0), Tooltip("Zero keeps the health set on the stage spawn.")]
        public int health;
        [Min(0), Tooltip("Zero keeps the speed set on the stage spawn.")]
        public float speed;
        public EnemyWalkingPattern walkingPattern;
        [Min(0), Tooltip("Zero uses the standard patrol distance.")]
        public float walkingDistance;
        [Min(0), Tooltip("For Patrol: activation range; returns to spawn beyond twice this range. Zero uses 4.")]
        public float patrolDistance = 4f;
        [Tooltip("Enemy model spawned twice when this enemy takes lethal damage. Leave empty for no split.")]
        public PS2EnemyVisual splitOnDeathPrefab;
        [Min(0), Tooltip("Horizontal distance from the defeated enemy to each child.")]
        public float splitOffset = .5f;
    }
}
