using UnityEngine;

namespace GainGround.Game
{
    public enum StagePaintMarkerKind { Object, Hazard, Blocker }

    public sealed class StagePaintMarker : MonoBehaviour
    {
        [HideInInspector] public string id;
        public StagePaintMarkerKind kind;
        public GameObject visualPrefab;
        public Sprite hazardSprite;
        public Vector2 size = Vector2.one;
        [Tooltip("Inclui a área deste objeto nas paredes de movimento e de projéteis ao aplicar a fase.")]
        public bool blocksMovement;
        [Min(0)] public int health;
        [Min(0)] public int teleportGroup;
        public int[] controllers = new int[0];

        public Rect Area
        {
            get
            {
                var layout = GetComponentInParent<StagePaintLayout>();
                Vector2 center = layout == null ? (Vector2)transform.localPosition :
                    (Vector2)layout.transform.InverseTransformPoint(transform.position);
                return new Rect(center - size * .5f, size);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == StagePaintMarkerKind.Hazard ? Color.magenta :
                kind == StagePaintMarkerKind.Blocker || blocksMovement ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, .01f));
        }
    }
}
