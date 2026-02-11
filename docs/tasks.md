結論: ビルド失敗の主因は、複数の UdonSharp スクリプトで「Program asset が無効」＝Udon C# Program Asset の関連付け欠落・破損が多数発生しており、あわせて VRChat の Layers と Collision Matrix の未設定が残っているためです。 対処は、Udon C# Program Asset の再生成・再関連付けをすべて修復したうえで、SDK パネルからレイヤーとコリジョンマトリクスをセットし、再ビルドします。[1][2][3][4]

## 主なエラー
- 「[UdonSharp] Program asset on ... is not valid, make sure the U# script has a program asset associated with it.」が多数発生し、特に PublicProfilePublisher が 30 個、その他は VTMController 配下の各コンポーネントで 1 件ずつ検出されています。[1]
- Post Build Fixup 中に UdonSharpEditorManager.SanitizeProxyBehaviours で NullReferenceException が発生しており、未修復の Udon プログラム資産や壊れた参照が原因で後工程が継続できていません。[1]
- SDK のバリデーションで「You must address Layers and Collision Matrix issues before you can build.」と明示され、VRChat 用レイヤーとコリジョンマトリクスの初期化未完了がブロックになっています。[4][1]

## 原因の整理
- UdonSharp の「Program asset is not valid」系は、対応する Udon C# Program Asset が未割り当て・空・破損などで起き、_UdonProgramSources 内の壊れた資産やソース未設定の資産が原因になり得ます。[2][3][1]
- UdonSharp 1.x では「Program assets must point to a script and may not be empty」などの破壊的変更があり、動作要件未満の資産はコンパイルやビルドでエラーになります。[3]
- VRChat SDK はビルド前に「Setup Layers for VRChat」と「Set Collision Matrix」でプロジェクト設定を適用する必要があり、未適用だとバリデーションで停止します。[4][1]

## 解決手順
- 1) Udon C# Program Asset の修復  
  - 影響している UdonSharpBehaviour の Inspector で「Program Source」を開き、対応する Udon C# Program Asset に正しいスクリプトが割り当たっているか確認し、未割り当ての場合は割り当てるか、_UdonProgramSources 内の壊れた資産を削除して再生成します。[2][3][1]
  - UdonSharp メニューから必要に応じて「Force Upgrade」などを実行し、資産のアップグレード・再生成を促進します（1.x 移行時の既知手順）。[3]
  - 名前要件や空資産禁止などの 1.x 仕様（U# ビヘイビア名と .cs ファイル名一致、Program Asset が空でない等）を満たしているか確認します。[3]

- 2) 壊れた参照のクリーンアップ  
  - Program Source の「Source Script」が None の資産を特定して削除、あるいは正しい U# スクリプトを割り当て、欠落資産を無くします（_UdonProgramSources に集約されています）。[2]
  - 修復後、再コンパイルを行い、同様の「Program asset is not valid」ログが出ないことを確認します。[1][2]

- 3) VRChat レイヤーとコリジョンの設定  
  - VRChat SDK の Control Panel で「Setup Layers for VRChat」を実行し、続けて「Set Collision Matrix」を実行して、Physics の Layer Collision Matrix に推奨設定を適用します。[4][1]
  - Project Settings > Physics の「Layer Collision Matrix」で反映を確認します（SDK の手順通りでバリデーションが通る状態にします）。[4][1]

- 4) 再ビルド  
  - すべての Program Asset エラーとレイヤー・コリジョンの警告が解消したら、Build & Test を再実行し、ポストビルドで例外が発生しないことを確認します。[1]

## 影響範囲（抜粋）
- VTMController 上の対象コンポーネント（各 1 件）: PlayerDataManager, DiagnosisController, VectorBuilder, CompatibilityCalculator, PerfGuard, ValuesSummaryGenerator, MainUIController, SafetyController, SessionRoomManager。[1]
- PlayerProfile_00 〜 PlayerProfile_29 の 30 個で PublicProfilePublisher がすべて「Program asset is not valid」となっており、最優先で修復対象です。[1]

## 再発防止のヒント
- UdonSharp 1.x の要件に合わせてプロジェクトを整理し、Creator Companion 経由で UdonSharp/SDK を最新・整合状態に保ち、空の Program Asset や重複・不一致を作らない運用にします。[3]
- シーンごとの _UdonProgramSources を点検し、「Source Script が None」の資産が生成されないように変更やバックアップ運用を見直します（見つけた場合は削除または再割当）。[2]

必要であれば、該当 GameObject ごとのエラー一覧と修復手順のチェックリスト化も提供できます（例：PlayerProfile_* から順に Program Source を確認→未割当資産の再生成→再コンパイル→SDK 設定→再ビルドの順）。[2][3][4][1]

[1](https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/attachments/52522745/bc2786d0-ccb3-4bd6-8a97-dfe37b728458/paste.txt)
[2](https://ask.vrchat.com/t/udon-compilation-exception-udonsharp-udonsharpprogramasset-is-null/18786/3)
[3](https://udonsharp.docs.vrchat.com/migration/)
[4](https://qiita.com/henjiganai/items/b199a26e833a35f4a042)
[5](https://assetexplorer.natsuneko.com/packages/cllg0qxlp0001l909934b5f91?version=1.1.9&q=&before=bccc6c4ed9117f64aaee0b63ddb70b3d)
[6](https://ask.vrchat.com/t/backups-generate-udonsharp-errors/24183)
[7](https://github.com/MerlinVR/UdonSharp/blob/master/Assets/UdonSharp/Editor/Editors/UdonSharpGUI.cs)
[8](https://dontpaniclabs.com/blog/post/2011/08/11/my-battle-with/)
[9](https://github.com/MerlinVR/UdonSharp/releases)
[10](https://github.com/VRLabs/Collision-Detection)
[11](https://nullreferenceexception2.rssing.com/chan-16888554/all_p46.html)
[12](https://zenn.dev/syunpp/articles/45654cabf68315)
[13](https://ppl-ai-code-interpreter-files.s3.amazonaws.com/web/direct-files/0bf0a8f5f19be04ba4903191144d1549/53538de3-93ba-427b-9919-6ea6af43cbf5/56233d35.csv)
[14](https://ppl-ai-code-interpreter-files.s3.amazonaws.com/web/direct-files/0bf0a8f5f19be04ba4903191144d1549/53538de3-93ba-427b-9919-6ea6af43cbf5/6d81dae4.csv)

