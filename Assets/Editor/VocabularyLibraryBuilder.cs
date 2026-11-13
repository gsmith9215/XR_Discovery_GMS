using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using XRDiscovery.Vocabulary;

/// <summary>Regenerates resource paths and categories while retaining reviewed translations by prefab GUID.</summary>
public sealed class VocabularyLibraryBuilder : IPreprocessBuildWithReport
{
    public const string LibraryPath = "Assets/Resources/VocabularyData/Library.json";
    public const string FontPath = "Assets/Resources/VocabularyData/JapaneseFont.asset";
    private const string FallbackPath = "Assets/Resources/VocabularyData/JapaneseFallback.asset";
    private const string SourceFontPath = "Assets/Fonts/Vocabulary/NotoSansJP-Regular.otf";
    private const string BoxesRoot = "Assets/Resources/VocabularyBoxes";
    public int callbackOrder => -100;

    public void OnPreprocessBuild(BuildReport report)
    {
        RebuildLibrary();
        PremadeSetAuthoring.Validate();
        EnsureJapaneseFont();
    }

    [MenuItem("Tools/Vocabulary/Rebuild Library")]
    public static void RebuildLibrary()
    {
        var previous = File.Exists(LibraryPath)
            ? JsonUtility.FromJson<VocabularyLibraryData>(File.ReadAllText(LibraryPath))
            : new VocabularyLibraryData();
        if (previous == null || previous.version != 1 || previous.entries == null)
            throw new InvalidOperationException("Existing vocabulary metadata is invalid. It was not overwritten.");
        var byGuid = previous.entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var seeds = JapaneseSeed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|')).ToDictionary(p => p[0], p => p, StringComparer.OrdinalIgnoreCase);
        var result = new VocabularyLibraryData();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { BoxesRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string relative = path.Substring((BoxesRoot + "/").Length);
            int categoryEnd = relative.IndexOf('/');
            string category = categoryEnd >= 0 ? relative.Substring(0, categoryEnd) : "Uncategorized";
            string name = Path.GetFileNameWithoutExtension(path);
            string resourcePath = path.Substring("Assets/Resources/".Length);
            resourcePath = resourcePath.Substring(0, resourcePath.Length - ".prefab".Length);
            string japanese = string.Empty, reading = string.Empty;
            if (byGuid.TryGetValue(guid, out var reviewed))
            {
                japanese = reviewed.Japanese;
                reading = reviewed.Reading;
            }
            else if (seeds.TryGetValue(name, out var seed))
            {
                japanese = seed[1];
                reading = seed[2];
            }
            if (string.IsNullOrWhiteSpace(japanese))
                Debug.LogWarning("Vocabulary needs a Japanese term: " + path);
            result.entries.Add(new VocabularyEntry(guid, name, japanese, reading, category, resourcePath));
        }
        if (result.entries.Count == 0)
            throw new InvalidOperationException("No vocabulary prefabs were found; existing metadata was not overwritten.");
        result.entries = result.entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id).ToList();
        EnsureFolder("Assets/Resources/VocabularyData");
        string json = JsonUtility.ToJson(result, true) + "\n";
        if (!File.Exists(LibraryPath) || File.ReadAllText(LibraryPath) != json)
        {
            File.WriteAllText(LibraryPath, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(LibraryPath, ImportAssetOptions.ForceSynchronousImport);
        }
        Debug.Log($"Vocabulary library ready: {result.entries.Count} items in {result.entries.Select(e => e.Category).Distinct().Count()} categories.");
    }

    [MenuItem("Tools/Vocabulary/Rebuild Japanese Font")]
    public static void EnsureJapaneseFont()
    {
        if (TMP_Settings.instance == null)
            throw new InvalidOperationException("TextMeshPro Essentials must be available before building the vocabulary font.");
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
            throw new InvalidOperationException("The bundled Noto Sans JP source font is missing or has not imported.");
        EnsureFolder("Assets/Resources/VocabularyData");
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback == null)
        {
            fallback = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            SaveFont(fallback, FallbackPath, "Japanese custom-name fallback");
        }
        var serializedFallback = new SerializedObject(fallback);
        serializedFallback.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
        serializedFallback.ApplyModifiedPropertiesWithoutUndo();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            SaveFont(font, FontPath, "Japanese vocabulary");
        }
        var data = File.Exists(LibraryPath)
            ? JsonUtility.FromJson<VocabularyLibraryData>(File.ReadAllText(LibraryPath))
            : new VocabularyLibraryData();
        var text = new StringBuilder();
        for (int c = 32; c < 127; c++) text.Append((char)c);
        for (int c = 0x3041; c <= 0x3096; c++) text.Append((char)c);
        for (int c = 0x30A1; c <= 0x30FA; c++) text.Append((char)c);
        text.Append("ー・。、！？「」『』（）〜…—–✓×→←↑↓");
        foreach (var entry in data.entries)
            text.Append(entry.Name).Append(entry.Japanese).Append(entry.Reading).Append(entry.Category);
        string premadeText = string.Concat(PremadeSetAuthoring.FontText());
        text.Append(premadeText);
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        bool complete = font.TryAddCharacters(new string(text.ToString().Distinct().ToArray()), out string missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        var serializedMain = new SerializedObject(font);
        serializedMain.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serializedMain.ApplyModifiedPropertiesWithoutUndo();
        foreach (var atlas in font.atlasTextures)
        {
            if (atlas == null) continue;
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(atlas);
        }
        EditorUtility.SetDirty(font);
        EditorUtility.SetDirty(fallback);
        AssetDatabase.SaveAssets();
        string required = string.Concat(data.entries.Select(e => e.Japanese + e.Reading)) + premadeText;
        var missingRequired = required.Distinct().Where(c => !font.HasCharacter(c)).ToArray();
        if (missingRequired.Length > 0)
            throw new InvalidOperationException("The vocabulary font is missing required Japanese characters: " + new string(missingRequired));
        if (!complete) Debug.LogWarning("Noto Sans JP has no glyph for these optional UI characters: " + missing);
        Debug.Log($"Japanese vocabulary font ready: {font.characterTable.Count} baked characters plus dynamic custom-name fallback.");
    }

    private static void SaveFont(TMP_FontAsset font, string path, string name)
    {
        if (font == null) throw new InvalidOperationException("Could not create the Japanese font atlas.");
        font.name = name;
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var texture in font.atlasTextures)
            if (texture != null) AssetDatabase.AddObjectToAsset(texture, font);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Initial content only. Once generated, review Japanese/reading in Library.json;
    // subsequent rebuilds retain that content by GUID even when a prefab is renamed.
    private const string JapaneseSeed = @"Giant clam|シャコガイ|しゃこがい
Starfish|ヒトデ|ひとで
Cap|帽子|ぼうし
Glasses|眼鏡|めがね
Hat|帽子|ぼうし
Barrel|樽|たる
Bucket|バケツ|ばけつ
Combine harvester|コンバイン|こんばいん
Farm trailer|農業用トレーラー|のうぎょうようとれーらー
Fence|柵|さく
Gate|門|もん
Grain bag|穀物袋|こくもつぶくろ
Hay bale|干し草の束|ほしくさのたば
Pallet|パレット|ぱれっと
Sack|袋|ふくろ
Scarecrow|かかし|かかし
Seed packet|種の袋|たねのふくろ
Tractor|トラクター|とらくたー
Trough|飼い葉桶|かいばおけ
Watering can|じょうろ|じょうろ
Well|井戸|いど
Wheelbarrow|手押し車|ておしぐるま
Wind turbine|風力発電機|ふうりょくはつでんき
Windmill|風車|ふうしゃ
Apple|りんご|りんご
Apricot|あんず|あんず
Asparagus|アスパラガス|あすぱらがす
Banana|バナナ|ばなな
Beans|豆|まめ
Beet|ビーツ|びーつ
Bell pepper|パプリカ|ぱぷりか
Bread|パン|ぱん
Broccoli|ブロッコリー|ぶろっこりー
Butternut squash|バターナッツかぼちゃ|ばたーなっつかぼちゃ
Carrots|にんじん|にんじん
Cherries|さくらんぼ|さくらんぼ
Chili pepper|唐辛子|とうがらし
Coconut|ココナッツ|ここなっつ
Corn|とうもろこし|とうもろこし
Cucumber|きゅうり|きゅうり
Eggplant|なす|なす
Hamburger|ハンバーガー|はんばーがー
Lemon|レモン|れもん
Onion|玉ねぎ|たまねぎ
Orange|オレンジ|おれんじ
Peach|桃|もも
Pear|洋梨|ようなし
Plum|すもも|すもも
Potato|じゃがいも|じゃがいも
Pumpkin|かぼちゃ|かぼちゃ
Strawberry|いちご|いちご
Tomato|トマト|とまと
Watermelon|すいか|すいか
Air conditioner|エアコン|えあこん
Bed|ベッド|べっど
Bench|ベンチ|べんち
Cardboard box|段ボール箱|だんぼーるばこ
Chair|椅子|いす
Chest|宝箱|たからばこ
Clock|時計|とけい
Clothesline|物干しロープ|ものほしろーぷ
Deck chair|デッキチェア|でっきちぇあ
Door|ドア|どあ
Firewood|薪|まき
Mailbox|郵便受け|ゆうびんうけ
Mug|マグカップ|まぐかっぷ
Picnic table|ピクニックテーブル|ぴくにっくてーぶる
Picture frame|額縁|がくぶち
Pirate flag|海賊旗|かいぞくき
Plate|皿|さら
Pot|壺|つぼ
Rocking chair|ロッキングチェア|ろっきんぐちぇあ
Satellite dish|パラボラアンテナ|ぱらぼらあんてな
Security camera|防犯カメラ|ぼうはんかめら
Sofa|ソファ|そふぁ
Table|テーブル|てーぶる
Trash bag|ごみ袋|ごみぶくろ
Trash can|ごみ箱|ごみばこ
Window|窓|まど
Wooden crate|木箱|きばこ
Dead tree|枯れ木|かれき
Fern|シダ|しだ
Flowers|花|はな
Grass|草|くさ
Log|丸太|まるた
Mushroom|きのこ|きのこ
Palm tree|ヤシの木|やしのき
Pine tree|松|まつ
Root|根|ね
Sunflower|ひまわり|ひまわり
Tree stump|切り株|きりかぶ
Tree|木|き
Twig|小枝|こえだ
Cave|洞窟|どうくつ
Mountain|山|やま
Pebbles|小石|こいし
Rock|岩|いわ
Stone archway|岩のアーチ|いわのあーち
Volcano|火山|かざん
Coral|サンゴ|さんご
Driftwood|流木|りゅうぼく
Lily pads|睡蓮の葉|すいれんのは
Reeds|葦|あし
Seaweed|海藻|かいそう
Desk|机|つくえ
Keypad|キーパッド|きーぱっど
Newspaper|新聞|しんぶん
Screen|画面|がめん
Shelf|棚|たな
Astronaut|宇宙飛行士|うちゅうひこうし
Boy|男の子|おとこのこ
Farmer|農家|のうか
Girl|女の子|おんなのこ
Robot|ロボット|ろぼっと
Skeleton|骸骨|がいこつ
Skull|頭蓋骨|ずがいこつ
Statue|像|ぞう
Bottle|瓶|びん
Coin|硬貨|こうか
First aid kit|救急箱|きゅうきゅうばこ
Key|鍵|かぎ
Smartphone|スマートフォン|すまーとふぉん
Umbrella|傘|かさ
Apartment building|集合住宅|しゅうごうじゅうたく
Barn|納屋|なや
City hall|市役所|しやくしょ
Dock|桟橋|さんばし
Fire escape|非常階段|ひじょうかいだん
Garage|車庫|しゃこ
Greenhouse|温室|おんしつ
Hot dog stand|ホットドッグの屋台|ほっとどっぐのやたい
House|家|いえ
Office building|オフィスビル|おふぃすびる
Outhouse|屋外トイレ|おくがいといれ
Shack|小屋|こや
Shop|店|みせ
Silo|サイロ|さいろ
Train station|駅|えき
Water tower|給水塔|きゅうすいとう
Anvil|金床|かなとこ
Ax|斧|おの
Cable reel|ケーブルリール|けーぶるりーる
Chain|鎖|くさり
Chainsaw|チェーンソー|ちぇーんそー
Gas can|ガソリン携行缶|がそりんけいこうかん
Gas tank|燃料タンク|ねんりょうたんく
Hoe|鍬|くわ
Hook|フック|ふっく
Ladder|はしご|はしご
Lever|レバー|ればー
Pickaxe|つるはし|つるはし
Pipe|パイプ|ぱいぷ
Pitchfork|農業用フォーク|のうぎょうようふぉーく
Plank|板|いた
Push button|押しボタン|おしぼたん
Rake|熊手|くまで
Rope|ロープ|ろーぷ
Scythe|大鎌|おおがま
Shovel|シャベル|しゃべる
Toolbox|工具箱|こうぐばこ
Vise|万力|まんりき
Ambulance|救急車|きゅうきゅうしゃ
ATM|ATM|えーてぃーえむ
Billboard|看板|かんばん
Bus shelter|バス停|ばすてい
Car|車|くるま
Dumpster|大型ごみ箱|おおがたごみばこ
Fire hydrant|消火栓|しょうかせん
Oar|オール|おーる
Outrigger canoe|アウトリガーカヌー|あうとりがーかぬー
Parking meter|パーキングメーター|ぱーきんぐめーたー
Police car|パトカー|ぱとかー
Raft|いかだ|いかだ
Road barrier|道路のバリケード|どうろのばりけーど
Steering wheel|ハンドル|はんどる
Stop sign|一時停止標識|いちじていしひょうしき
Taxi|タクシー|たくしー
Tire|タイヤ|たいや
Traffic cone|三角コーン|さんかくこーん
Traffic light|信号機|しんごうき
Truck|トラック|とらっく
Van|バン|ばん";
}
