using System;
using GainGround.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit, repeatable weapon migration. Never changes existing dash parameters or GUIDs.</summary>
public static class OriginalCharactersInstaller
{
    public static readonly string[] Names = {
        "Johnny", "Athra", "Betty", "Babalu", "Gaston", "Moss", "Glow Knight", "Fire Knight",
        "Water Knight", "Zaemon", "Valkyrie", "Mamu", "Professor", "Kou", "Kiddo",
        "Mad Poppy", "General", "Robbie", "Cyber", "Honey"
    };

    static WeaponDefinition Weapon(string label, ProjectileKind kind, float range, float speed = 8, float cooldown = .32f, int damage = 1,
        WeaponDirection direction = WeaponDirection.Facing, bool lob = false)
    {
        return new WeaponDefinition {
            label = label, kind = kind, range = range, speed = speed, cooldown = cooldown, damage = damage,
            direction = direction, lobbed = lob, color = Color.yellow, maxActive = 3
        };
    }

    public static void Configure(CharacterDefinition c, string name)
    {
        var facing = WeaponDirection.Facing; var north = WeaponDirection.North;
        var normal = Weapon("Pistola", ProjectileKind.Bullet, 5.5f);
        WeaponDefinition special;
        c.speed = 3;
        switch (name)
        {
            case "Athra": case "Gaston":
                c.speed = 3.8f;
                normal = Weapon("Lanca", ProjectileKind.Spear, 3.4f, 7, .4f);
                special = Weapon("Lanca em arco", ProjectileKind.Spear, name == "Athra" ? 4 : 4.8f, 5, .65f, 1, name == "Athra" ? facing : north, true);
                break;
            case "Babalu": case "Moss":
                c.speed = name == "Moss" ? 3.7f : 2.9f;
                normal = Weapon("Flecha", ProjectileKind.Arrow, 8, 9);
                special = Weapon("Flecha em arco", ProjectileKind.Arrow, 9, 6, .6f, 1, name == "Babalu" ? facing : north, true);
                break;
            case "Johnny": case "Professor":
                c.speed = 3.2f;
                normal = Weapon("Rifle", ProjectileKind.Bullet, 10, 12, .25f);
                special = Weapon(name == "Johnny" ? "Rifle: cima" : "Rifle: esquerda + direita",
                    ProjectileKind.Bullet, 16, 12, .3f, 1, name == "Johnny" ? north : WeaponDirection.Horizontal);
                special.maxActive = 4;
                break;
            case "Kou": case "Kiddo":
                c.speed = 3.2f;
                normal = Weapon("Metralhadora", ProjectileKind.Bullet, 10, 13, .14f);
                normal.maxActive = 6; normal.radius = .18f;
                special = Weapon(name == "Kou" ? "Metralhadora: cima" : "Metralhadora: esquerda + direita",
                    ProjectileKind.Bullet, 16, 13, .16f, 1, name == "Kou" ? north : WeaponDirection.Horizontal);
                special.radius = .22f; special.maxActive = 8;
                break;
            case "Betty": case "Honey":
                special = Weapon("Granada" + (name == "Betty" ? ": cima" : ": 8 direcoes"),
                    ProjectileKind.Grenade, 4.6f, 5, .9f, 2, name == "Betty" ? north : facing, true);
                special.blastRadius = .75f; special.maxActive = 2; special.color = new Color(.5f, 1, .3f);
                break;
            case "Valkyrie": case "Mamu":
                special = Weapon("Bumerangue" + (name == "Mamu" ? ": cima" : ": 8 direcoes"),
                    ProjectileKind.Boomerang, 6, 7, .45f, 1, name == "Mamu" ? north : facing);
                special.pierces = true; special.maxActive = 2; special.color = Color.cyan;
                break;
            case "Glow Knight":
                normal = Weapon("Bomba magica", ProjectileKind.Magic, 6, 7, .4f, 2);
                special = Weapon("Luz: quatro orbes", ProjectileKind.Orbit, .85f, 1, 3, 2);
                special.duration = 2.8f; special.pierces = true; special.maxActive = 4; special.color = Color.yellow;
                break;
            case "Fire Knight":
                normal = Weapon("Bomba de fogo", ProjectileKind.Magic, 6, 7, .4f, 2);
                special = Weapon("Fogo: armadilha frontal", ProjectileKind.FireTrap, .85f, 1, 1.2f, 2, north);
                special.radius = .6f; special.duration = 2.5f; special.maxActive = 2; special.color = new Color(1, .35f, .08f);
                break;
            case "Water Knight":
                normal = Weapon("Bomba magica", ProjectileKind.Magic, 6, 7, .4f, 2);
                special = Weapon("Agua: imobiliza", ProjectileKind.WaterTrap, .9f, 1, 1.2f, 0);
                special.freezeSeconds = 3; special.duration = 2.5f; special.radius = .6f; special.maxActive = 2; special.color = Color.cyan;
                break;
            case "Zaemon":
                normal = Weapon("Bomba magica", ProjectileKind.Magic, 6, 7, .4f, 2);
                special = Weapon("Vento: tornado com ricochete", ProjectileKind.Tornado, 20, 3.4f, 2, 2);
                special.duration = 5; special.radius = .3f; special.pierces = true; special.maxActive = 1;
                special.color = new Color(.5f, 1, .65f);
                break;
            case "Mad Poppy":
                c.speed = 2;
                special = Weapon("Bazuca: cima", ProjectileKind.Rocket, 20, 9, .9f, 3, north);
                special.blastRadius = .55f; special.maxActive = 2; special.color = new Color(1, .6f, .15f);
                break;
            case "General":
                c.speed = 2;
                special = Weapon("Lanca-chamas", ProjectileKind.Flame, 2.5f, 5, .15f, 2);
                special.pierces = true; special.radius = .3f; special.maxActive = 4; special.color = new Color(1, .3f, .05f);
                break;
            case "Robbie":
                c.speed = 1.9f;
                special = Weapon("Foguete em arco", ProjectileKind.Rocket, 10, 6, .9f, 2, facing, true);
                special.blastRadius = .6f; special.maxActive = 2; special.color = new Color(.7f, .8f, 1);
                break;
            case "Cyber":
                c.speed = 1.8f;
                special = Weapon("Missil: leque de seis", ProjectileKind.Rocket, 20, 8, 1.2f, 1, WeaponDirection.SixWay);
                special.maxActive = 6; special.color = new Color(1, .5f, .3f);
                break;
            default: throw new ArgumentException("Unknown original character: " + name);
        }
        c.normalWeapon = normal; c.specialWeapon = special;
        // Keep legacy stats meaningful for Inspectors and older project tooling.
        c.shotInterval = normal.cooldown; c.projectileSpeed = normal.speed;
        c.range = normal.range; c.damage = normal.damage; c.specialPassesWalls = special.lobbed;
    }

    [MenuItem("Gain Ground/Install original 20 character weapons")]
    public static void Install()
    {
        var roster = new CharacterDefinition[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string path = CharacterAssets.Folder + "/" + Names[i].Replace(" ", "_") + ".asset";
            var c = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<CharacterDefinition>();
                c.sprite = CharacterAssets.Load(i >= 6 && i <= 9 ? "Guardiao" : i < 6 ? "Lanceira" : "Artilheira").sprite;
                c.name = c.displayName = Names[i];
                c.characterTint = Color.HSVToRGB((i * .13f) % 1, .35f, 1);
                AssetDatabase.CreateAsset(c, path);
            }
            Configure(c, Names[i]);
            EditorUtility.SetDirty(c); roster[i] = c;
        }
        const string scenePath = "Assets/GainGround/Editable/Scenes/GainGround.unity";
        EditorSceneManager.OpenScene(scenePath);
        var game = UnityEngine.Object.FindObjectOfType<GainGroundGame>();
        game.allCharacters = roster;
        EditorUtility.SetDirty(game);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed 20 original character weapon profiles. Existing dash settings preserved.");
    }
}
