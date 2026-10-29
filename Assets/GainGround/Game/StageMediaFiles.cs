using System;

namespace GainGround.Game
{
    [Serializable]
    public struct StageBundleFile
    {
        public string fileName;
        public string hash;
        public bool IsAssigned => !string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(hash);
    }

    [Serializable]
    public struct StageMediaFiles
    {
        public StageBundleFile preview;
        public string previewAddress;
        public StageBundleFile map;
        public string mapAddress;
        public StageBundleFile music;
        public StageBundleFile victoryMusic;
        public StageBundleFile objects;
        public bool hasBackground;
        public bool hasPaintedLayout;
        public bool hasTerrainAtlas;
        public EnemyVisualFiles[] enemyVisuals;
        public string[] objectAddresses;
        public string[] hazardAddresses;
    }

    [Serializable]
    public struct EnemyVisualFiles
    {
        public StageBundleFile bundle;
        public StageBundleFile modelBundle;
        public string modelAddress;
        public bool hasSprite;
        public bool hasModel;
    }

    [Serializable]
    public struct CharacterPortraitFiles
    {
        public CharacterDefinition character;
        public StageBundleFile portrait;
        public string portraitAddress;
    }
}
