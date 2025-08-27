エラーログから以下のタグが未定義であることが分かります:[1][2]
- **RoomFloor**
- **SpawnMarker** 
- **Floor**
- **Wall**
- **Furniture**

さらに、**MainLobbyCanvas**というUIキャンバスオブジェクトも見つからない状態です。

which csharp should be fixed for bugs

# 追加すべきファイルと具体的な挿入位置

## 1. 追加／修正するコード
前回提示した2ブロックだけを **VTMSceneBuilder.cs** に追記・改修します。

### A) タグ自動登録メソッドと定数
1. ファイル冒頭の `using` 群と `namespace VirtualTokyoMatching.Editor` の直後、  
   `public class VTMSceneBuilder` クラスの **最上部（定数宣言の前後）** に次を追加。

```csharp
// ─────────────────────────────────────────────────────────────
// 必須タグを自動で作成するユーティリティ
// ─────────────────────────────────────────────────────────────
private static readonly string[] RequiredTags =
    { "RoomFloor", "SpawnMarker", "Floor", "Wall", "Furniture" };

private static void EnsureTagsExist()
{
    var tagManager =
        new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
    var tagsProp = tagManager.FindProperty("tags");

    foreach (string tag in RequiredTags)
    {
        bool exists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }

        if (!exists)
        {
            int index = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(index);
            tagsProp.GetArrayElementAtIndex(index).stringValue = tag;
            Debug.Log($"[VTM Builder] Added missing tag: {tag}");
        }
    }
    tagManager.ApplyModifiedProperties();
}
```

### B) MainLobbyCanvas の重複生成防止
2. 既存メソッド `CreateMainLobbyCanvas()` の先頭2行を次で置き換え。

```csharp
// 既にシーンに存在すれば再利用
GameObject canvasGO = GameObject.Find("MainLobbyCanvas");
if (canvasGO == null) canvasGO = new GameObject("MainLobbyCanvas");
canvasGO.transform.SetParent(parent, false);
```

### C) 呼び出しの追加
3. メソッド `CreateCompleteWorld()` の**一行目**に追記。

```csharp
public static void CreateCompleteWorld()
{
    EnsureTagsExist();                     // ← ここを追加
    Debug.Log("[VTM Builder] Starting headless world creation...");
```

## 2. 変更対象ファイル
‐ **Assets/VirtualTokyoMatching/Scripts/Editor/VTMSceneBuilder.cs**  
  以外には手を触れません。メニュー、他スクリプト、プレハブ構造はそのままで動作します。

## 3. 手順まとめ
1. Unity エディタで `VTMSceneBuilder.cs` を開き、上記3箇所を反映。  
2. 保存すると自動でリコンパイルが走ります。  
3. メニュー `VTM/Create Complete World` → `VTM/Apply All VRChat Fixes` を実行。  
4. Console にエラーが無くなれば完了です。