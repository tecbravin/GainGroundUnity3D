using System;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

// Converts the second world to editable tilemaps while keeping the original stage data.
public static class CompleteStages21To210
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Montar estágios 2-1 a 2-10 no editor pintável")]
    public static void Build()
    {
        for (int number = 1; number <= 10; number++) BuildStage(number, false);
        AssetDatabase.SaveAssets();
        Debug.Log("Layouts ausentes do segundo mundo criados; layouts já editados preservados.");
    }

    [MenuItem("Gain Ground/Recriar estágios 2-1 a 2-10 dos mapas de origem")]
    public static void RebuildFromSource()
    {
        for (int number = 1; number <= 10; number++) BuildStage(number, true);
        AssetDatabase.SaveAssets();
    }

    static void BuildStage(int number, bool recreate)
    {
        string name = "Stage_2_" + number;
        string stagePath = Root + "/" + name + ".asset";
        string layoutPath = Root + "/PaintedStages/" + name + "_Layout.prefab";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null || stage.background == null)
            throw new InvalidOperationException("Mapa original ausente: " + stagePath);
        if (!recreate && AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath) != null)
            return;

        if (number == 2) RefreshStage22CastleTexture();
        if (number == 7) RestoreStage27ISOTexture();

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
                if (marker.id != null && marker.id.StartsWith("PAINT:STAGE2" + number + "_BLOCK_", StringComparison.Ordinal))
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
                blocker.id = "PAINT:STAGE2" + number + "_BLOCK_" + separateBlockers;
                blocker.kind = StagePaintMarkerKind.Blocker;
                blocker.size = wall.size;
                blocker.blocksMovement = true;
            }

            if (number == 2) AddStoneElevationFaces(layout, walls);
            if (number == 3) RemoveBlueRoofOverlays(layout);
            if (number == 4) AddSanctuaryWallFaces(layout, walls);
            if (number == 5) AddOpenStoneGate(layout, walls);
            if (number == 7) AddCorridorElevationFaces(layout, walls);

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

    [InitializeOnLoadMethod]
    static void EnsureCastleStageTextures()
    {
        EditorApplication.delayCall += () =>
        {
            foreach (int number in new[] { 2 })
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_2_" + number + ".asset");
                string preview = Root + "/Complete/Maps/2-" + number.ToString("D2") + "-Castle.png";
                if (stage == null || AssetDatabase.GetAssetPath(stage.background) == preview) continue;
                try { RefreshCastleTexture(number); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        };
    }

    [MenuItem("Gain Ground/Atualizar textura de castelo da fase 2-2")]
    public static void RefreshStage22CastleTexture() => RefreshCastleTexture(2);

    [MenuItem("Gain Ground/Restaurar texturas da ISO na fase 2-7")]
    public static void RestoreStage27ISOTexture()
    {
        const string map = Root + "/Complete/Maps/2-07.png";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_2_7.asset");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(map);
        if (stage == null || sprite == null)
            throw new InvalidOperationException("Textura original da fase 2-7 ausente: " + map);
        if (stage.background == sprite) return;
        stage.background = sprite;
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
    }

    static void RefreshCastleTexture(int number)
    {
        string sourcePath = Root + "/Complete/Maps/2-" + number.ToString("D2") + ".png";
        string outputPath = Root + "/Complete/Maps/2-" + number.ToString("D2") + "-Castle.png";
        string wallPath = Root + "/PaintedStages/Art/Stage_2_2_" +
            (number == 2 ? "CastleBlock.png" : "CastleWall.png");
        string stagePath = Root + "/Stage_2_" + number + ".asset";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null || stage.walls == null || stage.walls.Length != (number == 2 ? 4 : 11))
            throw new InvalidOperationException("Geometria da fase 2-" + number + " ausente.");
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var wall = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D result = null;
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(Path.Combine(projectRoot, sourcePath))) ||
                !ImageConversion.LoadImage(wall, File.ReadAllBytes(Path.Combine(projectRoot, wallPath))))
                throw new InvalidOperationException("Não foi possível ler as texturas da fase 2-" + number + ".");
            int pixelsPerUnit = source.width / Mathf.RoundToInt(stage.size.x);
            if (source.width != 12 * pixelsPerUnit || source.height != 14 * pixelsPerUnit)
                throw new InvalidOperationException("A textura original da fase 2-" + number + " não corresponde à grade 12x14.");
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var pixels = source.GetPixels();
            foreach (var area in stage.walls)
            {
                int left = Mathf.RoundToInt((area.xMin + stage.size.x * .5f) * pixelsPerUnit);
                int bottom = Mathf.RoundToInt((area.yMin + stage.size.y * .5f) * pixelsPerUnit);
                int width = Mathf.RoundToInt(area.width * pixelsPerUnit);
                int height = Mathf.RoundToInt(area.height * pixelsPerUnit);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var color = wall.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
                        int index = (bottom + y) * source.width + left + x;
                        pixels[index] = Color.Lerp(pixels[index], color, color.a);
                    }
            }
            result.SetPixels(pixels);
            result.Apply(false);
            File.WriteAllBytes(Path.Combine(projectRoot, outputPath), result.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Importador da prévia 2-" + number + " ausente.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            stage.background = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (stage.background == null) throw new InvalidOperationException("Prévia 2-" + number + " não foi importada como sprite.");
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(wall);
            if (result != null) UnityEngine.Object.DestroyImmediate(result);
        }
    }

    static void AddStoneElevationFaces(StagePaintLayout layout, Rect[] walls)
    {
        const string art = Root + "/PaintedStages/Art/Stage_2_2_CastleWall.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art);
        const string topArt = Root + "/PaintedStages/Art/Stage_2_2_CastleBlock.png";
        var topSprite = AssetDatabase.LoadAssetAtPath<Sprite>(topArt);
        if (sprite == null || topSprite == null)
            throw new InvalidOperationException("Texturas da muralha ausentes: " + art + " / " + topArt);
        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name.StartsWith("Castle elevation face ", StringComparison.Ordinal) ||
                child.name.StartsWith("Castle wall top ", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        for (int i = 0; i < walls.Length; i++)
        {
            const float depth = .875f;
            var wall = walls[i];
            for (int y = Mathf.FloorToInt(wall.yMin); y < Mathf.CeilToInt(wall.yMax); y++)
                for (int x = Mathf.FloorToInt(wall.xMin); x < Mathf.CeilToInt(wall.xMax); x++)
                    layout.elevations.SetTile(new Vector3Int(x, y, 0), null);
            var top = new GameObject("Castle wall top " + (i + 1));
            top.transform.SetParent(layout.transform, false);
            top.transform.localPosition = wall.center;
            top.transform.localScale = new Vector3(wall.width / topSprite.bounds.size.x,
                wall.height / topSprite.bounds.size.y, 1);
            var topRenderer = top.AddComponent<SpriteRenderer>();
            topRenderer.sprite = topSprite;
            topRenderer.sortingOrder = -7;
            var face = new GameObject("Castle elevation face " + (i + 1));
            face.transform.SetParent(layout.transform, false);
            face.transform.localPosition = new Vector3(wall.center.x, wall.yMin - depth * .5f, 0);
            face.transform.localScale = new Vector3(wall.width / sprite.bounds.size.x,
                depth / sprite.bounds.size.y, 1);
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -7;
        }
    }

    static void RemoveBlueRoofOverlays(StagePaintLayout layout)
    {
        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name.StartsWith("Blue roof ", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    static void AddSanctuaryWallFaces(StagePaintLayout layout, Rect[] walls)
    {
        const string art = Root + "/PaintedStages/Art/Stage_2_2_StoneFace.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art);
        if (sprite == null) throw new InvalidOperationException("Face de pedra ausente: " + art);
        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name.StartsWith("Sanctuary wall face ", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        for (int i = 0; i < walls.Length; i++)
        {
            var wall = walls[i];
            float depth = wall.width > 1.5f ? 1f : .72f;
            var face = new GameObject("Sanctuary wall face " + (i + 1));
            face.transform.SetParent(layout.transform, false);
            face.transform.localPosition = new Vector3(wall.center.x, wall.yMin - depth * .5f, 0);
            face.transform.localScale = new Vector3(wall.width / sprite.bounds.size.x,
                depth / sprite.bounds.size.y, 1);
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -7;
            renderer.color = new Color(.76f, .75f, .68f);
        }
    }

    static void AddCorridorElevationFaces(StagePaintLayout layout, Rect[] walls)
    {
        layout.elevations.ClearAllTiles();
        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name.StartsWith("Corridor elevation face ", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        for (int i = 0; i < walls.Length; i++)
        {
            string art = Root + "/PaintedStages/Art/Stage_2_7_ISOWall_" + (i + 1).ToString("D2") + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art);
            if (sprite == null) throw new InvalidOperationException("Textura original da muralha ausente: " + art);
            var wall = walls[i];
            var face = new GameObject("Corridor elevation face " + (i + 1));
            face.transform.SetParent(layout.transform, false);
            face.transform.localPosition = wall.center;
            face.transform.localScale = new Vector3(wall.width / sprite.bounds.size.x,
                wall.height / sprite.bounds.size.y, 1);
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -7;
        }
    }

    static void AddOpenStoneGate(StagePaintLayout layout, Rect[] walls)
    {
        const string artRoot = Root + "/PaintedStages/Art/";
        var grass = AssetDatabase.LoadAssetAtPath<Sprite>(artRoot + "Stage_2_5_ContinuousGrass.png");
        var gate = AssetDatabase.LoadAssetAtPath<Sprite>(artRoot + "Stage_2_5_OpenStoneGate.png");
        var pillar = AssetDatabase.LoadAssetAtPath<Sprite>(artRoot + "Stage_2_5_StonePillar.png");
        var block = AssetDatabase.LoadAssetAtPath<Sprite>(artRoot + "Stage_2_5_StoneBlock.png");
        var moat = AssetDatabase.LoadAssetAtPath<Sprite>(artRoot + "Stage_2_5_Moat.png");
        var bridge = AssetDatabase.LoadAssetAtPath<Sprite>(artRoot + "Stage_2_5_Bridge.png");
        if (grass == null || gate == null || pillar == null || block == null || moat == null || bridge == null)
            throw new InvalidOperationException("Texturas da fase 2-5 ausentes.");

        // The original map alternates grass and paving in a checkerboard. Keep the
        // smaller obstacle collisions, but replace all source tiles: they include
        // patches of the old paving beneath the stone objects.
        layout.ground.ClearAllTiles();
        layout.elevations.ClearAllTiles();

        for (int i = layout.transform.childCount - 1; i >= 0; i--)
        {
            var child = layout.transform.GetChild(i);
            if (child.name == "Continuous grass" || child.name == "Open stone gate" ||
                child.name == "Castle moat" || child.name == "Stone bridge" ||
                child.name.StartsWith("Stone obstacle ", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        AddStageSprite(layout, "Continuous grass", grass, Vector2.zero, new Vector2(12, 14), -10);
        AddStageSprite(layout, "Castle moat", moat, new Vector2(0, .25f), new Vector2(12, 2.5f), -8);
        AddStageSprite(layout, "Stone bridge", bridge, new Vector2(0, .25f), new Vector2(2, 2.7f), -7);
        AddStageSprite(layout, "Open stone gate", gate, new Vector2(0, 3), new Vector2(12, 4), -7);
        for (int i = 2; i < Mathf.Min(walls.Length, 6); i++)
        {
            bool narrow = i < 4;
            // The transparent source images have padding; these sizes and offsets
            // align their visible stone edges with the existing collision rectangles.
            var size = narrow ? new Vector2(1.394654f, 1.939504f) :
                new Vector2(1.914504f, 1.874439f);
            var offset = narrow ? new Vector2(-.028302f, .034985f) :
                new Vector2(-.00229f, .000747f);
            AddStageSprite(layout, "Stone obstacle " + (i - 1), narrow ? pillar : block,
                walls[i].center + offset, size, -7);
        }
    }

    static void AddStageSprite(StagePaintLayout layout, string name, Sprite sprite,
        Vector2 center, Vector2 size, int sortingOrder)
    {
        var part = new GameObject(name);
        part.transform.SetParent(layout.transform, false);
        part.transform.localPosition = center;
        part.transform.localScale = new Vector3(size.x / sprite.bounds.size.x,
            size.y / sprite.bounds.size.y, 1);
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }
}
