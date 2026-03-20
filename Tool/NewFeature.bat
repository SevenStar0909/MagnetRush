@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

REM カラー出力用の関数を定義
call :DEFINE_COLORS

call :LOG_INFO "=============================================="
call :LOG_INFO "     フィーチャーブランチ作成ツール           "
call :LOG_INFO "=============================================="
echo.

REM リポジトリルートを取得（tool/ の親フォルダ）
for %%i in ("%~dp0..") do SET "REPO_ROOT=%%~fi\"
cd /d "!REPO_ROOT!"

REM Gitコマンドが利用可能か確認
where git >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: Gitがインストールされていないか、PATHに含まれていません。"
    pause
    exit /b 1
)

REM .gitディレクトリの存在を確認
IF NOT EXIST ".git" (
    call :LOG_ERROR "エラー: .git ディレクトリが見つかりません。"
    pause
    exit /b 1
)

REM ========================================
REM develop ブランチの存在確認
REM ========================================
call :LOG_INFO "リモートの情報を取得しています..."
git fetch origin >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: リモートの情報取得に失敗しました。"
    call :LOG_ERROR "インターネット接続を確認してください。"
    pause
    exit /b 1
)

git rev-parse --verify "origin/develop" >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: リモートに develop ブランチが存在しません。"
    call :LOG_ERROR "先に GitHub で develop ブランチを作成してください。"
    pause
    exit /b 1
)

REM ========================================
REM フィーチャー名の入力
REM ========================================
echo.
call :LOG_INFO "作成するフィーチャーの名前を入力してください。"
call :LOG_INFO "例: add-new-enemy, fix-ui-layout, update-title-screen"
call :LOG_INFO "（スペースは使えません。英数字とハイフンを使ってください）"
echo.
set /p "FEATURE_NAME=%ESC%[93m[入力]%ESC%[0m フィーチャー名: "

REM 空入力チェック
if not defined FEATURE_NAME (
    call :LOG_ERROR "エラー: フィーチャー名が入力されていません。"
    pause
    exit /b 1
)

REM スペースチェック
echo !FEATURE_NAME! | findstr /C:" " >nul 2>&1
IF %ERRORLEVEL% EQU 0 (
    call :LOG_ERROR "エラー: フィーチャー名にスペースは使えません。"
    call :LOG_ERROR "ハイフン（-）で単語を繋げてください。例: my-new-feature"
    pause
    exit /b 1
)

SET "BRANCH_NAME=feature/!FEATURE_NAME!"

REM 同名ブランチの存在確認
git rev-parse --verify "!BRANCH_NAME!" >nul 2>&1
IF %ERRORLEVEL% EQU 0 (
    call :LOG_ERROR "エラー: ブランチ !BRANCH_NAME! は既に存在します。"
    call :LOG_ERROR "別の名前を指定するか、SwitchBranch.bat で切り替えてください。"
    pause
    exit /b 1
)

REM ========================================
REM 未コミット変更の自動コミット
REM ========================================
for /f "tokens=*" %%a in ('git status --porcelain 2^>nul') do set "HAS_CHANGES=1"
if defined HAS_CHANGES (
    call :LOG_INFO "未コミットの変更を自動コミットしています..."
    git add -A
    git commit -m "Auto-commit before branch switch: %date% %time%"
    IF !ERRORLEVEL! NEQ 0 (
        call :LOG_ERROR "警告: 自動コミットに失敗しました。"
    ) else (
        call :LOG_SUCCESS "変更を自動コミットしました。"
    )
)

REM ========================================
REM ブランチ作成
REM ========================================
call :LOG_INFO "ブランチ !BRANCH_NAME! を origin/develop から作成しています..."
git checkout -b "!BRANCH_NAME!" origin/develop
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: ブランチの作成に失敗しました。"
    pause
    exit /b 1
)

echo.
call :LOG_SUCCESS "=============================================="
call :LOG_SUCCESS "  ブランチ !BRANCH_NAME! を作成しました！"
call :LOG_SUCCESS "=============================================="
echo.
call :LOG_INFO "次のステップ:"
call :LOG_INFO "  1. Unity で作業を行う"
call :LOG_INFO "  2. Fetch.bat で最新を取り込む"
call :LOG_INFO "  3. PushBranch.bat で作業をプッシュ"
call :LOG_INFO "  4. GitHub で develop 宛の PR を作成"
echo.
pause
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
