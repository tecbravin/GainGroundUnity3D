using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class GainGroundStagePaintBaker
{
    const string OutputFolder = "Assets/GainGround/Editable/OriginalCampaign/PaintedMaps";
    const string MarkerPrefix = "PAINT:";

    public static void Bake(StagePaintLayout layout)
    {
        if (layout == null || layout.stage == null) throw new InvalidOperationException("Layout sem StageDefinition.");
        if (layout.grid == null || layout.ground == null || layout.collision == null)
            throw new InvalidOperationException("O layout precisa das camadas Grid, Ground e Blocking.");
        if (layout.grid.cellSize != Vector3.one || layout.grid.cellGap != Vector3.zero ||
            layout.grid.cellLayout != GridLayout.CellLayout.Rectangle ||
            layout.transform.position != Vector3.zero ||
            layout.transform.rotation != Quaternion.identity || layout.transform.localScale != Vector3.one)
            throw new InvalidOperationException("Mantenha o Grid do layout na origem, sem rotação ou escala, com células de 1 unidade.");
        foreach (var tilemap in new[] { layout.ground, layout.elevations, layout.decorations,
                     layout.effects, layout.collision })
            if (tilemap != null && (tilemap.transform.localPosition != Vector3.zero ||
                tilemap.transform.localRotation != Quaternion.identity ||
                tilemap.transform.localScale != Vector3.one))
                throw new InvalidOperationException("As camadas Tilemap devem permanecer alinhadas ao Grid: " + tilemap.name);
        var stage = layout.stage;
        var markers = layout.GetComponentsInChildren<StagePaintMarker>(true);
        ValidateMarkers(stage, markers);
        var newWalls = BuildWalls(layout.collision, stage.size);
        foreach (var marker in markers)
            if (marker.kind == StagePaintMarkerKind.Blocker ||
                marker.kind == StagePaintMarkerKind.Object && marker.blocksMovement)
                newWalls.Add(marker.Area);
        int width = 0, height = 0;
        Sprite sprite = stage.background;
        if (!Application.isBatchMode)
        {
            byte[] image = RenderMap(layout, out width, out height);
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/GainGround/Editable/OriginalCampaign", "PaintedMaps");
            string path = OutputFolder + "/" + stage.name + ".png";
            File.WriteAllBytes(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), path), image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureMapImporter(path, width, height, stage.size.x);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Falha ao importar o mapa pintado: " + path);
        }

        Undo.RecordObject(stage, "Apply painted stage");
        stage.background = sprite;
        var sourceLayout = PrefabUtility.GetCorrespondingObjectFromSource(layout);
        if (sourceLayout == null)
        {
            string layoutPath = "Assets/GainGround/Editable/OriginalCampaign/PaintedStages/" +
                stage.name + "_Layout.prefab";
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath);
            sourceLayout = savedPrefab != null ? savedPrefab.GetComponent<StagePaintLayout>() : null;
        }
        if (sourceLayout == null)
            throw new InvalidOperationException("Salve o prefab do layout antes de aplicar a fase.");
        stage.paintedLayout = sourceLayout;
        var baseWalls = new List<Rect>(stage.walls ?? Array.Empty<Rect>());
        foreach (var oldPainted in layout.lastBakedWalls ?? Array.Empty<Rect>())
        {
            int index = baseWalls.FindIndex(rect => rect.Equals(oldPainted));
            if (index >= 0) baseWalls.RemoveAt(index);
        }
        stage.walls = baseWalls.Concat(newWalls).ToArray();
        Undo.RecordObject(layout, "Remember painted walls");
        layout.originalWalls = baseWalls.ToArray();
        layout.lastBakedWalls = newWalls.ToArray();
        EditorUtility.SetDirty(layout);
        var managedIds = new HashSet<string>(layout.managedObjectIds ?? Array.Empty<string>());
        stage.objects = (stage.objects ?? Array.Empty<StageObjectSpawn>())
            .Where(item => item != null && (item.id == null ||
                (!item.id.StartsWith(MarkerPrefix, StringComparison.Ordinal) && !managedIds.Contains(item.id))))
            .Concat(markers.Where(item => item.kind == StagePaintMarkerKind.Object)
                .Select(item => new StageObjectSpawn {
                    id = item.id, prefab = item.visualPrefab, area = item.Area,
                    health = item.health, teleportGroup = item.teleportGroup }))
            .ToArray();
        layout.managedObjectIds = markers.Where(item => item.kind == StagePaintMarkerKind.Object)
            .Select(item => item.id).ToArray();
        stage.hazards = (stage.hazards ?? Array.Empty<HazardSpawn>())
            .Where(item => item != null && (item.editorId == null || !item.editorId.StartsWith(MarkerPrefix, StringComparison.Ordinal)))
            .Concat(markers.Where(item => item.kind == StagePaintMarkerKind.Hazard)
                .Select(item => new HazardSpawn {
                    editorId = item.id, prefab = item.visualPrefab,
                    sprite = item.hazardSprite, area = item.Area,
                    controllers = item.controllers ?? Array.Empty<int>() }))
            .ToArray();
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
        Debug.Log($"Estágio {stage.name} aplicado: {newWalls.Count} áreas pintadas, {markers.Length} marcadores" +
                  (width > 0 ? $", mapa {width}×{height}." : " (prévia preservada em batch mode)."));
    }

    static void ValidateMarkers(StageDefinition stage, StagePaintMarker[] markers)
    {
        var ids = new HashSet<string>();
        foreach (var marker in markers)
        {
            if (string.IsNullOrEmpty(marker.id) || !ids.Add(marker.id))
            {
                Undo.RecordObject(marker, "Assign stage marker ID");
                marker.id = MarkerPrefix + Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(marker);
                ids.Add(marker.id);
            }
            if (marker.size.x <= 0 || marker.size.y <= 0)
                throw new InvalidOperationException("Marcador com tamanho inválido: " + marker.name);
            if (marker.Area.xMin < -stage.size.x * .5f || marker.Area.xMax > stage.size.x * .5f ||
                marker.Area.yMin < -stage.size.y * .5f || marker.Area.yMax > stage.size.y * .5f)
                throw new InvalidOperationException("Marcador fora da área do estágio: " + marker.name);
            if (marker.kind == StagePaintMarkerKind.Object && marker.visualPrefab == null)
                throw new InvalidOperationException("Objeto sem prefab: " + marker.name);
            if (marker.kind == StagePaintMarkerKind.Hazard && marker.visualPrefab == null && marker.hazardSprite == null)
                throw new InvalidOperationException("Perigo sem prefab ou sprite: " + marker.name);
        }
    }

    // Consecutive painted cells become wide rectangles instead of one collision per tile.
    static List<Rect> BuildWalls(Tilemap map, Vector2 size)
    {
        var result = new List<Rect>();
        var previous = new Dictionary<Vector2Int, Rect>();
        int minX = Mathf.CeilToInt(-size.x * .5f);
        int maxX = Mathf.FloorToInt(size.x * .5f);
        int minY = Mathf.CeilToInt(-size.y * .5f);
        int maxY = Mathf.FloorToInt(size.y * .5f);
        for (int y = minY; y < maxY; y++)
        {
            var current = new Dictionary<Vector2Int, Rect>();
            for (int x = minX; x < maxX;)
            {
                if (!map.HasTile(new Vector3Int(x, y, 0))) { x++; continue; }
                int start = x++;
                while (x < maxX && map.HasTile(new Vector3Int(x, y, 0))) x++;
                var key = new Vector2Int(start, x - start);
                if (previous.TryGetValue(key, out var old))
                { old.height += 1; current.Add(key, old); }
                else current.Add(key, new Rect(start, y, x - start, 1));
            }
            foreach (var entry in previous)
                if (!current.ContainsKey(entry.Key)) result.Add(entry.Value);
            previous = current;
        }
        result.AddRange(previous.Values);
        return result;
    }

    static byte[] RenderMap(StagePaintLayout layout, out int width, out int height)
    {
        var stage = layout.stage;
        width = layout.sourceBackground != null ? Mathf.RoundToInt(layout.sourceBackground.rect.width) :
            Mathf.RoundToInt(stage.size.x * 64);
        height = layout.sourceBackground != null ? Mathf.RoundToInt(layout.sourceBackground.rect.height) :
            Mathf.RoundToInt(stage.size.y * 64);
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new InvalidOperationException("Dimensões de mapa inválidas para a pintura.");
        var root = new GameObject("Temporary stage paint render", typeof(Grid));
        var cameraObject = new GameObject("Temporary stage paint camera", typeof(Camera));
        RenderTexture target = null;
        Texture2D output = null;
        var previous = RenderTexture.active;
        try
        {
            root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<Grid>().cellSize = layout.grid.cellSize;
            root.GetComponent<Grid>().cellGap = layout.grid.cellGap;
            if (layout.sourceBackground != null)
            {
                var baseMap = new GameObject("Original map", typeof(SpriteRenderer));
                baseMap.transform.SetParent(root.transform, false);
                var renderer = baseMap.GetComponent<SpriteRenderer>();
                renderer.sprite = layout.sourceBackground;
                renderer.sortingOrder = -10;
                baseMap.transform.localScale = new Vector3(
                    stage.size.x / layout.sourceBackground.bounds.size.x,
                    stage.size.y / layout.sourceBackground.bounds.size.y, 1);
            }
            foreach (var tilemap in new[] { layout.ground, layout.elevations, layout.decorations, layout.effects })
                if (tilemap != null) UnityEngine.Object.Instantiate(tilemap.gameObject, root.transform).SetActive(true);
            foreach (var marker in layout.GetComponentsInChildren<StagePaintMarker>(true))
            {
                if (marker.visualPrefab == null) continue;
                var instance = UnityEngine.Object.Instantiate(marker.visualPrefab, root.transform);
                instance.transform.position = marker.Area.center;
                var visual = instance.GetComponent<SpriteRenderer>();
                if (visual != null && visual.sprite != null)
                {
                    if (visual.drawMode == SpriteDrawMode.Tiled) visual.size = marker.Area.size;
                    else instance.transform.localScale = new Vector3(
                        marker.Area.width / visual.sprite.bounds.size.x,
                        marker.Area.height / visual.sprite.bounds.size.y, 1);
                }
            }
            SetLayer(root, 31);
            target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var camera = cameraObject.GetComponent<Camera>();
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(stage.size.y * .5f,
                stage.size.x * height / (2f * width));
            camera.aspect = (float)width / height;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = stage.floorColor;
            camera.cullingMask = 1 << 31;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            output.Apply(false);
            return output.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (output != null) UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void SetLayer(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform) SetLayer(child.gameObject, layer);
    }

    static void ConfigureMapImporter(string path, int width, int height, float worldWidth)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Importador do mapa não encontrado: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = width / worldWidth;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
        importer.SaveAndReimport();
    }
}
