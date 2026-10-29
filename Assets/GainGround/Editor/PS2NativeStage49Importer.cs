using System;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the PS2 ISO's 4-9 terrain and model projection into an editable Tilemap.</summary>
public static class PS2NativeStage49Importer
{
    const string StagePath = "Assets/GainGround/Editable/OriginalCampaign/Stage_4_9.asset";
    const string MapPath = "Assets/GainGround/Editable/OriginalCampaign/PS2Native/Stage_4_9.png";
    const string DataPath = "Assets/GainGround/Editable/OriginalCampaign/PS2Native/Stage_4_9.json";
    const string LayoutPath = "Assets/GainGround/Editable/OriginalCampaign/PaintedStages/Stage_4_9_Layout.prefab";

    [Serializable]
    sealed class NativeData
    {
        public Vector2[] enemyPositions;
    }

    [MenuItem("Gain Ground/Import PS2 geometry/Stage 4-9")]
    public static void Apply()
    {
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath);
        if (stage == null || !File.Exists(MapPath) || !File.Exists(DataPath))
            throw new FileNotFoundException("Run python Tools/rebuild_ps2_stage_4_9.py before importing the PS2 geometry.");
        var native = JsonUtility.FromJson<NativeData>(File.ReadAllText(DataPath));
        if (native?.enemyPositions == null || native.enemyPositions.Length != 22)
            throw new InvalidDataException("The ISO 4-9 actor positions are incomplete.");

        AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(MapPath) as TextureImporter;
        if (importer == null) throw new InvalidDataException("The PS2 stage map did not import as a texture.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MapPath);
        if (sprite == null) throw new InvalidDataException("The PS2 stage map is missing its sprite.");

        // Erase the old Mega Drive tiles and collision markers before replacing the floor.
        var root = PrefabUtility.LoadPrefabContents(LayoutPath);
        try
        {
            var oldLayout = root.GetComponent<StagePaintLayout>();
            if (oldLayout == null) throw new InvalidDataException("The 4-9 editable layout is missing.");
            oldLayout.ground?.ClearAllTiles();
            oldLayout.elevations?.ClearAllTiles();
            oldLayout.decorations?.ClearAllTiles();
            oldLayout.effects?.ClearAllTiles();
            oldLayout.collision?.ClearAllTiles();
            foreach (var marker in root.GetComponentsInChildren<StagePaintMarker>(true))
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            oldLayout.originalWalls = Array.Empty<Rect>();
            oldLayout.lastBakedWalls = Array.Empty<Rect>();
            PrefabUtility.SaveAsPrefabAsset(root, LayoutPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        Undo.RecordObject(stage, "Import PS2 stage 4-9 geometry");
        stage.size = new Vector2(13, 10); // 416 by 320 PS2 units, at 32 units per cell.
        stage.start = new Vector2(0, -4.5f);
        stage.exit = new Rect(-.5f, -.5f, 1, .5f);
        stage.additionalExits = Array.Empty<Rect>();
        stage.walls = Array.Empty<Rect>();
        stage.background = sprite;
        // The PS2 stage uses the floating spherical enemy, unlike the Mega Drive
        // soldier/grenadier entries previously copied into this stage.
        var orbStage = AssetDatabase.LoadAssetAtPath<StageDefinition>(
            "Assets/GainGround/Editable/OriginalCampaign/Stage_5_3.asset");
        EnemySpawn orb = Array.Find(orbStage != null ? orbStage.enemies : Array.Empty<EnemySpawn>(),
            enemy => enemy != null && enemy.behavior == EnemyBehavior.Orb);
        if (orb == null || orb.sprite == null)
            throw new InvalidDataException("The PS2 orb enemy sprite is missing.");
        int originalCount = stage.enemies.Length;
        if (originalCount == 0) throw new InvalidDataException("Stage 4-9 has no enemy archetypes to preserve.");
        Array.Resize(ref stage.enemies, native.enemyPositions.Length);
        for (int index = originalCount; index < stage.enemies.Length; index++)
            stage.enemies[index] = CopyEnemy(orb);
        for (int index = 0; index < stage.enemies.Length; index++)
        {
            stage.enemies[index].behavior = EnemyBehavior.Orb;
            stage.enemies[index].sprite = orb.sprite;
            // The current orb model asset contains an entire stage structure and
            // covers the arena when instantiated at each enemy position.
            stage.enemies[index].modelPrefab = null;
            stage.enemies[index].visualSize = orb.visualSize * .55f;
            stage.enemies[index].health = orb.health;
            stage.enemies[index].speed = 0;
            stage.enemies[index].sentry = true;
            stage.enemies[index].position = native.enemyPositions[index];
        }
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();

        GainGroundStagePainter.RecreateStage(StagePath);

        root = PrefabUtility.LoadPrefabContents(LayoutPath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            layout.stage = stage;
            // Thirteen tile columns otherwise span -6..7 while the PS2 terrain spans -6.5..6.5.
            foreach (var layer in new[] { layout.ground, layout.elevations, layout.decorations,
                         layout.effects, layout.collision })
                if (layer != null) layer.transform.localPosition = new Vector3(-.5f, 0, 0);
            layout.originalWalls = Array.Empty<Rect>();
            layout.lastBakedWalls = Array.Empty<Rect>();
            // The eight side structures are separate PS2 meshes. Their inner edge stays
            // outside the actor lane; these rectangles provide the 2D game's collision.
            int serial = 0;
            foreach (float side in new[] { -1f, 1f })
                foreach (float sourceZ in new[] { 0f, 80f, 160f, 240f })
                {
                    var marker = new GameObject("PS2 side structure " + ++serial).AddComponent<StagePaintMarker>();
                    marker.transform.SetParent(layout.transform, false);
                    marker.transform.localPosition = new Vector3(side * 5.9f, (112 - sourceZ) / 32f, 0);
                    marker.id = "PS2:4-9:side:" + serial;
                    marker.kind = StagePaintMarkerKind.Blocker;
                    marker.size = new Vector2(1.2f, 1.4f);
                }
            // The ISO's height grid widens from seven raised vertices on the
            // southern approach to eleven around the exit. Keep the approach
            // open while blocking passage through the two raised side ledges.
            foreach (float side in new[] { -1f, 1f })
            {
                AddLedge(layout, side, 1.22f, -.02f, 2.5f, "upper");
                AddLedge(layout, side, .73f, -1.64f, 1.26f, "approach");
            }
            PrefabUtility.SaveAsPrefabAsset(root, LayoutPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath).GetComponent<StagePaintLayout>();
        GainGroundStagePaintBaker.Bake(saved);
        Debug.Log("Stage 4-9 now uses the PS2 ISO terrain UVs, central elevation, models and actor placements.");
    }

    [MenuItem("Gain Ground/Import PS2 geometry/Stage 4-9", true)]
    static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static EnemySpawn CopyEnemy(EnemySpawn source) => new EnemySpawn {
        health = source.health, speed = source.speed, shotInterval = source.shotInterval,
        initialShotDelay = source.initialShotDelay, sentry = source.sentry,
        elevated = source.elevated, melee = source.melee, spawnDelay = source.spawnDelay,
        behavior = source.behavior, walkingPattern = source.walkingPattern,
        walkingDistance = source.walkingDistance, patrolDistance = source.patrolDistance,
        sprite = source.sprite, modelPrefab = source.modelPrefab,
        visualSize = source.visualSize, hitRadius = source.hitRadius,
        obstacleSize = source.obstacleSize, boss = source.boss,
        freezeImmune = source.freezeImmune, triggerDistance = source.triggerDistance,
        teleportOnVanishDistance = source.teleportOnVanishDistance,
        attackSound = source.attackSound, hitSound = source.hitSound, deathSound = source.deathSound
    };

    static void AddLedge(StagePaintLayout layout, float side, float x, float y, float height, string label)
    {
        var marker = new GameObject("PS2 central " + label + " ledge").AddComponent<StagePaintMarker>();
        marker.transform.SetParent(layout.transform, false);
        marker.transform.localPosition = new Vector3(side * x, y, 0);
        marker.id = "PS2:4-9:ledge:" + label + ":" + (side < 0 ? "left" : "right");
        marker.kind = StagePaintMarkerKind.Blocker;
        marker.size = new Vector2(.22f, height);
    }
}
