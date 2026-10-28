using System;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class CompleteStages14To16
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Montar estágios 1-4 a 1-6 no editor pintável")]
    public static void Build()
    {
        for (int number = 4; number <= 6; number++) BuildStage(number);
        Debug.Log("Estágios 1-4, 1-5 e 1-6 montados em Tilemaps editáveis.");
    }

    static void BuildStage(int number)
    {
        string name = "Stage_1_" + number;
        string stagePath = Root + "/" + name + ".asset";
        string layoutPath = Root + "/PaintedStages/" + name + "_Layout.prefab";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null || stage.background == null)
            throw new InvalidOperationException("Mapa original ausente: " + stagePath);
        if (number == 4) RefreshStage14HillPreview();
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

            if (number == 6) layout.elevations.ClearAllTiles();
            if (number == 4) AddGrassyHills(layout, walls);

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

            // Existing fixed collision is now controlled by markers and can be moved in the editor.
            layout.originalWalls = Array.Empty<Rect>();
            layout.lastBakedWalls = walls;
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            GainGroundStagePaintBaker.Bake(layout);
            if (number == 4) Stage14NativeHills.Apply();
            if (number == 5) Stage15NativeBorders.Apply();
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

    [InitializeOnLoadMethod]
    static void EnsureStage14HillPreview()
    {
        EditorApplication.delayCall += () =>
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_4.asset");
            if (stage == null || AssetDatabase.GetAssetPath(stage.background) ==
                Root + "/Complete/Maps/1-04-Hills.png") return;
            try { RefreshStage14HillPreview(); }
            catch (Exception ex) { Debug.LogException(ex); }
        };
    }

    [MenuItem("Gain Ground/Atualizar prévia das colinas da fase 1-4")]
    public static void RefreshStage14HillPreview()
    {
        const string sourcePath = Root + "/Complete/Maps/1-04.png";
        const string hillPath = Root + "/PaintedStages/Art/Stage_1_4_GrassyHill.png";
        const string outputPath = Root + "/Complete/Maps/1-04-Hills.png";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_4.asset");
        if (stage == null || stage.walls == null || stage.walls.Length != 2)
            throw new InvalidOperationException("Geometria das colinas da fase 1-4 ausente.");
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var hill = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D result = null;
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(Path.Combine(projectRoot, sourcePath))) ||
                !ImageConversion.LoadImage(hill, File.ReadAllBytes(Path.Combine(projectRoot, hillPath))))
                throw new InvalidOperationException("Texturas da fase 1-4 ausentes.");
            int ppu = source.width / Mathf.RoundToInt(stage.size.x);
            if (source.width != 12 * ppu || source.height != 14 * ppu)
                throw new InvalidOperationException("A prévia original da fase 1-4 não corresponde à grade 12x14.");
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var pixels = source.GetPixels();
            foreach (var wall in stage.walls)
            {
                int left = Mathf.RoundToInt((wall.xMin + stage.size.x * .5f) * ppu);
                int bottom = Mathf.RoundToInt((wall.yMin + stage.size.y * .5f) * ppu);
                int width = Mathf.RoundToInt(wall.width * ppu);
                int height = Mathf.RoundToInt(wall.height * ppu);
                // Remove the old square platform before drawing the organic hill.
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        pixels[(bottom + y) * source.width + left + x] =
                            source.GetPixel(left + x, bottom + y - 6 * ppu);
                int slopeBottom = bottom - ppu / 2;
                int slopeHeight = height + ppu / 2;
                for (int y = 0; y < slopeHeight; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var color = hill.GetPixelBilinear((x + .5f) / width, (y + .5f) / slopeHeight);
                        int index = (slopeBottom + y) * source.width + left + x;
                        pixels[index] = Color.Lerp(pixels[index], color, color.a);
                    }
            }
            result.SetPixels(pixels);
            result.Apply(false);
            File.WriteAllBytes(Path.Combine(projectRoot, outputPath), result.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Importador da prévia 1-4 ausente.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            stage.background = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (stage.background == null) throw new InvalidOperationException("Prévia 1-4 não foi importada como sprite.");
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(hill);
            if (result != null) UnityEngine.Object.DestroyImmediate(result);
        }
    }

    static void AddGrassyHills(StagePaintLayout layout, Rect[] walls)
    {
        const string art = Root + "/PaintedStages/Art/Stage_1_4_GrassyHill.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art);
        if (sprite == null) throw new InvalidOperationException("Textura da colina ausente: " + art);
        layout.elevations.ClearAllTiles();
        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name.StartsWith("Grassy hill ", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        for (int i = 0; i < walls.Length; i++)
        {
            var wall = walls[i];
            var hill = new GameObject("Grassy hill " + (i + 1));
            hill.transform.SetParent(layout.transform, false);
            // The visible lower half-unit is a slope; the original blocker remains intact.
            hill.transform.localPosition = new Vector3(wall.center.x, wall.center.y - .25f, 0);
            hill.transform.localScale = new Vector3(wall.width / sprite.bounds.size.x,
                (wall.height + .5f) / sprite.bounds.size.y, 1);
            var renderer = hill.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -7;
        }
    }
}
