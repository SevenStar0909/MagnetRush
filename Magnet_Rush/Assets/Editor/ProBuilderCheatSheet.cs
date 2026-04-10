using UnityEditor;
using UnityEngine;

/// <summary>
/// ProBuilder操作チートシート。Tools > ProBuilderチートシート で開く。
/// リサーチ結果を基にしたTPS磁力ゲーム向けリファレンス。
/// </summary>
public class ProBuilderCheatSheet : EditorWindow
{
    private Vector2 m_scrollPos;
    private int m_selectedTab;
    private static readonly string[] k_TabNames =
    {
        "基本", "Face", "Edge", "Vertex",
        "Shape一覧", "UV・Mat", "レシピ", "Tips", "トラブル"
    };

    [MenuItem("Tools/ProBuilder/チートシート")]
    static void Open()
    {
        var window = GetWindow<ProBuilderCheatSheet>("PB チートシート");
        window.minSize = new Vector2(360, 420);
    }

    void OnGUI()
    {
        m_selectedTab = GUILayout.Toolbar(m_selectedTab, k_TabNames);
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);

        switch (m_selectedTab)
        {
            case 0: DrawBasics(); break;
            case 1: DrawFace(); break;
            case 2: DrawEdge(); break;
            case 3: DrawVertex(); break;
            case 4: DrawShapes(); break;
            case 5: DrawUV(); break;
            case 6: DrawRecipes(); break;
            case 7: DrawTips(); break;
            case 8: DrawTroubleshoot(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawBasics()
    {
        Section("ProBuilder 6.0 の始め方");
        Bullet("Tools overlay（Scene左上）でProBuilderコンテキストを有効化");
        Bullet("Tool Settings overlay でVertex/Edge/Faceモードを切替");
        Bullet("右クリックでアクションメニューが出る");
        Bullet("旧ProBuilder Windowは廃止。全てScene View内で操作");

        Section("右クリックメニュー構造（6.0）");
        Row("Editors", "UV Editor 等のエディタウィンドウ");
        Row("Edit", "Undo/Redo 等の編集操作");
        Row("Dimensions Overlay", "寸法表示のON/OFF");
        Row("Selection", "Grow / Shrink / Loop / Ring 等の選択ツール");
        Row("Interaction", "インタラクション設定");
        Row("Object", "Merge / Mirror / Freeze / Center Pivot 等");
        Row("Geometry", "Extrude / Bridge / Bevel / Inset / Cut 等");
        Row("Materials", "面ごとのマテリアル設定");
        Row("Vertex Colors", "頂点カラーの塗り");
        Row("Repair", "修復（Weld/Remove Unused等）");
        Row("Export", "OBJ / STL / PLY / Asset にエクスポート");
        Row("Actions", "Lightmap UVs / Subdivide 等");
        Row("Debug", "デバッグ情報");

        Section("6.0の主な変更");
        Bullet("ProBuilder Window → Scene View コンテキスト+overlay に統合");
        Bullet("ほとんどのアクションにプレビュー機能追加");
        Bullet("Shift押しでShape連続複製が可能");
        Bullet("Edit > Shortcuts で全ショートカット確認・変更可能");

        Section("モード切替");
        Row("H", "Vertex → Edge → Face サイクル");
        Row("Escape", "Object モードに戻る");

        Section("ショートカット一覧");
        Row("Ctrl+E", "Extrude（押し出し）");
        Row("Shift+ドラッグ", "インタラクティブExtrude（直感操作）");
        Row("Shift+スケール", "Inset（内側に面作成）");
        Row("Alt+B", "Bridge Edges（エッジ間に面を張る）");
        Row("Alt+U", "Insert Edge Loop（全周にループカット）");
        Row("Alt+E", "Connect Edges");
        Row("Alt+V", "Weld Vertices");
        Row("Alt+S", "Subdivide Faces");
        Row("Alt+N", "Flip Face Normals");
        Row("Alt+G", "Grow Selection（選択を隣接要素に拡張）");
        Row("Delete", "選択要素を削除");
        Row("Ctrl+Z", "Undo（こまめに！）");
    }

    void DrawFace()
    {
        Section("Face アクション");
        Row("Extrude [Ctrl+E]", "面を法線方向に押し出す。壁・段差の基本操作");
        Row("", "  3モード: Face Normals / Vertex Normals / Individual");
        Row("", "  正の距離=外向き、負の距離=内向き");
        Row("Inset", "面の中に小さい面を作る。窓枠・くぼみの下準備");
        Row("", "  Shift+スケール で直感的に操作可能");
        Row("Bevel", "面の全エッジを一括面取り。Distanceで幅調整");
        Row("Merge", "隣接する同一平面の面を統合。ジオメトリ簡素化");
        Row("Detach", "面を分離 → Game Object or Submesh");
        Row("Duplicate", "面をコピー");
        Row("Subdivide [Alt+S]", "面を細かく分割（頂点数増に注意）");
        Row("Triangulate", "面を三角形に変換");
        Row("Flip Normals [Alt+N]", "表裏を反転（黒い面の修正に）");
        Row("Delete", "面を削除して穴を開ける");

        Section("Extrude の3モード");
        Bullet("Face Normals: 面の法線方向。隣接面は接続維持");
        Bullet("Vertex Normals: 頂点法線方向（デフォルト）。接続維持");
        Bullet("Individual Faces: 各面を独立して押し出し。隣接は分離");

        Section("よくある手順");
        Bullet("① Inset → Extrude（内向き） = 窓くぼみ");
        Bullet("② Extrude（外向き） = 出っ張り・レッジ");
        Bullet("③ 上面 Delete = 箱の上を開ける");
        Bullet("④ Inset → Delete = 面に穴を開ける");
    }

    void DrawEdge()
    {
        Section("Edge アクション");
        Row("Bridge [Alt+B]", "2つの開いたエッジ間に面を張る");
        Row("", "  ドア穴・接続に必須。デフォルトは開いたエッジのみ");
        Row("", "  Prefs > Allow non-manifold で制限解除可能");
        Row("Edge Loop [Alt+U]", "メッシュ全周にループカット");
        Row("", "  T字路を作らないクリーンな分割。Connect Edgesより推奨");
        Row("Connect [Alt+E]", "選択エッジ間にエッジ追加（T字路注意）");
        Row("Bevel", "エッジを2つに分割し間に面を生成。角を丸くする");
        Row("", "  Distanceでベベル幅を指定");
        Row("Extrude [Ctrl+E]", "エッジを押し出して新しい面を作る");
        Row("", "  As Group: 複数エッジの接続維持");
        Row("Subdivide", "エッジの中点に頂点を追加");
        Row("Fill Hole", "開いた境界エッジから面を生成して穴を塞ぐ");

        Section("Select Loop / Ring");
        Bullet("Loop: 連続するエッジの列（輪切り方向）");
        Bullet("Ring: 平行するエッジの列（縦方向）");
        Bullet("ダブルクリックでLoop選択");

        Section("Edge Loop vs Connect Edges");
        Bullet("Edge Loop = 全周を切る → T字路なし（推奨）");
        Bullet("Connect = 選択間のみ → T字路が発生する可能性あり");
    }

    void DrawVertex()
    {
        Section("Vertex アクション");
        Row("Weld [Alt+V]", "近接頂点をマージ。重複頂点の修正に必須");
        Row("", "  Distance: マージ閾値（デフォルト0.001）");
        Row("", "  定期的に実行して重複頂点を除去すべき");
        Row("Collapse", "選択頂点を1つの頂点に統合");
        Row("Split", "共有頂点を分離。面を独立して動かす準備");
        Row("Connect", "2頂点間にエッジを作成");
        Row("Fill Hole", "境界頂点から面を生成して穴を塞ぐ");

        Section("注意点");
        Bullet("Weldは0.001〜0.01の範囲で定期実行");
        Bullet("1頂点だけ動かすとQuadが非平面になる → 表示が崩れる");
        Bullet("Grid Snapを使って位置を揃える");
    }

    void DrawShapes()
    {
        Section("12種のプリミティブ");
        Row("Cube", "直方体。壁・床・箱。最も基本");
        Row("Stair", "階段。Steps/Height/Circumference指定");
        Row("", "  Circumference: 0=直線, 90=L字, 360=螺旋");
        Row("Arch", "アーチ。ドア上部やトンネル入口");
        Row("", "  Circumference: 180=半円, 360=完全な円");
        Row("Door", "ドアフレーム。壁に合わせた形状");
        Row("", "  PedimentHeight/SideWidth で枠サイズ調整");
        Row("Cylinder", "円柱。柱に。Sides: 4〜64");
        Row("Pipe", "中空円柱。トンネルに。Thickness指定");
        Row("Sphere", "球体。Subdivisions: 1〜5");
        Row("Plane", "平面。床・天井に");
        Row("Cone", "円錐");
        Row("Prism", "三角柱。屋根・傾斜に");
        Row("Torus", "ドーナツ。Rows/Columns/TubeRadius");
        Row("Sprite", "1ユニットの平面");

        Section("PolyShape Tool");
        Bullet("自由な2D輪郭を描いて3D押し出し");
        Bullet("不規則な床プラン・部屋の形状に最適");
        Bullet("Shift押しで連続作成、点の移動・削除・追加可能");

        Section("Bezier Shape（実験的）");
        Bullet("ベジェ曲線に沿ってメッシュを押し出す");
        Bullet("曲がったトンネル向き。Prefs > Experimental で有効化");

        Section("スケール基準");
        Row("プレイヤー", "高さ 2m");
        Row("ドア", "高さ 2.5m、幅 1.2m");
        Row("廊下", "幅 2〜3m以上");
        Row("天井", "高さ 3m");
        Row("階段1段", "高さ 0.2〜0.3m、奥行 0.25〜0.35m");
    }

    void DrawUV()
    {
        Section("UV編集（Alt+U で開く）");

        Section("Autoモード（推奨）");
        Row("Fill Mode", "Tile=繰り返し / Fit=均一 / Stretch=引き伸ばし");
        Row("Anchor", "テクスチャ投影の原点（9ポイント）");
        Row("Offset X/Y", "アンカーからのずらし");
        Row("Rotation", "0〜360度（UV面回転→テクスチャは逆に動く）");
        Row("Tiling", "0.5=半分表示、2=2回繰り返し");
        Row("World Space", "グローバル座標でUV統一。異オブジェクト間で揃える");
        Row("Flip U/V", "水平/垂直反転");
        Row("Texture Group", "隣接面のタイリングを統一（壁の連続テクスチャ）");

        Section("Manualモード");
        Row("Planar投影", "1平面から投影。平らな面向き");
        Row("Box投影", "6面から同時投影。立方体的なオブジェクト向き");
        Row("Weld", "近接UV頂点をマージ（距離: 0.01）");
        Row("Split UVs", "UV要素を分離して独立操作");
        Row("Ctrl+Click", "Autostitch（隣接面のUVを自動接合）");
        Row("Ctrl+Shift+Click", "Copy UVs（面間のUVコピー）");

        Section("Lightmap UV（UV2）");
        Bullet("オブジェクト選択 → Lightmap UVs でUV2を生成");
        Bullet("ベイクドライティングに必須。やらないとライトが壊れる");

        Section("マテリアル（面ごと設定可能）");
        Bullet("① Faceモードで面を選択");
        Bullet("② ProjectからマテリアルをD&D / 右クリック → Set Material");
        Bullet("Material Palette: 最大10スロット、Alt+数字で適用");

        Section("ブロックアウト色分け");
        Row("グレー", "床");
        Row("青", "壁");
        Row("赤", "危険・ダメージゾーン");
        Row("緑", "インタラクティブ要素");
        Row("黄", "カバーポイント");

        Section("URP マテリアルのコツ");
        Bullet("シェーダー: Universal Render Pipeline/Lit");
        Bullet("コンクリート風: Metallic=0, Smoothness=0.2〜0.4");
        Bullet("UV Tiling でテクスチャ密度を合わせる（1タイル/m）");
    }

    void DrawRecipes()
    {
        Section("窓を作る");
        Bullet("① Cubeで壁を作成（例: 5×3×0.3m）");
        Bullet("② Faceモードで前面を選択");
        Bullet("③ Insert Edge Loop で窓位置に水平・垂直ループ追加");
        Bullet("④ 窓にしたい面を選択 → Inset（窓枠分の余白）");
        Bullet("⑤ Extrude で内向き（負の距離）→ 窓くぼみ");
        Bullet("⑥ 貫通させるなら押し込んだ面を Delete");

        Section("ドア穴を作る");
        Bullet("① Cubeで壁を作成");
        Bullet("② Insert Edge Loop でドアの幅・高さにループ追加");
        Bullet("③ ドア部分の面を Delete → 貫通穴");
        Bullet("または Door Shape を使って最初からフレーム形状で作成");

        Section("スロープ（坂道）を作る");
        Bullet("① Cube を作成");
        Bullet("② Vertexモードで片端の上面2頂点を選択");
        Bullet("③ Y軸方向に下げる");
        Bullet("歩行可能角度は30度以下推奨（CC は45度まで）");

        Section("階段を作る");
        Bullet("簡単: Shape Tool → Stairs（Steps/Height指定）");
        Bullet("Circumference: 0=直線, 90=L字, 360=螺旋");
        Bullet("コライダーTip: 透明スロープを上に被せてスムーズ移動");

        Section("カバーオブジェクト");
        Row("ハーフウォール", "Cube 2×1.2×0.3m（しゃがみカバー）");
        Row("スタンドカバー", "Cube 2×1.8×0.3m（立ちカバー）");
        Row("柱", "Cylinder Sides:8, R:0.3m, H:3m");
        Row("クレート", "Cube 1×1×1m + Bevel(0.02)");

        Section("アーチ・トンネル");
        Bullet("アーチ: Shape → Arch, Circumference=180, Sides=8〜12");
        Bullet("トンネル: Shape → Pipe, X軸90度回転, 底面Delete");

        Section("MagnetRush 固有の設計");
        Bullet("磁力弾が壁に貼り付く → 壁面積が広い方が戦略的");
        Bullet("磁場範囲を考慮した空間設計（狭すぎると全域影響）");
        Bullet("可動オブジェクト配置場所を確保（Push用）");
        Bullet("カバー越しの磁力引き寄せ → 厚みと高さが重要");
        Bullet("高低差で3D的な磁力戦術（上から貼り付け等）");
    }

    void DrawTips()
    {
        Section("TPS レベルデザインの原則");
        Bullet("カバーは3〜5m間隔で配置（カバーレーン形成）");
        Bullet("30〜45度の角度で複数方向からの脅威に対応");
        Bullet("長い直線廊下は避ける → L字・T字で視線を切る");
        Bullet("最大交戦距離: 30〜50m（TPSのカメラオフセット考慮）");
        Bullet("高低差は2〜3段まで。各レベル約3m差");
        Bullet("高所には必ず迂回ルート（一方的有利位置は作らない）");

        Section("ワークフロー");
        Bullet("① 紙でスケッチ（動線・高低差・カバー配置）");
        Bullet("② Grid Snap ON でプリミティブ配置（1m単位）");
        Bullet("③ プレイヤーを入れてスケール・動線を確認");
        Bullet("④ 面ごとにマテリアルで色分け");
        Bullet("⑤ テストプレイ → 修正を繰り返す");
        Bullet("⑥ 確定後にFBXエクスポート → アーティスト引き渡し");

        Section("やるべき");
        Bullet("Grid Snap常にON（ズレ防止・光漏れ防止）");
        Bullet("Edge Loop でT字路を避ける");
        Bullet("Quad（四角面）を維持");
        Bullet("同じ形状はPrefabにする");
        Bullet("定期的にWeld Verticesで重複頂点マージ");
        Bullet("保存はこまめに（メッシュ直接編集）");
        Bullet("Lightmap UVs をベイク前に必ず生成");

        Section("やらない");
        Bullet("ゲームプレイ確定前にディテール入れない");
        Bullet("Subdivide多用しない（頂点数爆発）");
        Bullet("Connect Edges多用しない（T字路の原因）");
        Bullet("非平面Quadを放置しない");
        Bullet("Boolean結果を無検証で使わない");

        Section("FBXエクスポート");
        Bullet("com.unity.formats.fbx パッケージが必要");
        Bullet("GameObject > Export To FBX");
        Bullet("Linked Prefab でBlender/Mayaと往復可能");
        Bullet("Script Stripping: ビルド時にPBコンポーネント自動除去");
    }

    void DrawTroubleshoot()
    {
        Section("面が黒い");
        Bullet("原因: 法線が裏返っている");
        Bullet("対処: 面を選択 → Flip Face Normals (Alt+N)");

        Section("面がちらつく / 消えたり見えたりする");
        Bullet("原因: 重複頂点（同位置に2面が重なっている）");
        Bullet("対処: 全頂点選択 → Weld Vertices (0.001〜0.01)");

        Section("ライトマップが壊れる");
        Bullet("原因: UV2（ライトマップUV）が未生成");
        Bullet("対処: オブジェクト選択 → Lightmap UVs 実行");

        Section("URPでマテリアルが正しく表示されない");
        Bullet("原因: URP用シェーダーサンプルが未インポート");
        Bullet("対処: Package Manager > ProBuilder > Samples > URP Support");

        Section("Extrude が期待通りに動かない");
        Bullet("原因: Extrude By モードが合っていない");
        Bullet("隣接面を接続したまま → Face/Vertex Normals");
        Bullet("各面を独立して → Individual Faces");

        Section("メッシュが自己交差する");
        Bullet("原因: 頂点を隣接頂点を超えて移動した");
        Bullet("対処: Grid Snap使用 + Undo で戻す");

        Section("FBX Exporter 削除後にコンパイルエラー");
        Bullet("原因: PROBUILDER_FBX_PLUGIN_ENABLED define が残存");
        Bullet("対処: PlayerSettings > Scripting Define Symbols から削除");

        Section("Boolean(CSG)の結果がおかしい");
        Bullet("実験的機能のため結果にクリーンアップが必要");
        Bullet("操作前に必ず保存すること");
        Bullet("Prefs > Experimental Features で有効化が必要");

        Section("コライダーが更新されない");
        Bullet("Prefs > Auto Resize Colliders がONか確認");
        Bullet("手動: MeshCollider コンポーネントを再追加");
    }

    static void Section(string title)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
    }

    static void Row(string key, string desc)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(key, EditorStyles.miniLabel, GUILayout.Width(140));
        EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndHorizontal();
    }

    static void Bullet(string text)
    {
        EditorGUILayout.LabelField("  • " + text, EditorStyles.wordWrappedLabel);
    }
}
