using System;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

// Places the two long PS2 cliff strips at the edges, outside the central play lane.
public static class Stage15NativeBorders
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Atualizar bordas nativas da fase 1-5")]
    public static void Apply()
    {
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_5.asset");
        if (stage == null || stage.size != new Vector2(12, 14))
            throw new InvalidOperationException("Geometria da fase 1-5 inesperada.");
        var objects = new List<StageObjectSpawn>(stage.objects ?? Array.Empty<StageObjectSpawn>());
        objects.RemoveAll(item => item != null && item.id != null &&
            item.id.StartsWith("PS2:Native:Stage15Border", StringComparison.Ordinal));
        Add("Left", "b1", new Vector2(-4.62f, .7f));
        Add("Right", "b2", new Vector2(4.62f, .7f));
        stage.objects = objects.ToArray();
        // A narrow collision line follows the inner lip of each cliff. The
        // lower opening lets the side-spawning enemies join the central lane.
        stage.walls = new[] {
            new Rect(-4.3f, -7f, .2f, 5.3f),
            new Rect(-4.3f, 1.3f, .2f, 5.7f),
            new Rect(4.1f, -7f, .2f, 5.3f),
            new Rect(4.1f, 1.3f, .2f, 5.7f)
        };
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();

        void Add(string id, string suffix, Vector2 center)
        {
            string model = "r1s05_" + suffix;
            string path = PS2StageModelInstaller.Root + "/Stage_1_05/" + model + "/" + model + ".prefab";
            var prefab = PS2StageBorderVariant.Create("Stage_1_5_Objects", path,
                "Stage15Border" + id);
            objects.Add(new StageObjectSpawn { id = "PS2:Native:Stage15Border" + id,
                prefab = prefab, area = new Rect(center - new Vector2(1.47f, 7f),
                    new Vector2(2.94f, 14f)), modelScale = new Vector3(12f / 13f, 1.45f, 1f) });
        }
    }
}
