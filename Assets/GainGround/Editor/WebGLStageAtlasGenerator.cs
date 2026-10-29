using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GainGround.Game;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Produces the runtime's compact raster maps and small per-world preview atlases.
// The authoring tilemaps remain untouched and are only rendered when their source hash changes.
public static class WebGLStageAtlasGenerator
{
    const string Root = "Assets/GainGround/GeneratedWebGL";
    const string MapRoot = Root + "/StageMaps";
    const string PreviewRoot = Root + "/StagePreviews";
    const int PreviewMaxSide = 384;

    public static string CreateRuntimeMap(StageDefinition stage, int index)
    {
        if (stage == null)
            throw new ArgumentNullException(nameof(stage), "A fase precisa de um layout de origem.");
        if (stage.paintedLayout == null)
        {
            var background = stage.background;
            if (background == null && stage.name == "Stage_1_7")
                background = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/GainGround/Editable/OriginalCampaign/Complete/Maps/1-07.png");
            if (background != null) return AssetDatabase.GetAssetPath(background);
            throw new InvalidOperationException("Layout pintável e mapa de origem ausentes: " + stage.name);
        }
        EnsureFolder(MapRoot);
        string path = MapRoot + "/Stage_" + (index + 1).ToString("D2") + ".png";
        string sourcePath = AssetDatabase.GetAssetPath(stage.paintedLayout.gameObject);
        string sourceHash = AssetDatabase.GetAssetDependencyHash(sourcePath) + "|" +
            stage.size.x.ToString("R") + "|" + stage.size.y.ToString("R") + "|" + stage.floorColor;
        if (stage.name == "Stage_3_4" || stage.name == "Stage_3_5")
            sourceHash += "|iso-elevation-front-faces-v2";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.userData == sourceHash) return path;

        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null) throw new InvalidOperationException("Layout pintável ausente: " + sourcePath);
            if (layout.stage == null) layout.stage = stage;
            int width, height;
            byte[] image = GainGroundStagePaintBaker.RenderMap(layout, out width, out height, false);
            File.WriteAllBytes(AbsoluteAssetPath(path), image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            ConfigureSprite(importer, width / stage.size.x, 4096);
            importer.userData = sourceHash;
            importer.SaveAndReimport();
            return path;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string CreatePreviewAtlas(StageDefinition[] stages, int world)
    {
        if (stages == null || stages.Length == 0) throw new ArgumentNullException(nameof(stages));
        EnsureFolder(PreviewRoot);
        string path = PreviewRoot + "/World_" + (world + 1).ToString("D2") + ".png";
        int first = world * 10;
        int count = Mathf.Min(10, stages.Length - first);
        var mapPaths = new string[count];
        var addresses = new string[count];
        var signature = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            int stageIndex = first + i;
            mapPaths[i] = CreateRuntimeMap(stages[stageIndex], stageIndex);
            var importer = AssetImporter.GetAtPath(mapPaths[i]);
            signature.Append(importer != null ? importer.userData : "missing").Append('|');
            addresses[i] = "stage-" + (stageIndex + 1).ToString("D2") + "-preview";
        }

        var atlasImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        string sourceHash = signature.ToString();
        if (atlasImporter != null && atlasImporter.userData == sourceHash) return path;

        var slices = new Texture2D[count];
        Texture2D atlas = null;
        try
        {
            for (int i = 0; i < count; i++) slices[i] = CreateThumbnail(mapPaths[i]);
            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] packed = atlas.PackTextures(slices, 2, 4096, false);
            var spriteRects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(Mathf.RoundToInt(packed[i].x * atlas.width),
                    Mathf.RoundToInt(packed[i].y * atlas.height),
                    Mathf.RoundToInt(packed[i].width * atlas.width),
                    Mathf.RoundToInt(packed[i].height * atlas.height));
                if (rect.width != slices[i].width || rect.height != slices[i].height)
                    throw new InvalidOperationException("O atlas de prévias excedeu 4096 pixels.");
                spriteRects[i] = new SpriteRect {
                    name = addresses[i], rect = rect,
                    spriteID = StableSpriteId(path, addresses[i]),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                };
            }

            File.WriteAllBytes(AbsoluteAssetPath(path), atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            atlasImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            ConfigureSprite(atlasImporter, 64, 4096);
            atlasImporter.spriteImportMode = SpriteImportMode.Multiple;
            atlasImporter.userData = sourceHash;
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(atlasImporter);
            if (provider == null) throw new InvalidOperationException("Provedor de sprites ausente: " + path);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(spriteRects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null)
            {
                var pairs = new SpriteNameFileIdPair[count];
                for (int i = 0; i < count; i++) pairs[i] = new SpriteNameFileIdPair(addresses[i], spriteRects[i].spriteID);
                names.SetNameFileIdPairs(pairs);
            }
            provider.Apply();
            atlasImporter.SaveAndReimport();

            var imported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) imported.Add(sprite.name);
            foreach (string address in addresses)
                if (!imported.Contains(address)) throw new InvalidOperationException("Prévia ausente no atlas: " + address);
            return path;
        }
        finally
        {
            foreach (var slice in slices) if (slice != null) UnityEngine.Object.DestroyImmediate(slice);
            if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
        }
    }

    static Texture2D CreateThumbnail(string mapPath)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(AbsoluteAssetPath(mapPath))))
                throw new InvalidOperationException("Não foi possível ler o mapa: " + mapPath);
            float scale = Mathf.Min(1f, (float)PreviewMaxSide / Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = source.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
            result.SetPixels(pixels);
            result.Apply(false);
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    static void ConfigureSprite(TextureImporter importer, float pixelsPerUnit, int maxSize)
    {
        if (importer == null) throw new InvalidOperationException("Importador de textura ausente.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string AbsoluteAssetPath(string path) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

    static GUID StableSpriteId(string path, string spriteName)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(path + "/" + spriteName));
            return new GUID(BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant());
        }
    }
}
