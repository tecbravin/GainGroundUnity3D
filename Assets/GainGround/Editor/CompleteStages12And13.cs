using System;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CompleteStages12And13
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Montar estágios 1-2 e 1-3 no editor pintável")]
    public static void Build()
    {
        BuildStage(2);
        BuildStage(3);
        Debug.Log("Estágios 1-2 e 1-3 montados em Tilemaps com relevo, saída e colisões editáveis.");
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
                    Rect cellArea = new Rect(cell.x, cell.y, 1, 1);
                    var original = layout.ground.GetTile(cell);
                    if (original == null) continue;
                    bool exitArt = stage.exit.Overlaps(cellArea);
                    bool raised = Array.Exists(walls, wall => wall.Overlaps(cellArea));
                    if (exitArt) layout.effects.SetTile(cell, original);
                    else if (raised) layout.elevations.SetTile(cell, original);
                    if (exitArt || raised) layout.ground.SetTile(cell, floor);
                }

            for (int i = 0; i < walls.Length; i++)
            {
                var go = new GameObject("Blocking elevation " + (i + 1), typeof(StagePaintMarker));
                go.transform.SetParent(layout.transform, false);
                go.transform.localPosition = walls[i].center;
                var marker = go.GetComponent<StagePaintMarker>();
                marker.id = "PAINT:STAGE1" + number + "_BLOCK_" + i;
                marker.kind = StagePaintMarkerKind.Blocker;
                marker.size = walls[i].size;
                marker.blocksMovement = true;
            }
            // The old rectangles are now owned by the editable markers.
            layout.originalWalls = Array.Empty<Rect>();
            layout.lastBakedWalls = walls;
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            GainGroundStagePaintBaker.Bake(layout);
            if (number == 2) ApplyStage12NativeStructures(stage);
            if (number == 3) ApplyStage13NativeStructures(stage);
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            AssetDatabase.SaveAssets();
            Debug.Log(name + " montado: " + walls.Length + " áreas de colisão editáveis.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ApplyStage12NativeStructures(StageDefinition stage)
    {
        var objects = new List<StageObjectSpawn>(stage.objects ?? Array.Empty<StageObjectSpawn>());
        objects.RemoveAll(item => item.id != null && item.id.StartsWith("PS2:Native:", StringComparison.Ordinal));
        Add("LeftElevation", "b2", new Rect(-6, -4.5f, 4.5f, 4), new Vector3(1.4f, 1.25f, 1));
        Add("RightElevation", "b3", new Rect(1.5f, -4.5f, 4.5f, 4), new Vector3(1.4f, .9f, 1));
        Add("UpperBridge", "b1", new Rect(-2, 4, 4, 1), new Vector3(.56f, .35f, 1));
        stage.objects = objects.ToArray();
        EditorUtility.SetDirty(stage);

        void Add(string id, string suffix, Rect area, Vector3 scale)
        {
            string model = "r1s02_" + suffix;
            string path = PS2StageModelInstaller.Root + "/Stage_1_02/" + model + "/" + model + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Import PS2 stage models first: " + path);
            objects.Add(new StageObjectSpawn { id = "PS2:Native:" + id, prefab = prefab,
                area = area, modelScale = scale });
        }
    }

    static void ApplyStage13NativeStructures(StageDefinition stage)
    {
        var objects = new List<StageObjectSpawn>(stage.objects ?? Array.Empty<StageObjectSpawn>());
        objects.RemoveAll(item => item.id != null && item.id.StartsWith("PS2:Native:", StringComparison.Ordinal));
        Add("LeftCliff", "b1", new Rect(-6, -3, 2.25f, 10), new Vector3(.7f, 1, 1));
        Add("MiddleCliff", "b2", new Rect(-.5f, -3, 1, 8), new Vector3(.31f, .8f, 1));
        Add("RightCliff", "b1", new Rect(3.5f, -3, 1, 8), new Vector3(.31f, .8f, 1));
        stage.objects = objects.ToArray();
        EditorUtility.SetDirty(stage);

        void Add(string id, string suffix, Rect area, Vector3 scale)
        {
            string model = "r1s03_" + suffix;
            string path = PS2StageModelInstaller.Root + "/Stage_1_03/" + model + "/" + model + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Import PS2 stage models first: " + path);
            objects.Add(new StageObjectSpawn { id = "PS2:Native:" + id, prefab = prefab,
                area = area, modelScale = scale });
        }
    }
}
