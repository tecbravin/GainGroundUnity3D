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

    [MenuItem("Gain Ground/Recriar estágio 3-3 da ISO")]
    public static void BuildStage33()
    {
        BuildStage(3);
        AssetDatabase.SaveAssets();
        Debug.Log("Estágio 3-3 reconstruído com o mapa e as elevações da ISO.");
    }

    [MenuItem("Gain Ground/Recriar elevações 3-4 e 3-5 da ISO")]
    public static void BuildStages34And35()
    {
        RebuildElevationVisuals(4);
        RebuildElevationVisuals(5);
        AssetDatabase.SaveAssets();
        Debug.Log("Elevações 3-4 e 3-5 reconstruídas com frentes texturizadas da ISO.");
    }

    static void RebuildElevationVisuals(int number)
    {
        string name = "Stage_3_" + number;
        string stagePath = Root + "/" + name + ".asset";
        string layoutPath = Root + "/PaintedStages/" + name + "_Layout.prefab";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null || stage.walls == null || stage.walls.Length != 2)
            throw new InvalidOperationException("As duas elevações da fase " + name + " não foram encontradas.");
        var root = PrefabUtility.LoadPrefabContents(layoutPath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null || layout.elevations == null)
                throw new InvalidOperationException("Layout incompleto: " + layoutPath);
            AddElevationFrontFaces(layout, stage.walls, number);
            layout.lastBakedWalls = (Rect[])stage.walls.Clone();
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            GainGroundStagePaintBaker.Bake(layout);
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
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
            // The 3-3 sample at (0, -6) is inside the brown trench floor. Using it
            // beneath elevations paints brown squares through the raised ground.
            var floorCell = number == 3 ? new Vector3Int(0, 0, 0) : new Vector3Int(0, -6, 0);
            var floor = layout.ground.GetTile(floorCell);
            if (floor == null) throw new InvalidOperationException("Tile de chão ausente em " + floorCell + ": " + name);
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

            if (number == 4 || number == 5) AddElevationFrontFaces(layout, walls, number);

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

    static void AddElevationFrontFaces(StagePaintLayout layout, Rect[] walls, int number)
    {
        const float pixelsPerUnit = 128f;
        const float frontEdgeOffsetPixels = 10f;
        string prefix = "ISO elevation front face ";
        string legacyPrefix = "Stage " + number + " elevation face ";
        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name.StartsWith(prefix, StringComparison.Ordinal) ||
                child.name.StartsWith(legacyPrefix, StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        if (walls.Length != 2)
            throw new InvalidOperationException("Esperadas duas elevações na fase 3-" + number + ".");

        for (int i = 0; i < walls.Length; i++)
        {
            string path = Root + "/PaintedStages/Art/Stage_3_" + number +
                "_ElevationFace_" + (i + 1) + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Face ISO ausente: " + path);
            var wall = walls[i];
            var face = new GameObject(prefix + "3-" + number + "-" + (i + 1));
            face.transform.SetParent(layout.transform, false);
            face.transform.localPosition = new Vector3(wall.center.x,
                wall.yMin + (frontEdgeOffsetPixels - sprite.rect.height * .5f) / pixelsPerUnit, 0);
            face.transform.localScale = new Vector3(wall.width / sprite.bounds.size.x,
                1, 1);
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -7;
        }
    }

    static bool SameArea(Rect a, Rect b) =>
        Mathf.Abs(a.x - b.x) < .001f && Mathf.Abs(a.y - b.y) < .001f &&
        Mathf.Abs(a.width - b.width) < .001f && Mathf.Abs(a.height - b.height) < .001f;
}

