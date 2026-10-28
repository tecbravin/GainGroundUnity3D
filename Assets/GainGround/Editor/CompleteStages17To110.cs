using System;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class CompleteStages17To110
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Montar estágios 1-7 a 1-10 no editor pintável")]
    public static void Build()
    {
        for (int number = 7; number <= 10; number++) BuildStage(number);
        Debug.Log("Estágios 1-7, 1-8, 1-9 e 1-10 montados em Tilemaps editáveis.");
    }

    static void BuildStage(int number)
    {
        string name = "Stage_1_" + number;
        string stagePath = Root + "/" + name + ".asset";
        string layoutPath = Root + "/PaintedStages/" + name + "_Layout.prefab";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null || stage.background == null)
            throw new InvalidOperationException("Mapa original ausente: " + stagePath);
        GainGroundStagePainter.RecreateStage(stagePath);

        var root = PrefabUtility.LoadPrefabContents(layoutPath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null || layout.ground == null || layout.elevations == null ||
                layout.effects == null || layout.collision == null)
                throw new InvalidOperationException("Layout incompleto: " + layoutPath);
            foreach (var marker in layout.GetComponentsInChildren<StagePaintMarker>(true))
                if (marker.id != null && marker.id.StartsWith("PAINT:STAGE1" + number + "_BLOCK_", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(marker.gameObject);

            var floor = layout.ground.GetTile(new Vector3Int(0, -6, 0));
            if (floor == null) throw new InvalidOperationException("Tile de chão ausente: " + name);
            var walls = (Rect[])(stage.walls ?? Array.Empty<Rect>()).Clone();
            int columns = Mathf.RoundToInt(stage.size.x);
            int rows = Mathf.RoundToInt(stage.size.y);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    var cell = new Vector3Int(col - columns / 2, row - rows / 2, 0);
                    var area = new Rect(cell.x, cell.y, 1, 1);
                    var original = layout.ground.GetTile(cell);
                    if (original == null) continue;
                    bool exitArt = stage.exit.Overlaps(area);
                    bool raised = Array.Exists(walls, wall => wall.Overlaps(area));
                    if (exitArt) layout.effects.SetTile(cell, original);
                    else if (raised) layout.elevations.SetTile(cell, original);
                    if (exitArt || raised) layout.ground.SetTile(cell, floor);
                }

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
                blocker.id = "PAINT:STAGE1" + number + "_BLOCK_" + separateBlockers;
                blocker.kind = StagePaintMarkerKind.Blocker;
                blocker.size = wall.size;
                blocker.blocksMovement = true;
            }

            if (number == 9) AddStoneElevationFaces(layout, walls);

            // Existing fixed collision is now controlled by markers and can be moved in the editor.
            layout.originalWalls = Array.Empty<Rect>();
            layout.lastBakedWalls = walls;
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            GainGroundStagePaintBaker.Bake(layout);
            if (number == 8) Stage18NativeBorders.Apply();
            if (number == 9) Stage19And110NativeStructures.ApplyStage19();
            if (number == 10) Stage19And110NativeStructures.ApplyStage110();
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            AssetDatabase.SaveAssets();
            Debug.Log(name + " montado: " + walls.Length + " áreas de colisão, " +
                (stage.objects == null ? 0 : stage.objects.Length) + " objetos preservados.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool SameArea(Rect a, Rect b) =>
        Mathf.Abs(a.x - b.x) < .001f && Mathf.Abs(a.y - b.y) < .001f &&
        Mathf.Abs(a.width - b.width) < .001f && Mathf.Abs(a.height - b.height) < .001f;

    static void AddStoneElevationFaces(StagePaintLayout layout, Rect[] walls)
    {
        const string artPath = Root + "/PaintedStages/Art/Stage_1_9_StoneFace.png";
        var face = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
        if (face == null) throw new InvalidOperationException("Face de pedra ausente: " + artPath);
        const float depth = .75f;
        for (int i = 0; i < walls.Length; i++)
        {
            var wall = walls[i];
            var visual = new GameObject("Elevation face " + (i + 1));
            visual.transform.SetParent(layout.transform, false);
            visual.transform.localPosition = new Vector3(wall.center.x, wall.yMin - depth * .5f, 0);
            visual.transform.localScale = new Vector3(wall.width / face.bounds.size.x,
                depth / face.bounds.size.y, 1);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = face;
            renderer.sortingOrder = -7;
        }
    }
}

