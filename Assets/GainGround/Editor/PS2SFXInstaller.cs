using System.IO;
using GainGround.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PS2SFXInstaller
{
    const string Root="Assets/GainGround/Editable/Audio/PS2SFX";
    [MenuItem("Gain Ground/Install PS2 sound effects")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        foreach(var file in Directory.GetFiles(Root,"*.wav"))
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
            settings.preloadAudioData=true;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.SaveAndReimport();
        }
        foreach(var name in OriginalCharactersInstaller.Names)
        {
            var hero=CharacterAssets.Load(name);
            Assign(hero.normalWeapon);Assign(hero.specialWeapon);
            if(hero.normalAttackSound==null) hero.normalAttackSound=Clip(CharacterSound(name,false,hero.normalWeapon));
            if(hero.specialAttackSound==null) hero.specialAttackSound=Clip(CharacterSound(name,true,hero.specialWeapon));
            EditorUtility.SetDirty(hero);
        }
        EditorSceneManager.OpenScene("Assets/GainGround/Editable/Scenes/GainGround.unity");
        var game=Object.FindObjectOfType<GainGroundGame>();
        game.shotSound=Clip("SE_01");game.hitSound=Clip("SE_20");game.rescueSound=Clip("SE_64");
        game.dashSound=Clip("SYU");game.impactSound=Clip("SE_02");game.explosionSound=Clip("SE_29");
        game.switchSound=Clip("SE_67");
        game.scoreCountSound=Clip("SE_67");
        game.secretUnlockSound=Clip("SE_60");
        game.titlePressSound=Clip("SE_64");
        EditorUtility.SetDirty(game);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        Debug.Log("PS2 SFX installed: 67 imported clips, weapon and gameplay events connected.");
    }
    static void Assign(WeaponDefinition weapon)
    {
        if(weapon==null || weapon.fireSound!=null)return;
        string id;
        switch(weapon.kind)
        {
            case ProjectileKind.Spear:case ProjectileKind.Arrow: id="SE_03";break;
            case ProjectileKind.Grenade:id="SE_05";break;
            case ProjectileKind.Rocket:id="SE_06";break;
            case ProjectileKind.Boomerang:id="SYU";break;
            case ProjectileKind.Flame:case ProjectileKind.FireTrap:id="SE_08";break;
            case ProjectileKind.Magic:case ProjectileKind.Orbit:id="SE_09";break;
            case ProjectileKind.WaterTrap:id="SE_11";break;
            case ProjectileKind.Tornado:id="SE_12";break;
            default:id="SE_01";break;
        }
        weapon.fireSound=Clip(id);
    }
    static string CharacterSound(string name,bool special,WeaponDefinition weapon)
    {
        switch(name)
        {
            case "Athra":case "Gaston":case "Babalu":return "SE_03";
            case "Moss":return special && weapon.kind==ProjectileKind.Boomerang ? "SYU" : "SE_03";
            case "Betty":case "Honey":return special ? "SE_05" : "SE_01";
            case "Johnny":case "Professor":case "Kou":case "Kiddo":return "SE_01";
            case "Glow Knight":return "SE_09";
            case "Fire Knight":return special ? "SE_08" : "SE_09";
            case "Water Knight":return special ? "SE_11" : "SE_09";
            case "Zaemon":return special ? "SE_12" : "SE_09";
            case "Valkyrie":case "Mamu":return special ? "SYU" : "SE_01";
            case "Mad Poppy":case "Cyber":return special ? "SE_06" : "SE_01";
            case "Robbie":return special || weapon.kind==ProjectileKind.Rocket ? "SE_06" : "SE_01";
            case "General":return special ? "SE_08" : "SE_01";
            default:throw new InvalidDataException("Unknown original character sound profile: "+name);
        }
    }
    static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/"+name+".wav");
}
