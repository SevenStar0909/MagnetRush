@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

REM カラー出力用の関数を定義
call :DEFINE_COLORS

call :LOG_INFO "=============================================="
call :LOG_INFO "     ブランチステータス確認ツール             "
call :LOG_INFO "=============================================="
echo.

REM リポジトリルートを取得（Tool/ の親フォルダ）
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
REM リモート情報の更新（読み取り専用）
REM ========================================
call :LOG_INFO "リモートの情報を取得しています..."
git fetch origin >nul 2>&1

REM ========================================
REM 現在のブランチ
REM ========================================
set "CURRENT_BRANCH="
for /f "tokens=*" %%a in ('git symbolic-ref --short HEAD 2^>nul') do set "CURRENT_BRANCH=%%a"
if not defined CURRENT_BRANCH set "CURRENT_BRANCH=(detached HEAD)"

echo.
echo   %ESC%[92m現在のブランチ: !CURRENT_BRANCH!%ESC%[0m
echo.

REM ========================================
REM 直近3件のコミット
REM ========================================
call :LOG_INFO "--- 直近のコミット ---"
git log -3 --pretty=format:"  %%h - %%an, %%ar : %%s" 2>nul
echo.
echo.

REM ========================================
REM 変更ファイル数
REM ========================================
SET /A STAGED=0
SET /A UNSTAGED=0
SET /A UNTRACKED=0

for /f "tokens=*" %%a in ('git diff --cached --name-only 2^>nul') do SET /A STAGED+=1
for /f "tokens=*" %%a in ('git diff --name-only 2^>nul') do SET /A UNSTAGED+=1
for /f "tokens=*" %%a in ('git ls-files --others --exclude-standard 2^>nul') do SET /A UNTRACKED+=1

call :LOG_INFO "--- 変更ファイル ---"
echo   ステージ済み:    !STAGED! 件
echo   未ステージ:      !UNSTAGED! 件
echo   未追跡:          !UNTRACKED! 件
echo.

REM ========================================
REM リモートとの ahead/behind
REM ========================================
call :LOG_INFO "--- リモートとの差分 ---"

REM リモートにブランチが存在するか確認
git rev-parse --verify "origin/!CURRENT_BRANCH!" >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo   リモートにブランチ !CURRENT_BRANCH! が存在しません（ローカル専用）
) ELSE (
    SET "AHEAD=0"
    SET "BEHIND=0"
    for /f %%a in ('git rev-list --count "origin/!CURRENT_BRANCH!..HEAD" 2^>nul') do set "AHEAD=%%a"
    for /f %%a in ('git rev-list --count "HEAD..origin/!CURRENT_BRANCH!" 2^>nul') do set "BEHIND=%%a"

    if "!AHEAD!"=="0" if "!BEHIND!"=="0" (
        echo   %ESC%[92mリモートと同期しています%ESC%[0m
    )
    if !AHEAD! GTR 0 echo   %ESC%[93m↑ !AHEAD! コミット先行（未プッシュ）%ESC%[0m
    if !BEHIND! GTR 0 echo   %ESC%[93m↓ !BEHIND! コミット遅れ（Fetch.bat で更新してください）%ESC%[0m
)

echo.
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
