# vmatch2

VRChat内でユーザー同士の相性を計算・推薦する `VirtualTokyoMatching` の Unity / UdonSharp prototype です。

現行実装は公開profileの6次元ベクトルを取得し、cosine similarity と confidence を計算して上位recommendationを更新します。計算はframe budgetに応じて分散実行されます。

主な実装:

- `Assets/VirtualTokyoMatching/Scripts/Matching/CompatibilityCalculator.cs` — compatibility計算とrecommendation
- `Assets/VirtualTokyoMatching/Scripts/Assessment/` — assessment
- `Assets/VirtualTokyoMatching/Scripts/Performance/` — performance制御
- `Assets/VirtualTokyoMatching/Scripts/Safety/` — safety関連
- `Assets/VirtualTokyoMatching/Scripts/Session/` — session管理
- `Assets/VirtualTokyoMatching/Scripts/UI/` — UI

このrepositoryはWebのイベント検索や一般的なVRChat情報サイトではなく、VRChat world内で動くmatching systemの実装です。

回帰テスト:

```bash
python -m unittest discover -s tests -p 'test_*.py' -v
```

このテストは、永続化復元後の1回答編集と全回答からの再構築が同じ30Dベクトルになること、同一回答の再適用が冪等であること、VectorBuilderから公開経路まで不整合状態をfail-closedにする契約を検証します。
