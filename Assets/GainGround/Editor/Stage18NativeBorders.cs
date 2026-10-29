using System;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class Stage18NativeBorders
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Atualizar bordas nativas da fase 1-8")]
    public static void Apply()
    {
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_8.asset");
        if (stage == null || stage.size != new Vector2(12, 14))
            throw new InvalidOperationException("Geometria da fase 1-8 inesperada.");
        var objects = new List<StageObjectSpawn>(stage.objects ?? Array.Empty<StageObjectSpawn>());
        objects.RemoveAll(item => item != null && item.id != null &&
            item.id.StartsWith("PS2:Native:Stage18Border", StringComparison.Ordinal));
        Add("Left", "01", new Vector2(-4.62f, .4f));
        Add("Right", "02", new Vector2(4.62f, .4f));
        stage.objects = objects.ToArray();
        // Side enemies spawn at x +/-5.5, y -1.5 and must enter the corridor.
        // Split the stone-bank collision around that opening.
        stage.walls = new[] {
            new Rect(-6f, -7f, 2.7f, 4.5f),
            new Rect(-6f, -.5f, 2.7f, 7.5f),
            new Rect(3.3f, -7f, 2.7f, 4.5f),
            new Rect(3.3f, -.5f, 2.7f, 7.5f)
        };
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();

        void Add(string id, string suffix, Vector2 center)
        {
            string model = "r1s08_" + suffix;
            string path = PS2StageModelInstaller.Root + "/Stage_1_08/" + model + "/" + model + ".prefab";
            var prefab = PS2StageBorderVariant.Create("Stage_1_8_Objects", path,
                "Stage18Border" + id);
            objects.Add(new StageObjectSpawn { id = "PS2:Native:Stage18Border" + id,
                prefab = prefab, area = new Rect(center - new Vector2(1.38f, 7f),
                    new Vector2(2.76f, 14f)), modelScale = new Vector3(12f / 13f, 1.45f, 1f) });
        }
    }
}
