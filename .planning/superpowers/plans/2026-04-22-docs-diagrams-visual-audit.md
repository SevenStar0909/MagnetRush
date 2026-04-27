# VitePress docs 全図ビジュアル監査と修正 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** website/docs 配下の全19ページ・22個のPlantUML図を実ブラウザで順次スクショ検査し、レンダリング失敗・横幅はみ出し・文字切れを洗い出して1ページずつ修正する。先行ブロッカーとして `scripts/Core/Player.md` の SVG 配信不全を解消する。

**Architecture:** 作業は 5 フェーズ。Phase 1 で Player.md の SVG 配信バグを根本解決（dev server とプラグインの挙動を制御）。Phase 2 で全19ページを Playwright で一括スクショ + メトリクス取得。Phase 3 で問題をカテゴリ分け。Phase 4 でページごとに PlantUML を修正。Phase 5 で再スクショして全ページ合格を検証。

**Tech Stack:**
- VitePress 1.6.4 (dev server on port 5173+)
- vitepress-plugin-diagrams 1.2.2 (PlantUML → Kroki.io → SVG)
- Kroki.io (external PlantUML renderer)
- Playwright 1.59 (headless Chromium スクショ + DOM メトリクス)
- Node.js (ES2022)

**対象ページ一覧（19ページ、22図）:**
```
基盤層:
  docs/scripts/index.md                                 (2 図)
Core:
  docs/scripts/Core/Bullet.md                           (1)
  docs/scripts/Core/Entity.md                           (1)
  docs/scripts/Core/Entity/Interfaces.md                (— 本文のみ、図なし)
  docs/scripts/Core/Entity/StateMachine.md              (1)
  docs/scripts/Core/Enemy.md                            (1)
  docs/scripts/Core/Enemy/Turret.md                     (1)
  docs/scripts/Core/Enemy/Weapon.md                     (1)
  docs/scripts/Core/Magnet.md                           (2)
  docs/scripts/Core/Magnet/Field.md                     (1)
  docs/scripts/Core/Magnet/Interfaces.md                (1)
  docs/scripts/Core/Player.md                           (1)  ← ブロッカー
  docs/scripts/Core/Player/States.md                    (1)
Concepts:
  docs/scripts/Concepts/AssemblyGraph.md                (1)
  docs/scripts/Concepts/EventArchitecture.md            (1)
  docs/scripts/Concepts/MagnetHoldSystem.md             (2)
  docs/scripts/Concepts/VelocityModel.md                (1)
提示層:
  docs/scripts/Game.md                                  (1)
  docs/scripts/Rendering.md                             (1)
  docs/scripts/UI.md                                    (1)
```

**成果物:**
- 全22図が正しくレンダリング、ページ幅内に収まる、文字切れなし
- `C:/Users/nanat/AppData/Local/Temp/docs-shots/` に before/after スクショ 2 セット
- `website/_audit.json` に各ページのメトリクス（図サイズ・overflow 有無）ログ

---

## Phase 1: Player.md SVG 配信ブロッカーの解消

### Task 1: 現状の再現と切り分け

**Files:**
- 診断のみ: `website/docs/public/diagrams/plantuml-Player-21-*.svg`
- 参照: `website/docs/.vitepress/config.mts`

- [ ] **Step 1: ファイルシステム側の検証**

```bash
ls -la "C:/Users/nanat/Desktop/MagnetRush/website/docs/public/diagrams/" | grep -i player
```
Expected: `plantuml-Player-21-465f1d7fb2aa6643c1e7a5a5a77ffc5d.svg` が 8978 bytes 前後で存在。中身の先頭は `<?plantuml ... ?><svg xmlns="...">`。

- [ ] **Step 2: dev server の応答確認**

Port は `_shot.js` で使う最新ポート（最初に 5173、衝突時は 5174 / 5175）。現行の task ID は `b3isjxda1`、port は 5175。

```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && node -e "
const http = require('http');
http.get('http://localhost:5175/diagrams/plantuml-Player-21-465f1d7fb2aa6643c1e7a5a5a77ffc5d.svg', r => {
  console.log(r.statusCode, r.headers['content-type']);
  let n = 0; r.on('data', c => n += c.length); r.on('end', () => console.log('bytes:', n));
});"
```
Expected (再現): `200 text/html bytes: 447` (SPA フォールバック、SVG として配信されない)。
If not reproducing: `200 image/svg+xml bytes: ~8978` なら Task 4 まで飛ばして再検証に戻る。

- [ ] **Step 3: 他ページの既存SVGと対比**

```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && node -e "
const http = require('http');
http.get('http://localhost:5175/diagrams/plantuml-Enemy-21-53c5234e7792bb9fdd552c641d17efef.svg', r => {
  console.log(r.statusCode, r.headers['content-type']);
  let n = 0; r.on('data', c => n += c.length); r.on('end', () => console.log('bytes:', n));
});"
```
Expected: `200 image/svg+xml bytes: ~11211`（正常なSVG配信）。
この差が示すもの: **dev server 起動後に作られた新規SVGファイルが vite の public 配信に拾われていない**仮説が強い。

### Task 2: 根本原因の webリサーチ

- [ ] **Step 1: vitepress-plugin-diagrams の挙動調査**

Context7 で `vitepress-plugin-diagrams` のドキュメントを引く。

```
mcp__context7__resolve-library-id with libraryName="vitepress-plugin-diagrams"
→ mcp__context7__query-docs with the resolved id, topic="dynamic public file generation dev server watch"
```
Expected: プラグインが `docs/public/diagrams/` へファイルを書き込むタイミング（build 時 vs dev 時）、dev server 側で拾うためのフラグや推奨フォルダ配置が分かる。

- [ ] **Step 2: vite public 配信の仕様確認**

Context7 で `vite` に対し `topic="public directory dev server serve static runtime"`。
Expected: `publicDir` に配置したファイルは **dev server 起動時のスキャン済み範囲でのみ** URL としてアクセス可能（dev 中に後から追加したファイルはリロードで拾われる）、もしくは `vite.config` の `server.fs` / `publicDir` watch 設定で制御できる等の仕様。

- [ ] **Step 3: 調査結果から対策を1つ選ぶ**

候補（どれを採るかは Step 1-2 の結果で確定）:
- **A:** プラグインの出力先を `docs/public/diagrams/` から `docs/diagrams/` に移し、`assets` として扱う（`publicPath` 同時変更）
- **B:** `vite.config` 相当（`.vitepress/config.mts` の `vite` オプション）に `server.watch.ignored: false` を足して public 配下の変更を拾わせる
- **C:** dev 中は kroki から取得した SVG をインライン化する設定が `vitepress-plugin-diagrams` にあればそちら
- **D:** `dev server 再起動` をスクリプト化して毎回完全起動にする運用回避策（根本対策ではない最終手段）

決定をコメントとして Task 3 の冒頭に書く。

### Task 3: 対策実装

**Files:**
- Modify: `website/docs/.vitepress/config.mts` もしくは `website/package.json` (選んだ対策に応じて)

- [ ] **Step 1: Task 2 で選んだ対策を適用**

（仮に B の場合の一例。実際は Task 2 の Step 3 の決定に従う）

`website/docs/.vitepress/config.mts` に `vite` キーを追加:
```ts
import { defineConfig } from 'vitepress'
import { configureDiagramsPlugin } from 'vitepress-plugin-diagrams'

export default defineConfig({
  title: 'Magnet Rush',
  // ...既存設定...
  vite: {
    server: {
      watch: {
        ignored: ['!**/docs/public/diagrams/**'],
      },
    },
  },
})
```

- [ ] **Step 2: dev server を停止・再起動**

既存のバックグラウンド task を TaskStop で停止してから:
```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && npm run docs:dev
```
(run_in_background: true)

約 8 秒 sleep して起動確認。ログに `Local: http://localhost:51xx/` が出るまで待つ。

- [ ] **Step 3: Player.md を一度保存し直して HMR を発火**

```bash
# Player.md の末尾に無害な改行を足して削除する等、mtime を更新する
cd "C:/Users/nanat/Desktop/MagnetRush/website/docs/scripts/Core" && python -c "
import pathlib, time
p = pathlib.Path('Player.md')
p.touch()
"
```
Expected: dev server ログに `hmr update /scripts/Core/Player.md` が出る。続いて `Removed old diagram file: plantuml-Player-21-*.svg` の後で新しい SVG 生成ログが出るはず。

- [ ] **Step 4: 配信確認（Task 1 Step 2 の再実行）**

```bash
# 最新ポート (起動ログで確認)
cd "C:/Users/nanat/Desktop/MagnetRush/website" && node -e "
const http = require('http');
const port = process.env.PORT || '5173';
const fs = require('fs');
const svg = fs.readdirSync('docs/public/diagrams').find(f => f.startsWith('plantuml-Player-21-'));
http.get('http://localhost:' + port + '/diagrams/' + svg, r => {
  console.log(r.statusCode, r.headers['content-type']);
  let n = 0; r.on('data', c => n += c.length); r.on('end', () => console.log('bytes:', n));
});"
```
Expected: `200 image/svg+xml bytes: 8000〜10000`。
If still 447 text/html: 採った対策が不発。Task 2 に戻って別の候補を試す。

- [ ] **Step 5: ブラウザ側の表示検証**

```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && node _shot.js
```
続けて `player-arch-section.png` を Read で確認。
Expected: 「plantuml Diagram」プレースホルダーが消え、実際の図が描画されている。

- [ ] **Step 6: コミット（オプション。ユーザー指示があるまでは保留）**

この時点では **コミットしない**（プロジェクトCLAUDE.md: 「コミットはユーザーの指示があるまで絶対にしない」）。
タスク完了フラグだけつけて次へ。

---

## Phase 2: 全ページ一括ベースライン取得

### Task 4: 一括スクショ用スクリプトの作成

**Files:**
- Create: `website/_audit.js`
- Create 出力先: `C:/Users/nanat/AppData/Local/Temp/docs-shots/before/`
- Create: `C:/Users/nanat/AppData/Local/Temp/docs-shots/audit-before.json`

- [ ] **Step 1: `website/_audit.js` を新規作成**

```js
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

// 対象ページ（url path）全19件。新規追加時はここに並べる
const PAGES = [
  '/scripts/',                                  // index.md (2 diagrams)
  '/scripts/Core/Bullet',
  '/scripts/Core/Entity',
  '/scripts/Core/Entity/StateMachine',
  '/scripts/Core/Enemy',
  '/scripts/Core/Enemy/Turret',
  '/scripts/Core/Enemy/Weapon',
  '/scripts/Core/Magnet',
  '/scripts/Core/Magnet/Field',
  '/scripts/Core/Magnet/Interfaces',
  '/scripts/Core/Player',
  '/scripts/Core/Player/States',
  '/scripts/Concepts/AssemblyGraph',
  '/scripts/Concepts/EventArchitecture',
  '/scripts/Concepts/MagnetHoldSystem',
  '/scripts/Concepts/VelocityModel',
  '/scripts/Game',
  '/scripts/Rendering',
  '/scripts/UI',
];

const BASE = process.env.BASE || 'http://localhost:5173';
const OUT_DIR = process.argv[2] || 'C:/Users/nanat/AppData/Local/Temp/docs-shots/before';
const JSON_OUT = path.join(OUT_DIR, '../audit-' + path.basename(OUT_DIR) + '.json');

fs.mkdirSync(OUT_DIR, { recursive: true });

(async () => {
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  const report = [];

  for (const p of PAGES) {
    const page = await ctx.newPage();
    const url = BASE + p;
    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
      await page.waitForTimeout(1200);

      const info = await page.evaluate(() => {
        const docWidth = document.documentElement.clientWidth;
        const scrollWidth = Math.max(document.body.scrollWidth, document.documentElement.scrollWidth);
        const figs = [...document.querySelectorAll('figure.vpd-diagram')].map(fig => {
          const img = fig.querySelector('img');
          const r = fig.getBoundingClientRect();
          const ir = img ? img.getBoundingClientRect() : null;
          return {
            figX: Math.round(r.x),
            figW: Math.round(r.width),
            imgW: ir ? Math.round(ir.width) : null,
            imgH: ir ? Math.round(ir.height) : null,
            naturalW: img ? img.naturalWidth : 0,
            naturalH: img ? img.naturalHeight : 0,
            src: img ? img.getAttribute('src') : null,
          };
        });
        return { docWidth, scrollWidth, figs };
      });

      // 評価: 何が壊れている可能性があるか
      info.issues = [];
      if (info.scrollWidth > info.docWidth + 1) info.issues.push('page-hscroll');
      for (const f of info.figs) {
        if (!f.naturalW || !f.naturalH) info.issues.push('svg-broken:' + (f.src || 'null'));
        if (f.imgW && f.figW && f.imgW > f.figW + 4) info.issues.push('img-overflow');
        if (f.figX < 0) info.issues.push('fig-negative-x');
      }

      const fname = p.replace(/\//g, '_').replace(/^_+|_+$/g, '') || 'index';
      await page.screenshot({ path: path.join(OUT_DIR, fname + '.png'), fullPage: true });

      report.push({ url: p, ...info });
      console.log('OK', p, 'issues=' + (info.issues.length ? info.issues.join(',') : 'none'));
    } catch (e) {
      report.push({ url: p, error: String(e) });
      console.log('ERR', p, String(e));
    } finally {
      await page.close();
    }
  }

  fs.writeFileSync(JSON_OUT, JSON.stringify(report, null, 2));
  console.log('Report saved to', JSON_OUT);
  await browser.close();
})();
```

- [ ] **Step 2: スクリプトを "before" で実行**

```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && BASE=http://localhost:<最新ポート> node _audit.js "C:/Users/nanat/AppData/Local/Temp/docs-shots/before"
```
Expected: 19行の `OK ... issues=...` と最後に `Report saved to C:/Users/.../audit-before.json`。

- [ ] **Step 3: レポート JSON を確認**

```bash
cat "C:/Users/nanat/AppData/Local/Temp/docs-shots/audit-before.json" | python -c "
import json, sys
data = json.load(sys.stdin)
for r in data:
    issues = r.get('issues', [])
    status = 'BROKEN' if (r.get('error') or any(i.startswith('svg-broken') for i in issues)) else ('WARN' if issues else 'OK')
    print(f\"{status:7} {r['url']}  {','.join(issues) if issues else ''}\")
"
```
Expected: Phase 1 が成功していれば Player.md は OK か WARN。BROKEN が残っていれば Phase 1 の検証を再実施。

### Task 5: 問題ページの目視確認

- [ ] **Step 1: issues !== [] のページのスクショを Read で開いて目視**

Task 4 Step 3 の出力で WARN/BROKEN になった各ページの PNG を Read で表示。
- `page-hscroll` → ページ全体に横スクロール発生
- `img-overflow` → 図がコンテナから溢れている
- `svg-broken` → SVG が読み込まれていない
- `fig-negative-x` → 図が負座標に配置されている

- [ ] **Step 2: 問題の分類を記録**

以下の表を `C:/Users/nanat/AppData/Local/Temp/docs-shots/issues-classified.md` に書く。

```markdown
| URL | 問題種別 | 現況の SVG 寸法 | 原因候補 |
|---|---|---|---|
| /scripts/Core/Player | img-overflow | 1287×??? | 子ノード多すぎ→幅1287px |
| /scripts/Concepts/MagnetHoldSystem | ??? | ??? | ??? |
| ... | | | |
```

実際の数値は audit-before.json の figs から埋める。

---

## Phase 3: ページごとの修正

問題があったページだけ 1 ページ = 1 タスクで修正する。Phase 2 Step 3 の出力で問題なしの場合は Phase 3 をスキップ。

ここでは**典型的に想定される修正タスク**を Task 6〜Task N として予め書いておく。Task 5 Step 2 の分類表を見て、該当しないタスクはスキップ、追加が必要ならここに挿入する。

### Task 6: Core/Player.md（横幅オーバー）

**Files:**
- Modify: `website/docs/scripts/Core/Player.md:17-46`（アーキテクチャ概観のplantumlブロック）

**対処方針**: 現行 `left to right direction` + 6 個のボックスがすでに 1000px 超。以下のいずれかを採用して 800px 以下に。
- サブグループを package でまとめて縦配置
- 長いラベル（例: `― AimController    (LT → Time.timeScale)`）を短縮

- [ ] **Step 1: 候補Aのplantuml差し替え案を作成**

```plantuml
@startuml
skinparam defaultFontName "Noto Sans JP"
skinparam rectangle { BackgroundColor #F0F4FA; BorderColor #3451B2; FontSize 13 }
skinparam ArrowColor #666666
skinparam nodesep 8
skinparam ranksep 18

rectangle "Player\n(Entity : IMagnetTarget)" as P
package "入力 / 状態" {
  rectangle "PlayerInputHandler" as PIH
  rectangle "PlayerEvents (UnityEvent)" as PE
  rectangle "PlayerStateManager\nIdle / Move / Aim / Die" as PSM
  rectangle "Magnetizable" as MAG
}
package "能力 Controller" {
  rectangle "AimController (LT)" as AC
  rectangle "PoleController (Y)" as PC
  rectangle "ShootingController (RT/A/X)" as SC
}
package "演出" {
  rectangle "PlayerAnimator" as PA
  rectangle "CameraSettingsApplier" as CSA
}
rectangle "Entity override\nGravity / SnapForce / ExternalDrag / GroundLayer / PullOrientation*" as EO

P --> PIH
P --> PE
P --> PSM
P --> MAG
P --> AC
P --> PC
P --> SC
P --> PA
P --> CSA
P --> EO
@enduml
```

- [ ] **Step 2: Player.md に適用**

Edit で差し替え。

- [ ] **Step 3: HMR反映を待って単発再スクショ**

```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && sleep 4 && node _shot.js
```
Read で `player-arch-section.png` を確認。
Expected: ページ幅内に収まり、3グループに package で分割されて可視性が上がっている。

- [ ] **Step 4: 収まっていなければ候補Bを試す**

候補B: ラベル短縮のみ（ボックス数は6のまま）
```plantuml
rectangle "能力 Controller\nAim / Pole / Shooting" as CTL
rectangle "演出\nPlayerAnimator / Camera" as VIS
```

### Task 7: 問題が見つかった他ページの修正（1ページ1タスクで追加）

**方式は Task 6 と同じ:**
1. Phase 2 で検出された問題ページを 1 つ取る
2. 該当 `.md` の `plantuml` ブロックを読む
3. 問題に応じて PlantUML を修正
4. スクショで確認
5. ダメなら別案を試す

Phase 2 の結果を見てからタスクを追加する。プレースホルダ「Task 7: <URL>」として後から具体化する。

---

## Phase 4: 一括再スクショと検証

### Task 8: "after" 一括スクショ

- [ ] **Step 1: _audit.js を "after" で再実行**

```bash
cd "C:/Users/nanat/Desktop/MagnetRush/website" && BASE=http://localhost:<最新ポート> node _audit.js "C:/Users/nanat/AppData/Local/Temp/docs-shots/after"
```
Expected: 19 ページで `issues=none` が並ぶ。

- [ ] **Step 2: before / after 比較**

```bash
python -c "
import json
b = json.load(open('C:/Users/nanat/AppData/Local/Temp/docs-shots/audit-before.json'))
a = json.load(open('C:/Users/nanat/AppData/Local/Temp/docs-shots/audit-after.json'))
bd = {r['url']: r.get('issues', []) for r in b if 'issues' in r}
ad = {r['url']: r.get('issues', []) for r in a if 'issues' in r}
fixed, remaining = [], []
for u in bd:
    if bd[u] and not ad.get(u):
        fixed.append(u)
    elif ad.get(u):
        remaining.append((u, ad[u]))
print('FIXED:', len(fixed))
for u in fixed: print('  +', u)
print('REMAINING:', len(remaining))
for u, iss in remaining: print('  !', u, iss)
"
```
Expected: REMAINING=0 なら完了。残っていれば Phase 3 に戻って該当ページを再修正。

### Task 9: 最終目視

- [ ] **Step 1: 基盤層（index.md）から提示層（UI.md）までの順に Read で PNG 確認**

以下の順序で 1 枚ずつ Read:
1. `scripts_.png` (index)
2. `scripts_Core_Bullet.png`
3. `scripts_Core_Entity.png`
4. `scripts_Core_Entity_StateMachine.png`
5. `scripts_Core_Enemy.png`
6. `scripts_Core_Enemy_Turret.png`
7. `scripts_Core_Enemy_Weapon.png`
8. `scripts_Core_Magnet.png`
9. `scripts_Core_Magnet_Field.png`
10. `scripts_Core_Magnet_Interfaces.png`
11. `scripts_Core_Player.png`
12. `scripts_Core_Player_States.png`
13. `scripts_Concepts_AssemblyGraph.png`
14. `scripts_Concepts_EventArchitecture.png`
15. `scripts_Concepts_MagnetHoldSystem.png`
16. `scripts_Concepts_VelocityModel.png`
17. `scripts_Game.png`
18. `scripts_Rendering.png`
19. `scripts_UI.png`

Expected: どのページも（a）SVG が描画されている（b）ページ幅に収まっている（c）文字切れなし。

- [ ] **Step 2: 見つけた問題を Phase 3 の新タスクとして追加**

残課題があれば具体的なタスクとして追記し、Phase 3 → Phase 4 をもう1周。

---

## Phase 5: 片付け

### Task 10: 一時ファイルの整理

- [ ] **Step 1: デバッグスクリプトの扱いを決める**

`website/_shot.js`, `website/_debug.js`, `website/_audit.js` は gitignore 対象か残すか判断:
- 既存の `.gitignore` に `/_*.js` パターンがあれば問題なし
- なければ `.gitignore` に `_*.js`, `_shot*`, `_debug*`, `_audit*` を追加

```bash
grep -E "^_|_\\*\\.js" "C:/Users/nanat/Desktop/MagnetRush/website/.gitignore" 2>/dev/null
```
If empty and files are not gitignored: `.gitignore` に行追加。

- [ ] **Step 2: スクショ tmp の存在確認**

```bash
ls "C:/Users/nanat/AppData/Local/Temp/docs-shots/"
```
Temp 配下なので消さずに残す（次回比較用）。

- [ ] **Step 3: コミットは未実施のまま、作業終了報告**

ユーザーの指示を待つ。コミットも PR も作らない。

---

## 変更履歴

- 2026-04-22 初版作成
