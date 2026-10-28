using System;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

// Converts the third world to editable tilemaps while keeping the original stage data.
public static class CompleteStages31To310
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Montar estágios 3-1 a 3-10 no editor pintável")]
    public static void Build()
    {
        for (int number = 1; number <= 10; number++) BuildStage(number);
        AssetDatabase.SaveAssets();
        Debug.Log("Estágios 3-1 a 3-10 montados em Tilemaps editáveis.");
    }

    static void BuildStage(int number)
    {
        string name = "Stage_3_" + number;
        string stagePath = Root + "/" + name + ".asset";
        string layoutPath = Root + "/PaintedStages/" + name + "_Layout.prefab";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null || stage.background == null)
            throw new InvalidOperationException("Mapa original ausente: " + stagePath);

        // RecreateStage retains an existing prefab and replaces only its source-map tiles.
        GainGroundStagePainter.RecreateStage(stagePath);
        var root = PrefabUtility.LoadPrefabContents(layoutPath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null || layout.ground == null || layout.elevations == null ||
                layout.effects == null || layout.collision == null)
                throw new InvalidOperationException("Layout incompleto: " + layoutPath);

            var walls = (Rect[])(stage.walls ?? Array.Empty<Rect>()).Clone();
            var floor = layout.ground.GetTile(new Vector3Int(0, -6, 0));
            if (floor == null) throw new InvalidOperationException("Tile de chão ausente: " + name);
            int columns = Mathf.RoundToInt(stage.size.x);
            int rows = Mathf.RoundToInt(stage.size.y);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    var cell = new Vector3Int(col - columns / 2, row - rows / 2, 0);
                    var original = layout.ground.GetTile(cell);
                    if (original == null) continue;
                    var area = new Rect(cell.x, cell.y, 1, 1);
                    bool exitArt = stage.exit.Overlaps(area);
                    bool raised = Array.Exists(walls, wall => wall.Overlaps(area));
                    if (exitArt) layout.effects.SetTile(cell, original);
                    else if (raised) layout.elevations.SetTile(cell, original);
                    if (exitArt || raised) layout.ground.SetTile(cell, floor);
                }

            foreach (var marker in layout.GetComponentsInChildren<StagePaintMarker>(true))
                if (marker.id != null && marker.id.StartsWith("PAINT:STAGE3" + number + "_BLOCK_", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(marker.gameObject);

            var objectMarkers = layout.GetComponentsInChildren<StagePaintMarker>(true);
            int separateBlockers = 0;
            foreach (var wall in walls)
            {
                StagePaintMarker matchingObject = null;
                foreach (var marker in objectMarkers)
                    if (marker.kind == StagePaintMarkerKind.Object && marker.health == 0 &&
                        SameArea(marker.Area, wall)) { matchingObject = marker; break; }
                if (matchingObject != null)
                {
                    matchingObject.blocksMovement = true;
                    EditorUtility.SetDirty(matchingObject);
                    continue;
                }
                var go = new GameObject("Blocking elevation " + (++separateBlockers), typeof(StagePaintMarker));
                go.transform.SetParent(layout.transform, false);
                go.transform.localPosition = wall.center;
                var blocker = go.GetComponent<StagePaintMarker>();
                blocker.id = "PAINT:STAGE3" + number + "_BLOCK_" + separateBlockers;
                blocker.kind = StagePaintMarkerKind.Blocker;
                blocker.size = wall.size;
                blocker.blocksMovement = true;
            }

            layout.originalWalls = Array.Empty<Rect>();
            layout.lastBakedWalls = walls;
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            GainGroundStagePaintBaker.Bake(layout);
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            Debug.Log(name + ": " + walls.Length + " áreas de colisão e " +
                (stage.objects == null ? 0 : stage.objects.Length) + " objetos preservados.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool SameArea(Rect a, Rect b) =>
        Mathf.Abs(a.x - b.x) < .001f && Mathf.Abs(a.y - b.y) < .001f &&
        Mathf.Abs(a.width - b.width) < .001f && Mathf.Abs(a.height - b.height) < .001f;
}

