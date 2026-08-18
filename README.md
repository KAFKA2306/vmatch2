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
