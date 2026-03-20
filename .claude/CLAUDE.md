**このファイル(.claude/CLAUDE.md)はユーザーが明示的に編集を指示した場合のみ変更すること。自主的な編集禁止。**

# GameJam


## このファイルの方針

- 短く、具体的に、Claudeが自分で検証ループを回せる情報を書く
- 命名規則・禁止事項など「コードと照合できるルール」を書く
- アーキテクチャ詳細は書かない（コードを読めばわかる、陳腐化する）



# 開発ワークフロー

**Windowsコマンドは `powershell -Command` 経由で実行**

```bash
# 1. パス指定
C:/Users/nanat/file.txt                    # ✓ フォワードスラッシュ
C:\Users\nanat\file.txt                    # ✗ エスケープと解釈される

# 2. hooks のパス
"bash \"C:/Users/nanat/Desktop/NS-ENGINE/.claude/hooks/hook.sh\""  # ✓ 絶対パス
"bash \"$PROJECT_ROOT/.claude/hooks/hook.sh\""                     # ✗ 環境変数は展開されない
```

**ファイル削除は `rm` ではなくゴミ箱送り**（bash からシングルクォートで渡す）:
```bash
powershell -Command 'Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile("C:\full\path\file.txt","OnlyErrorDialogs","SendToRecycleBin")'
```

PowerShellでUTF-8ファイル（日本語含む）を操作する際は、必ず-Encoding UTF8を指定する必要があります。

# Assetsフォルダ内のファイル操作

- **Assets/ 以下のファイル作成・削除・移動は必ずUniCLI or MCPツール経由で行う**（`.meta` が自動管理される）
- `Write`/`Edit`/ゴミ箱送りで直接操作すると `.meta` の不整合が起きる
- やむを得ず直接操作した場合: 対応する `.meta` も必ずセットで処理し、`ls Assets/**/*.meta` でorphan確認

# Unity起動中のファイル変更

- **`.prefab` / `.asset` の値変更はMCPツール（`manage_components`等）経由が安全**
- **git操作（merge, checkout, pull等）でファイルが変わるとUnityが検知→メモリ上の状態で上書きする**ことがある。特に `.prefab` / `.asset` / シーンファイル
- 対策: **git操作前にUnityでシーンを保存（Ctrl+S）してもらう**。または操作後に `git checkout -- <file>` で正しい状態に戻す

# Git操作の注意

- **コミットはユーザーの指示があるまで絶対にしない**
- `git rm --cached` で追跡を外したファイルを `git revert` すると**ディスクからも消える**。Unityが manifest.json を最小構成で再生成し、全パッケージが消える事故になる
- `Packages/manifest.json` は `.gitignore` で除外中。MCP/UniCLIはローカル専用パッケージだが、manifest自体にはチーム共通パッケージ（URP, InputSystem等）も含まれるため、ファイル消失に注意

# UniCLI

ターミナルからUnityエディタを操作するCLIツール。

- **CLIパス:** `C:/Users/nanat/AppData/Local/UniCli/unicli.exe`
- **bash から使う場合:** `export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"` してから `unicli` を実行
- **Unity側パッケージ:** `com.yucchiy.unicli-server`（manifest.json に登録済み）
- Unityエディタが起動中でないとコマンドは実行できない

## UniCLI 使い方

```bash
unicli exec <Command.Name> [--arg value]  # Unityコマンド実行
unicli commands                            # 使えるコマンド一覧（引数も表示される）
unicli status                              # 接続確認
```

# MCP (UnityMCP) の切断パターン

- MCPはWebSocket経由でUnityと通信。**Unityの再起動・ドメインリロード・長時間無通信で切断される**
- 切断されるとMCPツールが会話中にアンロードされ `No such tool available` になる
- **復旧手順**: `ToolSearch` で `unity mcp` を検索 → ツールが再ロードされる
- **フォールバック**: MCPが使えないときはUniCLI（named pipe接続で安定）を使う
- 検証: `claude mcp list` でConnected確認、ToolSearchでツール取得を試す

# 誤認していたこと（失敗ログ）

- **context7は読み取り専用** — 登録APIは存在しない。「context7で登録する」は不可能。
- **`unicli exec -h` は動かない** — `Unknown command` エラー。引数を調べるには `unicli commands` を使う。
- **UniCLIは115コマンドある** — GameObject, Scene, Prefab, Material, Animation, PlayMode, Build, Console, Profiler, TestRunner, Eval, PackageManager等。MCP toolsと重複する機能が多いが、UniCLIはシェルから直接叩ける。

**`Packages/manifest.json` と `packages-lock.json` はgitで追跡しない（.gitignoreで除外済み）。**

- UniCLI (`com.yucchiy.unicli-server`) と Coplay MCP (`com.coplaydev.unity-mcp`) はローカル開発専用
- 他メンバーのPCにはこれらが不要で、git追跡するとパッケージ解決エラーになる
- 各開発者がローカルで自分のmanifest.jsonを管理する
- **mainブランチにmanifest.jsonの変更をpushしないこと**
