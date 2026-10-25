using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class PS2StageObjectInstaller
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign/Objects";
    const string SpriteFolder = Root + "/Sprites";
    const string PrefabFolder = Root + "/Prefabs";

    [MenuItem("Gain Ground/Import PS2 stage objects and install")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        EnsureFolder(PrefabFolder);
        var boards = Prefab("WoodWallBoards", true, 0);
        var cap = Prefab("WoodWallCap", true, 0);
        var gate = WoodenGatePrefab(cap, boards);
        var portal = Prefab("TeleportRing", false, 1);
        var laser = Prefab("LaserStrip", true, 3);
        var arch = Prefab("StoneArch", false, -1);
        var door = Prefab("IronDoor", false, -1);

        var palisade = Stage(1, 6);
        var walls = new List<StageObjectSpawn>();
        for (int i = 0; i < palisade.walls.Length; i++)
            walls.Add(new StageObjectSpawn { id = "PS2:WoodWall:" + i,
                prefab = i < 2 ? boards : cap, area = palisade.walls[i] });
        walls.Add(new StageObjectSpawn { id = "PS2:WoodenGate:0", prefab = gate,
            area = new Rect(-1.5f, -2f, 3f, 1.5f), health = 6 });
        ReplaceImportedObjects(palisade, walls);

        var square = Stage(4, 2);
        // These two safe floor positions belong to the current adapted layout.
        // The original PS2 geometry has not been decoded from its model data.
        ReplaceImportedObjects(square, new[] {
            new StageObjectSpawn { id = "PS2:Teleport:A", prefab = portal,
                area = new Rect(-5.5f, -5.2f, 1.4f, 1.4f), teleportGroup = 1 },
            new StageObjectSpawn { id = "PS2:Teleport:B", prefab = portal,
                area = new Rect(4.1f, -5.2f, 1.4f, 1.4f), teleportGroup = 1 }
        });

        var ruins = Stage(2, 3);
        ReplaceImportedObjects(ruins, new[] {
            new StageObjectSpawn { id = "PS2:StoneArch", prefab = arch,
                area = new Rect(ruins.exit.center.x - 1.25f, 4.3f, 2.5f, 2.1f) }
        });
        var prison = Stage(2, 8);
        ReplaceImportedObjects(prison, new[] {
            new StageObjectSpawn { id = "PS2:IronDoor", prefab = door,
                area = new Rect(prison.exit.center.x - 1.0f, 4.5f, 2f, 2f) }
        });

        var barriers = Stage(5, 7);
        if (barriers.hazards == null || barriers.hazards.Length != 4)
            throw new InvalidDataException("O estágio 5-7 deveria ter quatro lasers controlados por geradores.");
        foreach (var hazard in barriers.hazards) hazard.prefab = laser;
        EditorUtility.SetDirty(barriers);
        AssetDatabase.SaveAssets();
        Debug.Log("Objetos PS2 instalados nos estágios 1-6, 2-3, 2-8, 4-2 e 5-7.");
    }

    static StageDefinition Stage(int round, int number)
    {
        string path = $"Assets/GainGround/Editable/OriginalCampaign/Stage_{round}_{number}.asset";
        var result = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
        if (result == null) throw new FileNotFoundException("Fase não encontrada", path);
        return result;
    }

    static void ReplaceImportedObjects(StageDefinition stage, IEnumerable<StageObjectSpawn> imported)
    {
        var result = stage.objects == null ? new List<StageObjectSpawn>() :
            stage.objects.Where(item => item != null &&
                (item.id == null || !item.id.StartsWith("PS2:", StringComparison.Ordinal))).ToList();
        result.AddRange(imported);
        stage.objects = result.ToArray();
        EditorUtility.SetDirty(stage);
    }

    static GameObject Prefab(string name, bool tiled, int order)
    {
        string imagePath = SpriteFolder + "/" +
            (name == "WoodWallCap" ? "WoodWallCapClean" : name) + ".png";
        var importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Sprite PS2 ausente", imagePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
        if (sprite == null) throw new InvalidDataException("Falha ao importar sprite " + imagePath);
        string path = PrefabFolder + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var source = new GameObject(name);
        try
        {
            var renderer = source.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.drawMode = tiled ? SpriteDrawMode.Tiled : SpriteDrawMode.Simple;
            return PrefabUtility.SaveAsPrefabAsset(source, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    static GameObject WoodenGatePrefab(GameObject cap, GameObject boards)
    {
        string path = PrefabFolder + "/WoodenGate.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var source = new GameObject("WoodenGate");
        try
        {
            var face = source.AddComponent<SpriteRenderer>();
            face.sprite = cap.GetComponent<SpriteRenderer>().sprite;
            face.drawMode = SpriteDrawMode.Simple;
            face.size = new Vector2(3f, 1.5f);
            face.sortingOrder = 2;
            for (int i = 0; i < 2; i++)
            {
                var brace = new GameObject(i == 0 ? "Left wooden brace" : "Right wooden brace");
                brace.transform.SetParent(source.transform, false);
                brace.transform.localPosition = new Vector3(i == 0 ? -8.25f : 8.25f, 0, 0);
                brace.transform.localRotation = Quaternion.identity;
                brace.transform.localScale = new Vector3(.27f, .335f, 1);
                var braceSprite = brace.AddComponent<SpriteRenderer>();
                braceSprite.sprite = boards.GetComponent<SpriteRenderer>().sprite;
                braceSprite.sortingOrder = 3;
                braceSprite.color = Color.white;
            }
            return PrefabUtility.SaveAsPrefabAsset(source, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
