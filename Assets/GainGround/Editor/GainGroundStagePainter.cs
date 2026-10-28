using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GainGround.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public sealed class GainGroundStagePainter : EditorWindow
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign/PaintedStages";
    const string Palettes = "Assets/GainGround/Editor/StageTexturePalettes";
    static readonly string[] LayerNames = { "Chão", "Elevações", "Decoração", "Efeitos", "Bloqueio" };

    StageDefinition stage;
    TileBase selectedTile;
    GameObject objectPrefab;
    Sprite hazardSprite;
    int layer;
    bool erase;
    bool paintEnabled;
    bool editStagePoints;
    bool showStageData;
    Vector2 tileScroll;
    Editor stageEditor;
    Vector3Int lastPainted = new Vector3Int(int.MinValue, 0, 0);

    [InitializeOnLoadMethod]
    static void CreateFirstStageLayoutWhenEditorOpens()
    {
        EditorApplication.delayCall += () =>
        {
            const string path = Root + "/Stage_1_1_Layout.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            try { RecreateFirstStage(); }
            catch (Exception ex) { Debug.LogException(ex); }
        };
    }

    [MenuItem("Gain Ground/Editor de estágios")]
    public static void Open() => GetWindow<GainGroundStagePainter>("Editor de estágios");

    public static void OpenFor(StageDefinition selected)
    {
        var window = GetWindow<GainGroundStagePainter>("Editor de estágios");
        if (window.stage != selected) window.selectedTile = null;
        window.stage = selected;
        EditorPrefs.SetString("GainGround.StagePainter.Stage",
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selected)));
        window.Show();
        window.Focus();
    }

    void OnEnable()
    {
        stage = Selection.activeObject as StageDefinition;
        if (stage == null)
        {
            string guid = EditorPrefs.GetString("GainGround.StagePainter.Stage", "");
            stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(AssetDatabase.GUIDToAssetPath(guid));
        }
        SceneView.duringSceneGui += PaintScene;
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= PaintScene;
        if (stageEditor != null) DestroyImmediate(stageEditor);
    }

    void OnGUI()
    {
        var next = (StageDefinition)EditorGUILayout.ObjectField("Estágio", stage, typeof(StageDefinition), false);
        if (next != stage)
        {
            stage = next; selectedTile = null; paintEnabled = false;
            EditorPrefs.SetString("GainGround.StagePainter.Stage",
                stage == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(stage)));
        }
        if (stage == null)
        {
            EditorGUILayout.HelpBox("Selecione um StageDefinition existente para criar ou abrir seu layout pintável.", MessageType.Info);
            return;
        }

        string path = LayoutPath(stage);
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(exists ? "Abrir layout" : "Criar layout", GUILayout.Height(30))) OpenLayout();
            if (exists && GUILayout.Button("Selecionar prefab", GUILayout.Height(30)))
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        var layout = CurrentLayout();
        if (layout == null)
        {
            EditorGUILayout.HelpBox("Abra o prefab do layout. A pintura acontece na Scene View em Prefab Mode; as outras cenas permanecem intactas.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("Pintura", EditorStyles.boldLabel);
        layer = GUILayout.Toolbar(layer, LayerNames);
        using (new EditorGUILayout.HorizontalScope())
        {
            paintEnabled = GUILayout.Toggle(paintEnabled, "Pintar na Scene View", "Button");
            erase = GUILayout.Toggle(erase, "Apagar", "Button");
        }
        if (GUILayout.Toggle(editStagePoints, "Mover início, saída, resgates e inimigos", "Button") != editStagePoints)
        {
            editStagePoints = !editStagePoints;
            if (editStagePoints) paintEnabled = false;
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox("Clique ou arraste com o botão esquerdo. Alt permite navegar. Bloqueio define colisão; ele não aparece no mapa final.", MessageType.None);
        if (GUILayout.Button("Preparar tiles dos atlas da fase"))
        {
            try { ImportTiles(); }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("Falha ao preparar tiles", ex.Message, "OK"); }
        }
        DrawTiles();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Objetos e efeitos", EditorStyles.boldLabel);
        objectPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", objectPrefab, typeof(GameObject), false);
        hazardSprite = (Sprite)EditorGUILayout.ObjectField("Sprite do perigo", hazardSprite, typeof(Sprite), false);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Adicionar objeto")) AddMarker(layout, StagePaintMarkerKind.Object);
            if (GUILayout.Button("Adicionar perigo")) AddMarker(layout, StagePaintMarkerKind.Hazard);
            if (GUILayout.Button("Adicionar colisão")) AddMarker(layout, StagePaintMarkerKind.Blocker);
        }
        EditorGUILayout.HelpBox("Mova o marcador na Scene View. No Inspector, Blocks Movement cria colisão de jogo para o objeto; ajuste também Size, Health, Teleport Group ou Controllers. A camada Efeitos é visual; use um marcador de perigo para lógica de dano.", MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("Aplicar pintura, colisões e objetos à fase", GUILayout.Height(36)))
        {
            try { GainGroundStagePaintBaker.Bake(layout); }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("Falha ao aplicar fase", ex.Message, "OK"); }
        }
        showStageData = EditorGUILayout.Foldout(showStageData, "Outros dados do estágio", true);
        if (showStageData)
        {
            Editor.CreateCachedEditor(stage, null, ref stageEditor);
            stageEditor.OnInspectorGUI();
        }
    }

    static string LayoutPath(StageDefinition selected) => Root + "/" + selected.name + "_Layout.prefab";
    static string TileFolder(StageDefinition selected) => Root + "/Tiles/" + selected.name;

    [MenuItem("Gain Ground/Recriar estágio 1-1 no editor pintável")]
    public static void RecreateFirstStage() => RecreateStage(
        "Assets/GainGround/Editable/OriginalCampaign/Stage_1_1.asset");

    public static void RecreateStage(string stagePath)
    {
        var first = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (first == null || first.background == null)
            throw new InvalidOperationException("Estágio ou mapa original ausente: " + stagePath);
        EditorPrefs.SetString("GainGround.StagePainter.Stage", AssetDatabase.AssetPathToGUID(stagePath));
        var editor = CreateInstance<GainGroundStagePainter>();
        try
        {
            editor.stage = first;
            string path = LayoutPath(first);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) editor.CreateLayout(path);
            editor.ImportSourceMap(path);
            Debug.Log("Estágio convertido para Tilemap editável: " + path);
        }
        finally { DestroyImmediate(editor); }
    }

    void ImportSourceMap(string layoutPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath);
        if (prefab == null) throw new FileNotFoundException("Layout ausente", layoutPath);
        string mapPath = AssetDatabase.GetAssetPath(stage.background);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var root = PrefabUtility.LoadPrefabContents(layoutPath);
        try
        {
            source.LoadImage(File.ReadAllBytes(AbsoluteAssetPath(mapPath)));
            int columns = Mathf.RoundToInt(stage.size.x);
            int rows = Mathf.RoundToInt(stage.size.y);
            if (source.width % columns != 0)
                throw new InvalidOperationException("A largura do mapa não corresponde à grade da fase.");
            int pixels = source.width / columns;
            if (source.width != columns * pixels || source.height != rows * pixels)
                throw new InvalidOperationException("O mapa não possui tiles quadrados alinhados à grade da fase.");
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null || layout.ground == null)
                throw new InvalidOperationException("O prefab não contém StagePaintLayout e Ground Tilemap.");
            EnsureFolder(TileFolder(stage));
            string folder = TileFolder(stage);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    string name = "Original_" + col.ToString("00") + "_" + row.ToString("00");
                    string imagePath = folder + "/" + name + ".png";
                    string absoluteImagePath = AbsoluteAssetPath(imagePath);
                    var slice = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
                    try
                    {
                        slice.SetPixels(source.GetPixels(col * pixels, row * pixels, pixels, pixels));
                        slice.Apply(false);
                        byte[] encoded = slice.EncodeToPNG();
                        if (!File.Exists(absoluteImagePath) ||
                            !File.ReadAllBytes(absoluteImagePath).SequenceEqual(encoded))
                        {
                            File.WriteAllBytes(absoluteImagePath, encoded);
                            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
                        }
                    }
                    finally { DestroyImmediate(slice); }
                    var importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Tile ausente: " + imagePath);
                    if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != pixels)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = pixels;
                        importer.mipmapEnabled = false;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                    string tilePath = folder + "/" + name + ".asset";
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (tile == null)
                    {
                        tile = CreateInstance<Tile>();
                        AssetDatabase.CreateAsset(tile, tilePath);
                    }
                    tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                    tile.colliderType = Tile.ColliderType.None;
                    EditorUtility.SetDirty(tile);
                    layout.ground.SetTile(new Vector3Int(col - columns / 2, row - rows / 2, 0), tile);
                }
            var guide = root.transform.Find("Source map guide");
            if (guide != null) DestroyImmediate(guide.gameObject);
            layout.sourceBackground = null;
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            DestroyImmediate(source);
        }
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath).GetComponent<StagePaintLayout>();
        if (saved == null || saved.ground == null || saved.ground.GetUsedTilesCount() == 0)
            throw new InvalidOperationException("A conversão não persistiu os tiles no prefab.");
    }

    StagePaintLayout CurrentLayout()
    {
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null || prefabStage.assetPath != LayoutPath(stage)) return null;
        return prefabStage.prefabContentsRoot.GetComponent<StagePaintLayout>();
    }

    void OpenLayout()
    {
        string path = LayoutPath(stage);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) CreateLayout(path);
        var opened = PrefabStageUtility.OpenPrefab(path);
        Selection.activeGameObject = opened.prefabContentsRoot;
        paintEnabled = true;
        SceneView.lastActiveSceneView?.FrameSelected();
        Repaint();
    }

    void CreateLayout(string path)
    {
        EnsureFolder(Root);
        var root = new GameObject(stage.name + "_Layout", typeof(Grid), typeof(StagePaintLayout));
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            layout.stage = stage;
            layout.sourceBackground = stage.background;
            layout.originalWalls = (Rect[])(stage.walls ?? Array.Empty<Rect>()).Clone();
            layout.grid = root.GetComponent<Grid>();
            layout.grid.cellSize = Vector3.one;
            if (stage.background != null)
            {
                var backdrop = new GameObject("Source map guide", typeof(SpriteRenderer));
                backdrop.transform.SetParent(root.transform, false);
                var renderer = backdrop.GetComponent<SpriteRenderer>();
                renderer.sprite = stage.background;
                renderer.sortingOrder = -10;
                backdrop.transform.localScale = new Vector3(
                    stage.size.x / stage.background.bounds.size.x,
                    stage.size.y / stage.background.bounds.size.y, 1);
            }
            layout.ground = AddLayer(root.transform, "Ground", -9);
            layout.elevations = AddLayer(root.transform, "Elevations", -8);
            layout.decorations = AddLayer(root.transform, "Decorations", -7);
            layout.effects = AddLayer(root.transform, "Effects", -6);
            layout.collision = AddLayer(root.transform, "Blocking", 5);
            layout.collision.color = new Color(1, .2f, .2f, .45f);
            ImportExistingMarkers(layout);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        }
        finally { DestroyImmediate(root); }
    }

    static Tilemap AddLayer(Transform parent, string name, int sortingOrder)
    {
        var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        child.transform.SetParent(parent, false);
        child.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return child.GetComponent<Tilemap>();
    }

    void ImportExistingMarkers(StagePaintLayout layout)
    {
        var objectIds = new List<string>();
        foreach (var item in stage.objects ?? Array.Empty<StageObjectSpawn>())
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.id))
            {
                Undo.RecordObject(stage, "Identify stage object");
                item.id = "PAINT:" + Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(stage);
            }
            objectIds.Add(item.id);
            var marker = CreateExistingMarker(layout, item.id, StagePaintMarkerKind.Object, item.area);
            marker.visualPrefab = item.prefab;
            marker.health = item.health;
            marker.teleportGroup = item.teleportGroup;
            StagePaintMarkerInspector.SyncPreview(marker);
        }
        layout.managedObjectIds = objectIds.ToArray();
        foreach (var item in stage.hazards ?? Array.Empty<HazardSpawn>())
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.editorId))
            {
                Undo.RecordObject(stage, "Identify stage hazard");
                item.editorId = "PAINT:" + Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(stage);
            }
            var marker = CreateExistingMarker(layout, item.editorId, StagePaintMarkerKind.Hazard, item.area);
            marker.visualPrefab = item.prefab;
            marker.hazardSprite = item.sprite;
            marker.controllers = item.controllers ?? Array.Empty<int>();
            StagePaintMarkerInspector.SyncPreview(marker);
        }
    }

    static StagePaintMarker CreateExistingMarker(StagePaintLayout layout, string id,
        StagePaintMarkerKind kind, Rect area)
    {
        var go = new GameObject(kind == StagePaintMarkerKind.Object ? "Object" : "Hazard");
        go.transform.SetParent(layout.transform, false);
        go.transform.localPosition = area.center;
        var marker = go.AddComponent<StagePaintMarker>();
        marker.id = id;
        marker.kind = kind;
        marker.size = area.size;
        return marker;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    void ImportTiles()
    {
        var palette = AssetDatabase.LoadAssetAtPath<StageTexturePalette>(Palettes + "/" + stage.name + ".asset");
        if (palette == null) { EditorUtility.DisplayDialog("Sem atlas", "A fase não possui StageTexturePalette.", "OK"); return; }
        EnsureFolder(TileFolder(stage));
        ImportAtlas(palette.floor, "Floor");
        ImportAtlas(palette.elevations, "Elevation");
        ImportAtlas(palette.constructions, "Construction");
        AssetDatabase.SaveAssets();
        Repaint();
    }

    void ImportAtlas(Texture2D texture, string label)
    {
        if (texture == null) return;
        string path = AssetDatabase.GetAssetPath(texture);
        if (!path.StartsWith("Assets/GainGround/Editable/OriginalCampaign/StageTextures/", StringComparison.Ordinal))
            throw new InvalidOperationException("Use um atlas da pasta StageTextures: " + path);
        const int pixels = 64;
        if (texture.width < pixels || texture.height < pixels)
            throw new InvalidOperationException("O atlas é menor que 64 pixels: " + path);
        string folder = TileFolder(stage);
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Texture2D readable = null;
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply(false);
            for (int y = 0; y + pixels <= texture.height; y += pixels)
                for (int x = 0; x + pixels <= texture.width; x += pixels)
                {
                    string name = label + "_" + x / pixels + "_" + y / pixels;
                    string imagePath = folder + "/" + name + ".png";
                    if (!File.Exists(AbsoluteAssetPath(imagePath)))
                    {
                        var slice = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
                        try
                        {
                            slice.SetPixels(readable.GetPixels(x, y, pixels, pixels));
                            slice.Apply(false);
                            File.WriteAllBytes(AbsoluteAssetPath(imagePath), slice.EncodeToPNG());
                        }
                        finally { DestroyImmediate(slice); }
                        AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
                    }
                    var importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Tile PNG inválido: " + imagePath);
                    if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != pixels)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = pixels;
                        importer.mipmapEnabled = false;
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                    }
                    string tilePath = folder + "/" + name + ".asset";
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (tile == null)
                    {
                        tile = CreateInstance<Tile>();
                        AssetDatabase.CreateAsset(tile, tilePath);
                    }
                    tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                    tile.colliderType = Tile.ColliderType.None;
                    tile.flags = TileFlags.None;
                    EditorUtility.SetDirty(tile);
                }
        }
        finally
        {
            RenderTexture.active = previous;
            if (readable != null) DestroyImmediate(readable);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    static string AbsoluteAssetPath(string path) => Path.Combine(
        Path.GetFullPath(Path.Combine(Application.dataPath, "..")), path);

    void DrawTiles()
    {
        string folder = TileFolder(stage);
        string prefix = layer == 0 || layer == 4 ? "Floor_" :
            layer == 1 ? "Elevation_" : layer == 2 ? "Construction_" : "";
        var tilePaths = AssetDatabase.IsValidFolder(folder)
            ? AssetDatabase.FindAssets("t:Tile", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefix, StringComparison.Ordinal) ||
                    (layer == 0 && Path.GetFileNameWithoutExtension(p).StartsWith("Original_", StringComparison.Ordinal)))
                .OrderBy(p => p).ToArray()
            : Array.Empty<string>();
        if (tilePaths.Length == 0)
        {
            EditorGUILayout.HelpBox("Prepare os atlas da fase ou atribua um Tile asset ao campo Pincel.", MessageType.Info);
        }
        else
        {
            tileScroll = EditorGUILayout.BeginScrollView(tileScroll, GUILayout.Height(190));
            int columns = Mathf.Max(1, (int)(position.width - 25) / 68);
            for (int i = 0; i < tilePaths.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + columns, tilePaths.Length); j++)
                {
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePaths[j]);
                    var icon = tile != null && tile.sprite != null ? AssetPreview.GetAssetPreview(tile.sprite) : null;
                    if (icon == null && tile != null && tile.sprite != null) icon = AssetPreview.GetMiniThumbnail(tile.sprite);
                    bool active = selectedTile == tile;
                    var old = GUI.backgroundColor;
                    if (active) GUI.backgroundColor = new Color(.5f, .9f, 1);
                    if (GUILayout.Button(new GUIContent(icon, tile != null ? tile.name : ""), GUILayout.Width(62), GUILayout.Height(62)))
                        selectedTile = tile;
                    GUI.backgroundColor = old;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
        selectedTile = (TileBase)EditorGUILayout.ObjectField("Pincel", selectedTile, typeof(TileBase), false);
    }

    void AddMarker(StagePaintLayout layout, StagePaintMarkerKind kind)
    {
        if (kind == StagePaintMarkerKind.Object && objectPrefab == null)
        { EditorUtility.DisplayDialog("Prefab ausente", "Escolha um prefab para o objeto.", "OK"); return; }
        if (kind == StagePaintMarkerKind.Hazard && objectPrefab == null && hazardSprite == null)
        { EditorUtility.DisplayDialog("Visual ausente", "Escolha um prefab ou sprite para o perigo.", "OK"); return; }
        var go = new GameObject(kind == StagePaintMarkerKind.Object ? "Object" :
            kind == StagePaintMarkerKind.Hazard ? "Hazard" : "Blocking area");
        Undo.RegisterCreatedObjectUndo(go, "Add stage marker");
        SceneManager.MoveGameObjectToScene(go, layout.gameObject.scene);
        go.transform.SetParent(layout.transform, false);
        go.transform.localPosition = stage.start;
        var marker = Undo.AddComponent<StagePaintMarker>(go);
        marker.id = "PAINT:" + Guid.NewGuid().ToString("N");
        marker.kind = kind;
        marker.visualPrefab = kind == StagePaintMarkerKind.Blocker ? null : objectPrefab;
        marker.hazardSprite = hazardSprite;
        marker.blocksMovement = kind == StagePaintMarkerKind.Blocker;
        EditorUtility.SetDirty(marker);
        StagePaintMarkerInspector.SyncPreview(marker);
        Selection.activeGameObject = go;
        SceneView.RepaintAll();
    }

    void PaintScene(SceneView view)
    {
        if (stage == null) return;
        var layout = CurrentLayout();
        if (layout == null) return;
        if (editStagePoints) DrawStagePointHandles();
        if (!paintEnabled || editStagePoints) return;
        var tilemap = new[] { layout.ground, layout.elevations, layout.decorations, layout.effects, layout.collision }[layer];
        if (tilemap == null || (!erase && selectedTile == null)) return;
        Event e = Event.current;
        if (e.alt || e.control || e.command) return;
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Mathf.Abs(ray.direction.z) < .0001f) return;
        Vector3 world = ray.GetPoint(-ray.origin.z / ray.direction.z);
        Vector3Int cell = tilemap.WorldToCell(world);
        Vector3 center = tilemap.GetCellCenterWorld(cell);
        if (Mathf.Abs(center.x) > stage.size.x * .5f || Mathf.Abs(center.y) > stage.size.y * .5f) return;
        Handles.color = erase ? Color.red : Color.cyan;
        Handles.DrawWireCube(center, Vector3.one);
        if (e.type == EventType.MouseUp) lastPainted.x = int.MinValue;
        if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || e.button != 0) return;
        if (e.type == EventType.MouseDrag && cell == lastPainted) { e.Use(); return; }
        Undo.RecordObject(tilemap, erase ? "Erase stage tile" : "Paint stage tile");
        tilemap.SetTile(cell, erase ? null : selectedTile);
        EditorUtility.SetDirty(tilemap);
        lastPainted = cell;
        e.Use();
        view.Repaint();
    }

    void DrawStagePointHandles()
    {
        var nextStart = MovePoint("Início", stage.start, Color.cyan);
        var nextExit = MovePoint("Saída", stage.exit.center, Color.green);
        var nextRescue = stage.rescue != null ? MovePoint("Resgate", stage.rescuePosition, Color.yellow) : stage.rescuePosition;
        var nextEnemies = new Vector2[stage.enemies == null ? 0 : stage.enemies.Length];
        for (int i = 0; i < nextEnemies.Length; i++)
            nextEnemies[i] = MovePoint("Inimigo " + (i + 1), stage.enemies[i].position, Color.red);
        var nextCaptives = new Vector2[stage.additionalCaptives == null ? 0 : stage.additionalCaptives.Length];
        for (int i = 0; i < nextCaptives.Length; i++)
            nextCaptives[i] = MovePoint("Resgate extra " + (i + 1), stage.additionalCaptives[i].position, Color.yellow);
        var nextExits = new Vector2[stage.additionalExits == null ? 0 : stage.additionalExits.Length];
        for (int i = 0; i < nextExits.Length; i++)
            nextExits[i] = MovePoint("Saída extra " + (i + 1), stage.additionalExits[i].center, Color.green);
        bool changed = nextStart != stage.start || nextExit != stage.exit.center || nextRescue != stage.rescuePosition;
        for (int i = 0; i < nextEnemies.Length; i++) changed |= nextEnemies[i] != stage.enemies[i].position;
        for (int i = 0; i < nextCaptives.Length; i++) changed |= nextCaptives[i] != stage.additionalCaptives[i].position;
        for (int i = 0; i < nextExits.Length; i++) changed |= nextExits[i] != stage.additionalExits[i].center;
        if (!changed) return;
        Undo.RecordObject(stage, "Move stage spawn or exit");
        stage.start = nextStart;
        stage.exit = new Rect(nextExit - stage.exit.size * .5f, stage.exit.size);
        stage.rescuePosition = nextRescue;
        for (int i = 0; i < nextEnemies.Length; i++) stage.enemies[i].position = nextEnemies[i];
        for (int i = 0; i < nextCaptives.Length; i++) stage.additionalCaptives[i].position = nextCaptives[i];
        for (int i = 0; i < nextExits.Length; i++)
            stage.additionalExits[i] = new Rect(nextExits[i] - stage.additionalExits[i].size * .5f,
                stage.additionalExits[i].size);
        EditorUtility.SetDirty(stage);
    }

    static Vector2 MovePoint(string label, Vector2 position, Color color)
    {
        Handles.color = color;
        Vector3 point = new Vector3(position.x, position.y, 0);
        Handles.Label(point + Vector3.up * .2f, label);
        float size = HandleUtility.GetHandleSize(point) * .11f;
        Vector3 next = Handles.FreeMoveHandle(point, size, Vector3.zero, Handles.DotHandleCap);
        return new Vector2(next.x, next.y);
    }
}

[CustomEditor(typeof(StagePaintMarker))]
public sealed class StagePaintMarkerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) SyncPreview((StagePaintMarker)target);
    }

    public static void SyncPreview(StagePaintMarker marker)
    {
        var source = marker.visualPrefab != null ? marker.visualPrefab.GetComponentInChildren<SpriteRenderer>() : null;
        Sprite sprite = source != null ? source.sprite : marker.hazardSprite;
        Transform old = marker.transform.Find("Marker preview");
        if (sprite == null)
        {
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            return;
        }
        GameObject preview;
        if (old == null)
        {
            preview = new GameObject("Marker preview", typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(preview, "Create stage marker preview");
            SceneManager.MoveGameObjectToScene(preview, marker.gameObject.scene);
            preview.transform.SetParent(marker.transform, false);
        }
        else preview = old.gameObject;
        var renderer = preview.GetComponent<SpriteRenderer>();
        Undo.RecordObject(renderer, "Update stage marker preview");
        Undo.RecordObject(preview.transform, "Resize stage marker preview");
        renderer.sprite = sprite;
        renderer.sortingOrder = 3;
        renderer.drawMode = source != null ? source.drawMode : SpriteDrawMode.Simple;
        if (renderer.drawMode == SpriteDrawMode.Tiled)
        {
            renderer.size = marker.size;
            preview.transform.localScale = Vector3.one;
        }
        else preview.transform.localScale = new Vector3(
            marker.size.x / sprite.bounds.size.x, marker.size.y / sprite.bounds.size.y, 1);
        preview.transform.localPosition = Vector3.zero;
        EditorUtility.SetDirty(renderer);
    }
}
