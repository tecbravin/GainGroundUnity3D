using System.Collections.Generic;
using UnityEngine;

namespace GainGround.Game
{
    // Native sprite slicing: the reference atlas remains unchanged on disk.
    // Coordinates in the stage assets are measured from the top-left of a 192x224 map.
    public sealed class OriginalStageTerrain : MonoBehaviour
    {
        readonly List<Sprite> slices = new List<Sprite>();

        public void Build(StageDefinition stage)
        {
            var cache = new Dictionary<Vector2Int, Sprite>();
            for (int y = 0; y < 224; y += 8)
            for (int x = 0; x < 192; x += 8)
            {
                var source = new Vector2Int(x, y);
                for (int p = 0; p < stage.terrainPatches.Length; p++)
                    if (stage.terrainPatches[p].Overlaps(new RectInt(x, y, 8, 8)))
                    { source = stage.patchSources[p]; break; }
                // The archers at the top of 1-2 overlap the reference's EXIT banner.
                // Slice the unobstructed identical banner from 1-1 instead.
                if (y < 16 && Mathf.Approximately(stage.exit.width, stage.size.x))
                    source = new Vector2Int(x + 166 - stage.atlasOrigin.x, y);
                if (!cache.TryGetValue(source, out var sprite))
                {
                    sprite = Sprite.Create(stage.terrainAtlas,
                        new Rect(stage.atlasOrigin.x + source.x, stage.terrainAtlas.height - stage.atlasOrigin.y - source.y - 8, 8, 8),
                        new Vector2(.5f, .5f), 16, 0, SpriteMeshType.FullRect);
                    cache.Add(source, sprite); slices.Add(sprite);
                }
                var tile = new GameObject("Terrain tile").AddComponent<SpriteRenderer>();
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = new Vector3((x + 4 - 96) / 16f, (112 - y - 4) / 16f, 0);
                tile.sprite = sprite; tile.sortingOrder = -10;
            }
        }

        void OnDestroy() { foreach (var sprite in slices) if (sprite != null) Destroy(sprite); }
    }
}
