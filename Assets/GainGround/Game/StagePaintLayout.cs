using UnityEngine;
using UnityEngine.Tilemaps;

namespace GainGround.Game
{
    // Authoring data lives on a prefab. The game uses the baked map and stage asset.
    public sealed class StagePaintLayout : MonoBehaviour
    {
        public StageDefinition stage;
        public Sprite sourceBackground;
        public Rect[] originalWalls = new Rect[0];
        [HideInInspector] public Rect[] lastBakedWalls = new Rect[0];
        [HideInInspector] public string[] managedObjectIds = new string[0];
        public Grid grid;
        public Tilemap ground;
        public Tilemap elevations;
        public Tilemap decorations;
        public Tilemap effects;
        public Tilemap collision;
    }
}
