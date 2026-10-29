using System;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class PS2StageBorderVariant
{
    const string Art = "Assets/GainGround/Editable/OriginalCampaign/PaintedStages";

    public static GameObject Create(string stageFolder, string sourcePath, string variantName)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
            throw new InvalidOperationException("Modelo PS2 não importado: " + sourcePath);
        string folder = Art + "/" + stageFolder;
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Art, stageFolder);
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            var model = root.GetComponent<PS2CharacterModel>();
            if (model == null || model.rig == null)
                throw new InvalidOperationException("Rig da construção PS2 ausente: " + sourcePath);
            // The source art is measured in PS2 x/z ground coordinates. A steeper
            // view keeps the 320-unit floor strip inside the 14-unit Unity map.
            model.rig.localRotation = Quaternion.Euler(75f, 0f, 0f);
            root.name = variantName;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + variantName + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Falha ao salvar " + variantName);
            return prefab;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
