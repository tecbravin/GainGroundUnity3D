using System;
using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Editor paths resolve to serialized GUID references in the player build.
public sealed class CharacterAssets : IPreprocessBuildWithReport
{
    public const string Folder = "Assets/GainGround/Editable/Characters";
    const string GeneratedWebGLFolder = "Assets/GainGround/GeneratedWebGL/";
    public int callbackOrder => 0;

    public static CharacterDefinition Load(string name)
    {
        var fileName = name.Replace(" ", "_");
        var path = Folder + "/" + fileName + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
        if (result != null) return result;
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", new[] { Folder }))
        {
            var candidate = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(candidate), fileName, StringComparison.Ordinal)) continue;
            if (result != null) throw new InvalidOperationException("Duplicate character asset: " + fileName);
            result = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(candidate);
        }
        if (result == null) throw new FileNotFoundException("Character must exist in the Characters folder.", path);
        return result;
    }

    [MenuItem("Assets/Create/Gain Ground/Character")]
    public static void CreateCharacter()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/New Character.asset");
        var character = ScriptableObject.CreateInstance<CharacterDefinition>();
        character.displayName = "New Character";
        AssetDatabase.CreateAsset(character, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = character;
        EditorGUIUtility.PingObject(character);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(GeneratedWebGLFolder, StringComparison.Ordinal)) continue;
            if (!path.StartsWith(Folder + "/", StringComparison.Ordinal))
                throw new BuildFailedException("Move this character into " + Folder + " before building: " + path);
        }
    }
}
