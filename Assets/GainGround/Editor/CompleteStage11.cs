using System;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// One-time authoring pass. The saved prefab remains the editable source of the stage.
public static class CompleteStage11
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";
    const string LayoutPath = Root + "/PaintedStages/Stage_1_1_Layout.prefab";
    const string Art = Root + "/PaintedStages/Stage_1_1_Objects";
    const string Palette = Root + "/StageTextures/1-01";

    [MenuItem("Gain Ground/Completar layout editável do estágio 1-1")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath) == null)
            GainGroundStagePainter.RecreateFirstStage();
        if (!AssetDatabase.IsValidFolder(Art))
            AssetDatabase.CreateFolder(Root + "/PaintedStages", "Stage_1_1_Objects");

        var rockSprite = Slice(Palette + "/Constructions.png", "Rock", new RectInt(112, 300, 128, 128), 128);
        var bushSprite = Slice(Palette + "/Constructions.png", "Bush", new RectInt(846, 654, 128, 128), 128);
        var elevationTile = CreateTile(Slice(Palette + "/Elevations.png", "RaisedGround",
            new RectInt(384, 384, 64, 64), 64), "RaisedGround");
        var rock = ObjectPrefab("Rock", rockSprite, new Color(.35f, .25f, .17f, .8f), true);
        var bush = ObjectPrefab("Bush", bushSprite, new Color(.15f, .28f, .12f, .7f), false);

        var root = PrefabUtility.LoadPrefabContents(LayoutPath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null || layout.ground == null || layout.effects == null ||
                layout.elevations == null || layout.collision == null)
                throw new InvalidOperationException("O layout 1-1 não contém todas as camadas editáveis.");

            // The EXIT artwork is a separate paintable layer over normal ground.
            for (int x = -6; x < 6; x++)
            {
                var top = new Vector3Int(x, 6, 0);
                if (!layout.effects.HasTile(top))
                {
                    layout.effects.SetTile(top, layout.ground.GetTile(top));
                    layout.ground.SetTile(top, layout.ground.GetTile(new Vector3Int(x, 5, 0)));
                }
            }

            RemoveGeneratedMarkers(layout);
            var rocks = new[] {
                new Vector2(-4.8f, -4.2f), new Vector2(4.8f, -4.2f),
                new Vector2(-4.8f, -1.3f), new Vector2(4.8f, 2.8f)
            };
            for (int i = 0; i < rocks.Length; i++)
            {
                AddObject(layout, "Rock " + (i + 1), "PAINT:STAGE11_ROCK_" + i,
                    rock, rocks[i], new Vector2(1.2f, 1.1f), true);
                var cell = layout.elevations.WorldToCell(rocks[i]);
                layout.elevations.SetTile(cell, elevationTile);
            }
            var bushes = new[] {
                new Vector2(-5.2f, -2.7f), new Vector2(5.15f, -2.7f),
                new Vector2(-5.1f, 2.2f), new Vector2(5.1f, 4.7f)
            };
            for (int i = 0; i < bushes.Length; i++)
                AddObject(layout, "Bush " + (i + 1), "PAINT:STAGE11_BUSH_" + i,
                    bush, bushes[i], new Vector2(.65f, .8f), false);

            PrefabUtility.SaveAsPrefabAsset(root, LayoutPath);
            GainGroundStagePaintBaker.Bake(layout);
            ApplyNativeScenery();
            PrefabUtility.SaveAsPrefabAsset(root, LayoutPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Estágio 1-1 concluído: solo e saída em Tilemaps, elevações, rochas com colisão e vegetação editáveis.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Gain Ground/Atualizar contorno aberto do estágio 1-1")]
    public static void RefreshNativePerimeter()
    {
        if (!AssetDatabase.IsValidFolder(Art))
            AssetDatabase.CreateFolder(Root + "/PaintedStages", "Stage_1_1_Objects");
        ApplyNativeScenery();
        AssetDatabase.SaveAssets();
    }

    static void ApplyNativeScenery()
    {
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_1.asset");
        if (stage == null) throw new InvalidOperationException("Stage_1_1.asset is missing.");
        string[] rocks = { "03", "04", "05", "06" };
        string[] bushes = { "01", "02", "01", "02" };
        foreach (var item in stage.objects)
        {
            if (item.id == null) continue;
            for (int i = 0; i < 4; i++)
            {
                if (item.id == "PAINT:STAGE11_ROCK_" + i) item.prefab = Native(rocks[i]);
                if (item.id == "PAINT:STAGE11_BUSH_" + i) item.prefab = Native(bushes[i]);
            }
        }
        var perimeter = Array.Find(stage.objects, item => item.id == "PS2:NativePerimeter");
        if (perimeter == null)
        {
            var objects = new System.Collections.Generic.List<StageObjectSpawn>(stage.objects);
            objects.Add(new StageObjectSpawn { id = "PS2:NativePerimeter" });
            stage.objects = objects.ToArray();
            perimeter = stage.objects[stage.objects.Length - 1];
        }
        perimeter.prefab = OpenExitPerimeter();
        perimeter.area = new Rect(-stage.size * .5f, stage.size);
        // Keep the inner edges near x = +/-5.25, leaving the playfield open.
        perimeter.modelScale = new Vector3(1.75f, stage.size.y / 8.75f, 1);
        perimeter.health = 0;
        EditorUtility.SetDirty(stage);
    }

    static GameObject OpenExitPerimeter()
    {
        string sourcePath = AssetDatabase.GetAssetPath(Native("w1"));
        string meshPath = Art + "/PerimeterOpenExit.asset";
        string prefabPath = Art + "/PerimeterOpenExit.prefab";
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            var filter = root.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("O modelo do contorno 1-1 não contém malha.");
            var source = filter.sharedMesh;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "PerimeterOpenExit";
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else EditorUtility.CopySerialized(source, mesh);
            mesh.name = "PerimeterOpenExit";
            var vertices = source.vertices;
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var topology = source.GetTopology(submesh);
                var indices = source.GetIndices(submesh);
                int stride = topology == MeshTopology.Triangles ? 3 :
                    topology == MeshTopology.Lines ? 2 : 1;
                var kept = new System.Collections.Generic.List<int>(indices.Length);
                for (int i = 0; i + stride <= indices.Length; i += stride)
                {
                    bool clearEdge = false;
                    for (int j = 0; j < stride; j++)
                        clearEdge |= Mathf.Abs(vertices[indices[i + j]].z) > 115f;
                    if (clearEdge) continue;
                    for (int j = 0; j < stride; j++) kept.Add(indices[i + j]);
                }
                mesh.SetIndices(kept.ToArray(), topology, submesh);
            }
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            filter.sharedMesh = mesh;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (prefab == null) throw new InvalidOperationException("Falha ao salvar contorno aberto da 1-1.");
            return prefab;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static GameObject Native(string suffix)
    {
        string name = "r1s01_" + suffix;
        string path = PS2StageModelInstaller.Root + "/Stage_1_01/" + name + "/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException("Import PS2 stage models first", path);
        return prefab;
    }

    static void RemoveGeneratedMarkers(StagePaintLayout layout)
    {
        foreach (var marker in layout.GetComponentsInChildren<StagePaintMarker>(true))
            if (marker.id != null && marker.id.StartsWith("PAINT:STAGE11_", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
    }

    static void AddObject(StagePaintLayout layout, string name, string id, GameObject prefab,
        Vector2 position, Vector2 size, bool blocks)
    {
        var go = new GameObject(name, typeof(StagePaintMarker));
        go.transform.SetParent(layout.transform, false);
        go.transform.localPosition = position;
        var marker = go.GetComponent<StagePaintMarker>();
        marker.id = id;
        marker.kind = StagePaintMarkerKind.Object;
        marker.visualPrefab = prefab;
        marker.size = size;
        marker.blocksMovement = blocks;
        StagePaintMarkerInspector.SyncPreview(marker);
    }

    static Sprite Slice(string sourcePath, string name, RectInt region, int pixelsPerUnit)
    {
        string targetPath = Art + "/" + name + ".png";
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var slice = new Texture2D(region.width, region.height, TextureFormat.RGBA32, false);
        try
        {
            source.LoadImage(File.ReadAllBytes(Absolute(sourcePath)));
            slice.SetPixels(source.GetPixels(region.x, region.y, region.width, region.height));
            slice.Apply(false);
            File.WriteAllBytes(Absolute(targetPath), slice.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(slice); }
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(targetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
    }

    static Tile CreateTile(Sprite sprite, string name)
    {
        string path = Art + "/" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static GameObject ObjectPrefab(string name, Sprite sprite, Color sideColor, bool hasCollider)
    {
        string path = Art + "/" + name + ".prefab";
        var root = new GameObject(name, typeof(SpriteRenderer));
        try
        {
            var top = root.GetComponent<SpriteRenderer>();
            top.sprite = sprite;
            top.sortingOrder = 1;
            if (hasCollider) root.AddComponent<BoxCollider2D>();
            var side = new GameObject("Lower face", typeof(SpriteRenderer));
            side.transform.SetParent(root.transform, false);
            side.transform.localPosition = new Vector3(0, -.17f, 0);
            var sideVisual = side.GetComponent<SpriteRenderer>();
            sideVisual.sprite = sprite;
            sideVisual.color = sideColor;
            sideVisual.sortingOrder = -5;
            var shadow = new GameObject("Ground shadow", typeof(SpriteRenderer));
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(.1f, -.28f, 0);
            shadow.transform.localScale = new Vector3(1.18f, .42f, 1);
            var shadowVisual = shadow.GetComponent<SpriteRenderer>();
            shadowVisual.sprite = sprite;
            shadowVisual.color = new Color(.08f, .06f, .04f, .45f);
            shadowVisual.sortingOrder = -6;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static string Absolute(string path) => Path.Combine(
        Path.GetFullPath(Path.Combine(Application.dataPath, "..")), path);
}
