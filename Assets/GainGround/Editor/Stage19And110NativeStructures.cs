using System;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

// Places the ISO's stage-specific masonry on the existing editable platforms.
public static class Stage19And110NativeStructures
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";
    const string Prefix = "PS2:Native:Stage1";

    [MenuItem("Gain Ground/Atualizar construções nativas das fases 1-9 e 1-10")]
    public static void ApplyBoth()
    {
        ApplyStage19();
        ApplyStage110();
    }

    public static void ApplyStage19()
    {
        var stage = Load(9, 4);
        var objects = KeepOtherObjects(stage);
        for (int i = 0; i < stage.walls.Length; i++)
        {
            var wall = stage.walls[i];
            bool left = wall.center.x < 0;
            string model = left ? "r1s09_07" : "r1s09_08";
            // These are the PS2's stone front faces. Place them against the
            // near edge of each raised platform, below the sentries on top.
            var center = new Vector2(wall.center.x, wall.yMin + .38f);
            objects.Add(new StageObjectSpawn {
                id = Prefix + "9Platform" + i,
                prefab = Native(9, model),
                area = new Rect(center - wall.size * .5f, wall.size),
                modelScale = new Vector3(wall.width / 3.5f, 1f, 1f)
            });
        }
        Save(stage, objects);
    }

    public static void ApplyStage110()
    {
        var stage = Load(10, 2);
        var objects = KeepOtherObjects(stage);
        for (int i = 0; i < stage.walls.Length; i++)
        {
            var wall = stage.walls[i];
            string model = i == 0 ? "r1s10_01" : "r1s10_02";
            objects.Add(new StageObjectSpawn {
                id = Prefix + "10Platform" + i,
                prefab = Native(10, model),
                area = wall,
                modelScale = new Vector3(wall.width / (69f / 32f), 1.5f, 1f)
            });
        }
        Save(stage, objects);
    }

    static StageDefinition Load(int number, int expectedWalls)
    {
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_" + number + ".asset");
        if (stage == null || stage.size != new Vector2(12, 14) ||
            stage.walls == null || stage.walls.Length != expectedWalls)
            throw new InvalidOperationException("Geometria inesperada na fase 1-" + number);
        return stage;
    }

    static List<StageObjectSpawn> KeepOtherObjects(StageDefinition stage)
    {
        var objects = new List<StageObjectSpawn>(stage.objects ?? Array.Empty<StageObjectSpawn>());
        objects.RemoveAll(item => item != null && item.id != null &&
            item.id.StartsWith(Prefix, StringComparison.Ordinal));
        return objects;
    }

    static GameObject Native(int number, string model)
    {
        string path = PS2StageModelInstaller.Root + "/Stage_1_" + number.ToString("00") +
            "/" + model + "/" + model + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Modelo PS2 ausente: " + path);
        return prefab;
    }

    static void Save(StageDefinition stage, List<StageObjectSpawn> objects)
    {
        stage.objects = objects.ToArray();
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
    }
}
