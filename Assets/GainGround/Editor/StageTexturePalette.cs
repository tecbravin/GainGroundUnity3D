using System;
using System.Diagnostics;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public sealed class StageObjectTextureLink
{
    public string id;
    public GameObject prefab;
}

/// <summary>Editable source art. The runtime uses only the rebuilt stage PNG and placed prefabs.</summary>
public sealed class StageTexturePalette : ScriptableObject
{
    public StageDefinition stage;
    [Header("Map sources from the PS2 ISO")]
    public Texture2D floor;
    public Texture2D elevations;
    [Tooltip("Original construction atlas. Crop or replace sprites on the linked prefabs to change placed objects.")]
    public Texture2D constructions;
    public Texture2D exitBanner;
    [Header("Placed objects and constructions")]
    public StageObjectTextureLink[] objects = Array.Empty<StageObjectTextureLink>();
}

[CustomEditor(typeof(StageTexturePalette))]
public sealed class StageTexturePaletteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var palette = (StageTexturePalette)target;
        EditorGUILayout.HelpBox("Troque os atlas acima e clique em Recompor mapa. Para objetos e construções, abra o prefab vinculado e edite o SpriteRenderer ou o material do modelo 3D.", MessageType.Info);
        using (new EditorGUI.DisabledScope(palette.stage == null || palette.floor == null ||
                                           palette.elevations == null || palette.constructions == null ||
                                           palette.exitBanner == null))
            if (GUILayout.Button("Recompor mapa deste estágio")) Rebuild(palette);
        if (palette.objects == null) return;
        foreach (var item in palette.objects)
        {
            if (item == null || item.prefab == null) continue;
            var renderer = item.prefab.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(item.id + " / Sprite", renderer.sprite, typeof(Sprite), false);
                continue;
            }
            var model = item.prefab.GetComponent<PS2CharacterModel>();
            if (model != null && model.data != null && model.data.material != null)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(item.id + " / Material", model.data.material,
                        typeof(Material), false);
        }
    }

    internal static void Rebuild(StageTexturePalette palette)
    {
        if (palette == null || palette.stage == null) return;
        string stagePath = AssetDatabase.GetAssetPath(palette.stage);
        string name = Path.GetFileNameWithoutExtension(stagePath);
        var match = System.Text.RegularExpressions.Regex.Match(name, @"^Stage_(\d+)_(\d+)$");
        if (!match.Success) throw new InvalidDataException("Nome de estágio inesperado: " + name);
        int round = int.Parse(match.Groups[1].Value);
        int number = int.Parse(match.Groups[2].Value);
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string script = Path.Combine(project, "Tools", "rebuild_editable_stage_map.py");
        string mapPath = $"Assets/GainGround/Editable/OriginalCampaign/Complete/Maps/{round}-{number:00}.png";
        var paths = new[] { script, AssetDatabase.GetAssetPath(palette.floor),
            AssetDatabase.GetAssetPath(palette.elevations),
            AssetDatabase.GetAssetPath(palette.constructions),
            AssetDatabase.GetAssetPath(palette.exitBanner) };
        foreach (string path in paths)
            if (string.IsNullOrEmpty(path) || !File.Exists(Path.IsPathRooted(path) ? path : Path.Combine(project, path)))
                throw new FileNotFoundException("Textura editável ausente", path);
        var process = new ProcessStartInfo {
            FileName = "python", WorkingDirectory = project, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            Arguments = $"{Quote(script)} --round {round} --stage {number} " +
                $"--floor {Quote(paths[1])} --elevations {Quote(paths[2])} " +
                $"--constructions {Quote(paths[3])} --exit {Quote(paths[4])}"
        };
        using (var child = Process.Start(process))
        {
            if (child == null) throw new IOException("Não foi possível iniciar o Python.");
            string output = child.StandardOutput.ReadToEnd();
            string error = child.StandardError.ReadToEnd();
            child.WaitForExit();
            if (child.ExitCode != 0) throw new IOException("Falha ao recompor " + name + ": " + error);
            Debug.Log(output.Trim());
        }
        // The rebuild also refreshes the source tiles used by the painted
        // layout prefab. Import them before the stage is previewed or played.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
    }

    static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}

[CustomEditor(typeof(StageDefinition))]
public sealed class StageTextureStageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var stage = (StageDefinition)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Abrir editor de estágios")) GainGroundStagePainter.OpenFor(stage);
        string path = "Assets/GainGround/Editor/StageTexturePalettes/" + stage.name + ".asset";
        var palette = AssetDatabase.LoadAssetAtPath<StageTexturePalette>(path);
        if (palette == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Texturas editáveis", EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Paleta do estágio", palette, typeof(StageTexturePalette), false);
        if (GUILayout.Button("Abrir texturas de chão, elevações e construções"))
            Selection.activeObject = palette;
        if (GUILayout.Button("Recompor PNG do mapa")) StageTexturePaletteEditor.Rebuild(palette);
    }
}
