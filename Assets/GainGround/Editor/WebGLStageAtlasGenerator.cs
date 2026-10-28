using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GainGround.Game;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Build-only copies. The editable prefab and its tile PNGs remain the source of truth.
public static class WebGLStageAtlasGenerator
{
    const string Root = "Assets/GainGround/GeneratedWebGL/StageAtlases";
    const string PreviewRoot = "Assets/GainGround/GeneratedWebGL/StagePreviews";

    public static string CreatePreview(StageDefinition stage, int index)
    {
        if (stage == null || stage.background == null) throw new ArgumentNullException(nameof(stage));
        EnsureFolder(PreviewRoot);
        string sourcePath = AssetDatabase.GetAssetPath(stage.background.texture);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D preview = null;
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(AbsoluteAssetPath(sourcePath))))
                throw new InvalidOperationException("Não foi possível ler a prévia: " + sourcePath);
            var rect = stage.background.rect;
            int sourceWidth = Mathf.RoundToInt(rect.width), sourceHeight = Mathf.RoundToInt(rect.height);
            float scale = Mathf.Min(1f, 512f / Mathf.Max(sourceWidth, sourceHeight));
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
            preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (rect.x + (x + .5f) * sourceWidth / width) / source.width;
                    float v = (rect.y + (y + .5f) * sourceHeight / height) / source.height;
                    pixels[y * width + x] = source.GetPixelBilinear(u, v);
                }
            preview.SetPixels(pixels);
            preview.Apply(false);
            string path = PreviewRoot + "/Stage_" + (index + 1).ToString("D2") + ".png";
            File.WriteAllBytes(AbsoluteAssetPath(path), preview.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Importador da prévia ausente: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = width / stage.size.x;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return path;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
        }
    }

    [MenuItem("Gain Ground/Validate generated WebGL atlas (stage 1-1)")]
    public static void ValidateSample()
    {
        const string stagePath = "Assets/GainGround/Editable/OriginalCampaign/Stage_1_1.asset";
        var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
        if (stage == null) throw new InvalidOperationException("Estágio 1-1 ausente.");
        string result = Create(stage, 0);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result);
        var layout = prefab != null ? prefab.GetComponent<StagePaintLayout>() : null;
        if (layout == null || layout.ground == null || layout.ground.GetUsedTilesCount() == 0)
            throw new InvalidOperationException("Atlas gerado sem tiles no layout.");
        foreach (string dependency in AssetDatabase.GetDependencies(result, true))
            if (dependency.StartsWith("Assets/GainGround/Editable/OriginalCampaign/PaintedStages/Tiles/", StringComparison.Ordinal) ||
                dependency == stagePath)
                throw new InvalidOperationException("Layout WebGL ainda depende do asset editável: " + dependency);
        Debug.Log("Atlas WebGL 1-1 validado: " + result);
    }

    public static string Create(StageDefinition stage, int index)
    {
        if (stage == null || stage.paintedLayout == null) throw new ArgumentNullException(nameof(stage));
        EnsureFolder(Root);
        string folder = Root + "/Stage_" + (index + 1).ToString("D2");
        EnsureFolder(folder);
        string path = folder + "/Layout.prefab";
        string sourcePath = AssetDatabase.GetAssetPath(stage.paintedLayout.gameObject);
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            var layout = root.GetComponent<StagePaintLayout>();
            if (layout == null) throw new InvalidOperationException("Layout pintável ausente: " + sourcePath);
            foreach (var marker in root.GetComponentsInChildren<StagePaintMarker>(true))
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            if (layout.collision != null)
            {
                UnityEngine.Object.DestroyImmediate(layout.collision.gameObject);
                layout.collision = null;
            }
            // The runtime layout must not pull the authoring StageDefinition and its assets into the bundle.
            layout.stage = null;
            layout.sourceBackground = null;
            layout.originalWalls = Array.Empty<Rect>();
            layout.lastBakedWalls = Array.Empty<Rect>();
            layout.managedObjectIds = Array.Empty<string>();

            var tilemaps = new[] { layout.ground, layout.elevations, layout.decorations, layout.effects };
            var tiles = new List<Tile>();
            var seenTiles = new HashSet<Tile>();
            var sprites = new List<Sprite>();
            var spriteIndices = new Dictionary<Sprite, int>();
            foreach (var map in tilemaps)
            {
                if (map == null) continue;
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var tileBase = map.GetTile(cell);
                    if (tileBase == null) continue;
                    var tile = tileBase as Tile;
                    if (tile == null)
                        throw new InvalidOperationException(stage.name + " usa um TileBase não suportado: " + tileBase.name);
                    if (seenTiles.Add(tile)) tiles.Add(tile);
                    if (tile.sprite != null && !spriteIndices.ContainsKey(tile.sprite))
                    { spriteIndices.Add(tile.sprite, sprites.Count); sprites.Add(tile.sprite); }
                }
            }

            var atlasSprites = PackSprites(sprites, folder);
            var copies = new Dictionary<Tile, Tile>();
            int tileNumber = 0;
            foreach (var source in tiles)
            {
                var copy = UnityEngine.Object.Instantiate(source);
                copy.name = "Tile_" + tileNumber.ToString("D3");
                if (source.sprite != null) copy.sprite = atlasSprites[spriteIndices[source.sprite]];
                AssetDatabase.CreateAsset(copy, folder + "/" + copy.name + ".asset");
                copies.Add(source, copy);
                tileNumber++;
            }
            foreach (var map in tilemaps)
            {
                if (map == null) continue;
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(cell) as Tile;
                    if (tile != null) map.SetTile(cell, copies[tile]);
                }
                map.CompressBounds();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            Debug.Log(stage.name + ": atlas WebGL com " + sprites.Count + " sprites e " + tiles.Count + " tiles.");
            return path;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Sprite[] PackSprites(List<Sprite> originals, string folder)
    {
        if (originals.Count == 0) return Array.Empty<Sprite>();
        var slices = new Texture2D[originals.Count];
        Texture2D atlas = null;
        try
        {
            float ppu = originals[0].pixelsPerUnit;
            for (int i = 0; i < originals.Count; i++)
            {
                var sprite = originals[i];
                if (Mathf.Abs(sprite.pixelsPerUnit - ppu) > .01f)
                    throw new InvalidOperationException("Tiles com pixels por unidade diferentes no mesmo estágio: " + sprite.name);
                string path = AssetDatabase.GetAssetPath(sprite.texture);
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                    throw new InvalidOperationException("Sprite de tile precisa vir de PNG/JPG: " + path);
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(source, File.ReadAllBytes(AbsoluteAssetPath(path))))
                        throw new InvalidOperationException("Não foi possível ler o sprite: " + path);
                    var rect = sprite.rect;
                    int x = Mathf.RoundToInt(rect.x), y = Mathf.RoundToInt(rect.y);
                    int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                    var slice = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    slice.SetPixels(source.GetPixels(x, y, width, height));
                    slice.Apply(false);
                    slices[i] = slice;
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] packed = atlas.PackTextures(slices, 2, 4096, false);
            var spriteRects = new SpriteRect[originals.Count];
            for (int i = 0; i < originals.Count; i++)
            {
                var rect = new Rect(Mathf.RoundToInt(packed[i].x * atlas.width),
                    Mathf.RoundToInt(packed[i].y * atlas.height),
                    Mathf.RoundToInt(packed[i].width * atlas.width),
                    Mathf.RoundToInt(packed[i].height * atlas.height));
                if (rect.width != slices[i].width || rect.height != slices[i].height)
                    throw new InvalidOperationException("Atlas excede 4096 pixels; não reduza os tiles automaticamente.");
                var original = originals[i];
                spriteRects[i] = new SpriteRect {
                    name = "Sprite_" + i.ToString("D3"), rect = rect,
                    spriteID = StableSpriteId(folder, i),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(original.pivot.x / original.rect.width,
                        original.pivot.y / original.rect.height),
                    border = original.border
                };
            }
            string atlasPath = folder + "/Atlas.png";
            File.WriteAllBytes(AbsoluteAssetPath(atlasPath), atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Importador do atlas ausente: " + atlasPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Provedor de sprites ausente: " + atlasPath);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(spriteRects);
            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
            {
                var pairs = new SpriteNameFileIdPair[spriteRects.Length];
                for (int i = 0; i < pairs.Length; i++)
                    pairs[i] = new SpriteNameFileIdPair(spriteRects[i].name, spriteRects[i].spriteID);
                nameProvider.SetNameFileIdPairs(pairs);
            }
            provider.Apply();
            importer.SaveAndReimport();
            var byName = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(atlasPath))
                if (asset is Sprite sprite) byName.Add(sprite.name, sprite);
            var result = new Sprite[originals.Count];
            for (int i = 0; i < result.Length; i++)
                if (!byName.TryGetValue(spriteRects[i].name, out result[i]))
                    throw new InvalidOperationException("Sprite ausente no atlas: " + spriteRects[i].name);
            return result;
        }
        finally
        {
            foreach (var slice in slices) if (slice != null) UnityEngine.Object.DestroyImmediate(slice);
            if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
        }
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

    static GUID StableSpriteId(string folder, int index)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(folder + "/Sprite_" + index.ToString("D3")));
            return new GUID(BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant());
        }
    }
}
