@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

REM カラー出力用の関数を定義
call :DEFINE_COLORS

call :LOG_INFO "=============================================="
call :LOG_INFO "     Unityプロジェクト自動更新ツール 開始      "
call :LOG_INFO "=============================================="
call :LOG_INFO "開始日時: %date% %time%"

REM リポジトリルートを取得（tool/ の親フォルダ）
for %%i in ("%~dp0..") do SET "REPO_ROOT=%%~fi\"

REM ========================================
REM giturl.ini 廃止警告
REM ========================================
IF EXIST "!REPO_ROOT!giturl.ini" (
    call :LOG_ERROR "=========================================="
    call :LOG_ERROR " 警告: giturl.ini は廃止されました"
    call :LOG_ERROR "=========================================="
    call :LOG_ERROR "トークンを平文で保存するのはセキュリティ上危険です。"
    call :LOG_ERROR "Git Credential Manager による認証に移行してください。"
    call :LOG_ERROR "詳しくは つかいかた.txt を参照してください。"
    call :LOG_ERROR "giturl.ini を削除しても動作に影響はありません。"
    call :LOG_ERROR "=========================================="
    echo.
)

REM ========================================
REM Gitコマンドが利用可能か確認
REM ========================================
where git >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: Gitがインストールされていないか、PATHに含まれていません。"
    call :LOG_ERROR "Gitをインストールするか、環境変数PATHにGitの実行ファイルのパスを追加してください。"
    pause
    exit /b 1
)

REM ========================================
REM Git Credential Manager の確認・設定
REM ========================================
for /f "tokens=*" %%a in ('git config --global credential.helper 2^>nul') do set "CRED_HELPER=%%a"
if not defined CRED_HELPER (
    call :LOG_INFO "Git Credential Manager を設定しています..."
    git config --global credential.helper manager
    IF %ERRORLEVEL% NEQ 0 (
        call :LOG_ERROR "警告: Credential Helper の設定に失敗しました。"
        call :LOG_ERROR "Git for Windows を再インストールしてください。"
    ) else (
        call :LOG_SUCCESS "Credential Manager を設定しました。"
        call :LOG_INFO "初回の fetch/push 時に認証ダイアログが表示されます。"
    )
)

REM ========================================
REM プロジェクトのルートディレクトリに移動
REM ========================================
cd /d "!REPO_ROOT!"

REM .gitディレクトリの存在を確認
IF NOT EXIST ".git" (
    call :LOG_ERROR "エラー: .git ディレクトリが見つかりません。"
    call :LOG_ERROR "このフォルダは Git リポジトリではありません。"
    call :LOG_ERROR "まず以下のコマンドでリポジトリをクローンしてください:"
    call :LOG_ERROR "  git clone https://github.com/ユーザー名/リポジトリ名.git"
    pause
    exit /b 1
)

REM ========================================
REM リモートURLの確認
REM ========================================
for /f "tokens=*" %%a in ('git remote get-url origin 2^>nul') do set "REMOTE_URL=%%a"
if not defined REMOTE_URL (
    call :LOG_ERROR "エラー: リモート origin が設定されていません。"
    call :LOG_ERROR "以下のコマンドでリモートを設定してください:"
    call :LOG_ERROR "  git remote add origin https://github.com/ユーザー名/リポジトリ名.git"
    pause
    exit /b 1
)
call :LOG_PATH "リモートURL:" "%REMOTE_URL%"

REM ========================================
REM detached HEAD チェック
REM ========================================
for /f "tokens=*" %%a in ('git symbolic-ref --short HEAD 2^>nul') do set "CURRENT_BRANCH=%%a"
if not defined CURRENT_BRANCH (
    call :LOG_ERROR "エラー: detached HEAD 状態です。"
    call :LOG_ERROR "SwitchBranch.bat でブランチを選択してください。"
    pause
    exit /b 1
)
call :LOG_INFO "現在のブランチ: !CURRENT_BRANCH!"

REM ========================================
REM 自動コミット → fetch → rebase
REM ========================================
call :LOG_INFO "変更をステージングしています..."
git add -A

call :LOG_INFO "変更をコミットしています..."
git commit -m "Auto-commit before update: %date% %time%"

call :LOG_INFO "リモートの変更を取得しています..."
git fetch origin
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: リモートの変更の取得に失敗しました。"
    call :LOG_ERROR "インターネット接続を確認してください。"
    call :LOG_ERROR "初回実行時は認証ダイアログが表示されます。"
    pause
    exit /b 1
)

REM ========================================
REM リモートにブランチが存在するか確認
REM ========================================
git rev-parse --verify "origin/!CURRENT_BRANCH!" >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_INFO "リモートにブランチ !CURRENT_BRANCH! が存在しません（ローカル専用ブランチ）。"
    call :LOG_INFO "pull をスキップします。"
    goto :SKIP_PULL
)

call :LOG_INFO "ローカルの変更をリベースしています（!CURRENT_BRANCH!）..."
git pull --rebase origin !CURRENT_BRANCH!
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "警告: リベースでコンフリクトが発生しました。"
    call :LOG_ERROR "コンフリクトを解決して続行してください。"
    pause
    exit /b 1
)
:SKIP_PULL

REM ========================================
REM Git LFS
REM ========================================
call :LOG_INFO "LFSファイルを取得しています..."
git lfs pull
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "警告: Git LFSファイルの取得に失敗しました。"
    call :LOG_ERROR "Git LFSがインストールされているか確認してください。"
)

call :LOG_SUCCESS "更新が完了しました！"
call :LOG_INFO "現在のバージョン:"
git log -1 --pretty=format:"  %%h - %%an, %%ar : %%s"
echo.

call :LOG_INFO "終了日時: %date% %time%"
exit /b 0

REM ========================================
REM カラー出力用の関数定義
REM ========================================
:DEFINE_COLORS
for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
  set "ESC=%%b"
)
exit /b 0

:LOG_INFO
echo %ESC%[94m[情報]%ESC%[0m %~1
exit /b 0

:LOG_ERROR
echo %ESC%[91m[エラー]%ESC%[0m %~1
exit /b 0

:LOG_SUCCESS
echo %ESC%[92m[成功]%ESC%[0m %~1
exit /b 0

:LOG_PATH
echo %ESC%[94m[情報]%ESC%[0m %~1 %~2
exit /b 0
