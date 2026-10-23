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
        public StageBundleFile map;
        public StageBundleFile music;
        public StageBundleFile victoryMusic;
        public bool hasBackground;
        public bool hasTerrainAtlas;
        public EnemyVisualFiles[] enemyVisuals;
    }

    [Serializable]
    public struct EnemyVisualFiles
    {
        public StageBundleFile bundle;
        public bool hasSprite;
        public bool hasModel;
    }

    [Serializable]
    public struct CharacterPortraitFiles
    {
        public CharacterDefinition character;
        public StageBundleFile portrait;
    }
}
