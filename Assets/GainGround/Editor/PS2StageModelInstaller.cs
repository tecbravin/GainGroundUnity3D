using System;
using System.IO;
using System.Linq;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class PS2StageModelInstaller
{
    [Serializable] sealed class Entry
    {
        public string stage;
        public string name;
        public string json;
        public string texture;
        public string sha256;
    }

    [Serializable] sealed class Catalog { public Entry[] entries; }
    const string SourceCatalog = "Analysis/PS2StageModels/import.json";
    public const string Root = "Assets/GainGround/Editable/OriginalCampaign/Objects/Models3D";

    [MenuItem("Gain Ground/Link PS2 stage model palettes")]
    public static void LinkPalettes()
    {
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(SourceCatalog));
        if (catalog == null || catalog.entries == null) throw new InvalidDataException(SourceCatalog);
        LinkPalettes(catalog.entries);
        AssetDatabase.SaveAssets();
        Debug.Log("Linked PS2 stage model prefabs to the editable stage palettes.");
    }

    [MenuItem("Gain Ground/Import all PS2 stage model prefabs")]
    public static void ImportAll()
    {
        if (!File.Exists(SourceCatalog))
            throw new FileNotFoundException("Run python Tools/extract_ps2_stage_models.py first", SourceCatalog);
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(SourceCatalog));
        if (catalog == null || catalog.entries == null || catalog.entries.Length == 0)
            throw new InvalidDataException("No decoded PS2 stage models in " + SourceCatalog);
        EnsureFolder(Root);
        int imported = 0;
        try
        {
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                var entry = catalog.entries[i];
                EditorUtility.DisplayProgressBar("Importing PS2 stage objects", entry.name,
                    (float)i / catalog.entries.Length);
                var material = MaterialFor(entry);
                string stageFolder = Root + "/Stage_" + entry.stage.Replace('-', '_');
                EnsureFolder(stageFolder);
                string folder = stageFolder + "/" + entry.name;
                EnsureFolder(folder);
                PS2CharacterInstaller.InstallSource(entry.name, entry.json, null, folder, false,
                    material, 1f / 32f, true, false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + entry.name + ".prefab");
                var model = prefab != null ? prefab.GetComponent<PS2CharacterModel>() : null;
                if (model == null || model.renderers == null || model.renderers.Length == 0)
                    throw new InvalidDataException("Invalid imported PS2 stage model: " + entry.name);
                imported++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        LinkPalettes(catalog.entries);
        AssetDatabase.SaveAssets();
        Debug.Log($"Imported {imported} PS2 stage model prefabs into {Root}.");
    }

    static void LinkPalettes(Entry[] entries)
    {
        foreach (var group in entries.GroupBy(item => item.stage))
        {
            var parts = group.Key.Split('-');
            string asset = "Assets/GainGround/Editor/StageTexturePalettes/Stage_" +
                int.Parse(parts[0]) + "_" + int.Parse(parts[1]) + ".asset";
            var palette = AssetDatabase.LoadAssetAtPath<StageTexturePalette>(asset);
            if (palette == null) continue;
            var links = (palette.objects ?? Array.Empty<StageObjectTextureLink>())
                .Where(item => item != null && (item.id == null ||
                    !item.id.StartsWith("PS2Model:", StringComparison.Ordinal))).ToList();
            foreach (var entry in group)
            {
                string path = Root + "/Stage_" + entry.stage.Replace('-', '_') + "/" +
                    entry.name + "/" + entry.name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new FileNotFoundException("Imported prefab missing", path);
                links.Add(new StageObjectTextureLink { id = "PS2Model:" + entry.name, prefab = prefab });
            }
            palette.objects = links.ToArray();
            EditorUtility.SetDirty(palette);
        }
    }

    static Material MaterialFor(Entry entry)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.texture);
        if (texture == null) throw new FileNotFoundException("Stage texture missing", entry.texture);
        string stageFolder = Root + "/Stage_" + entry.stage.Replace('-', '_');
        EnsureFolder(stageFolder);
        string role = Path.GetFileNameWithoutExtension(entry.texture);
        string path = stageFolder + "/" + role + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("GainGround/PS2Character");
            if (shader == null) throw new InvalidDataException("PS2 character shader missing");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.mainTexture != texture)
        {
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
