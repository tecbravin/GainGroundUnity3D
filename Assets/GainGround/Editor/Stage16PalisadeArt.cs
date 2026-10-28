using System;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class Stage16PalisadeArt
{
    const string Root = "Assets/GainGround/Editable/OriginalCampaign";

    [MenuItem("Gain Ground/Atualizar arte da paliçada 1-6")]
    public static void Apply()
    {
        SetSimpleSprite(Root + "/Objects/Prefabs/WoodWallBoards.prefab");
        var cleanCap = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Objects/Sprites/WoodWallCapClean.png");
        if (cleanCap == null) throw new InvalidOperationException("Sprite sem meias toras ausente.");
        SetSimpleSprite(Root + "/Objects/Prefabs/WoodWallCap.prefab", cleanCap);
        SetSimpleSprite(Root + "/Objects/Prefabs/WoodenGate.prefab", cleanCap);
        SetSortingOrder(Root + "/Objects/Prefabs/WoodWallBoards.prefab", 0);
        SetSortingOrder(Root + "/Objects/Prefabs/WoodWallCap.prefab", 1);
        SetSortingOrder(Root + "/Objects/Prefabs/WoodenGate.prefab", 2);
        AlignGateJoints(Root + "/Objects/Prefabs/WoodenGate.prefab");

        string layoutPath = Root + "/PaintedStages/Stage_1_6_Layout.prefab";
        var root = PrefabUtility.LoadPrefabContents(layoutPath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null || layout.elevations == null)
                throw new InvalidOperationException("Layout da fase 1-6 sem camada de elevações.");
            // The editable wall objects now provide the log artwork. The former
            // dark elevation tiles must not show through their transparent edges.
            layout.elevations.ClearAllTiles();
            PrefabUtility.SaveAsPrefabAsset(root, layoutPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        AssetDatabase.SaveAssets();
        Debug.Log("Arte da paliçada 1-6 aplicada aos muros e ao portão.");
    }

    static void SetSimpleSprite(string path, Sprite replacement = null)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var sprite = root.GetComponent<SpriteRenderer>();
            if (sprite == null || sprite.sprite == null)
                throw new InvalidOperationException("Sprite da paliçada ausente: " + path);
            if (replacement != null) sprite.sprite = replacement;
            sprite.drawMode = SpriteDrawMode.Simple;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void SetSortingOrder(string path, int order)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var sprite = root.GetComponent<SpriteRenderer>();
            if (sprite == null) throw new InvalidOperationException("Sprite da paliçada ausente: " + path);
            sprite.sortingOrder = order;
            foreach (var child in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (child != sprite) child.sortingOrder = order + 1;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void AlignGateJoints(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int i = 0; i < 2; i++)
            {
                var child = root.transform.Find(i == 0 ? "Left wooden brace" : "Right wooden brace");
                if (child == null) throw new InvalidOperationException("Reforço do portão ausente: " + path);
                child.localPosition = new Vector3(i == 0 ? -8.25f : 8.25f, 0, 0);
                child.localRotation = Quaternion.identity;
                child.localScale = new Vector3(.27f, .335f, 1);
                var sprite = child.GetComponent<SpriteRenderer>();
                if (sprite != null) sprite.color = Color.white;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
