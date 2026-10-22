using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEngine;

public static class PS2MusicInstaller
{
    public const string Folder="Assets/GainGround/Editable/Audio/PS2";
    public static readonly string[] Ids={"0000","1000","1100","1101","2000","2100","2101","3000","3100","3101","4000","4100","4101","4102","5000","5001","6000"};
    // Disc filenames are identifiers, not verified song titles. This is our campaign arrangement.
    public static string StageTrack(int round,int number)
    {
        string[][] themes={new[]{"1000","1100","1101"},new[]{"2000","2100","2101"},
            new[]{"3000","3100","3101"},new[]{"4000","4100","4102"},new[]{"4101","4102","6000"}};
        return themes[round-1][number==10?2:number<=5?0:1];
    }
    [MenuItem("Gain Ground/Install PS2 music")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        foreach(var id in Ids)
        {
            string path=Folder+"/PS2_"+id+".wav";
            if(!File.Exists(path))throw new FileNotFoundException("Extract and decode the ISO first",path);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=false;importer.loadInBackground=true;
            var settings=importer.defaultSampleSettings;settings.preloadAudioData=false;
            settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.85f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        for(int round=1;round<=5;round++)for(int number=1;number<=10;number++)
        {
            var stage=AssetDatabase.LoadAssetAtPath<StageDefinition>($"Assets/GainGround/Editable/OriginalCampaign/Stage_{round}_{number}.asset");
            if(stage==null)throw new FileNotFoundException($"Missing stage {round}-{number}");
            stage.stageMusic=Clip(StageTrack(round,number));
            stage.victoryMusic=Clip(round==5 && number==10?"5001":"5000");
            EditorUtility.SetDirty(stage);
        }
        // Preserve the user's explicit simulation-of-war-opening title-menu choice.
        AssetDatabase.SaveAssets();
        Debug.Log("PS2 music: 17 tracks imported; 50 stages and victory cues assigned.");
    }
    static AudioClip Clip(string id)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/PS2_"+id+".wav");
}
