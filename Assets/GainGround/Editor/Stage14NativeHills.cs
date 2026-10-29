using System;
using System.Collections.Generic;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

// Keeps the PS2 slopes while removing their flat caps, which otherwise hide the painted floor.
public static class Stage14NativeHills
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";
    const string Art = Root + "/PaintedStages/Stage_1_4_Objects";

    [MenuItem("Gain Ground/Atualizar encostas nativas da fase 1-4")]
    public static void Apply()
    {
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Stage_1_4.asset");
        if (stage == null || stage.walls == null || stage.walls.Length != 2)
            throw new InvalidOperationException("As duas elevações da fase 1-4 não foram encontradas.");
        if (!AssetDatabase.IsValidFolder(Art))
            AssetDatabase.CreateFolder(Root + "/PaintedStages", "Stage_1_4_Objects");

        var left = SlopePrefab("05", "NativeHillLeftSides");
        var right = SlopePrefab("06", "NativeHillRightSides");
        var objects = new List<StageObjectSpawn>(stage.objects ?? Array.Empty<StageObjectSpawn>());
        objects.RemoveAll(item => item != null && item.id != null &&
            item.id.StartsWith("PS2:Native:Stage14Hill", StringComparison.Ordinal));
        objects.Add(new StageObjectSpawn { id = "PS2:Native:Stage14HillLeft", prefab = left,
            area = stage.walls[0], modelScale = new Vector3(1.5f, 1.15f, 1f) });
        objects.Add(new StageObjectSpawn { id = "PS2:Native:Stage14HillRight", prefab = right,
            area = stage.walls[1], modelScale = new Vector3(1.4f, 1.15f, 1f) });
        stage.objects = objects.ToArray();
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
    }

    static GameObject SlopePrefab(string suffix, string name)
    {
        string sourceName = "r1s04_" + suffix;
        string sourcePath = PS2StageModelInstaller.Root + "/Stage_1_04/" + sourceName +
            "/" + sourceName + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
            throw new InvalidOperationException("Modelo PS2 não importado: " + sourcePath);
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            var filter = root.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("Modelo PS2 sem malha: " + sourcePath);
            var source = filter.sharedMesh;
            string meshPath = Art + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else EditorUtility.CopySerialized(source, mesh);
            mesh.name = name;
            var vertices = source.vertices;
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var topology = source.GetTopology(submesh);
                var indices = source.GetIndices(submesh);
                int stride = topology == MeshTopology.Triangles ? 3 :
                    topology == MeshTopology.Lines ? 2 : 1;
                var kept = new List<int>(indices.Length);
                for (int i = 0; i + stride <= indices.Length; i += stride)
                {
                    bool flatCap = true;
                    for (int j = 0; j < stride; j++)
                        flatCap &= Mathf.Abs(vertices[indices[i + j]].y) < .001f;
                    if (flatCap) continue;
                    for (int j = 0; j < stride; j++) kept.Add(indices[i + j]);
                }
                mesh.SetIndices(kept.ToArray(), topology, submesh);
            }
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            filter.sharedMesh = mesh;
            root.name = name;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Art + "/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Não foi possível salvar " + name);
            return prefab;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
