@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

REM カラー出力用の関数を定義
call :DEFINE_COLORS

call :LOG_INFO "=============================================="
call :LOG_INFO "     ブランチ切り替えツール                   "
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
REM リモート情報の更新
REM ========================================
call :LOG_INFO "リモートの情報を取得しています..."
git fetch origin --prune >nul 2>&1

REM ========================================
REM 現在のブランチを取得
REM ========================================
set "CURRENT_BRANCH="
for /f "tokens=*" %%a in ('git symbolic-ref --short HEAD 2^>nul') do set "CURRENT_BRANCH=%%a"
if not defined CURRENT_BRANCH set "CURRENT_BRANCH=(detached HEAD)"

REM ========================================
REM ブランチ一覧を作成（ローカル + リモート、重複排除）
REM ========================================
SET "COUNT=0"

REM ローカルブランチを追加
for /f "tokens=*" %%a in ('git branch --format="%%(refname:short)" 2^>nul') do (
    SET /A COUNT+=1
    SET "BRANCH_!COUNT!=%%a"
    SET "BRANCH_!COUNT!_SRC=local"
)

REM リモートブランチを追加（ローカルに存在しないもの、HEAD除外）
for /f "tokens=*" %%a in ('git branch -r --format="%%(refname:short)" 2^>nul') do (
    SET "REMOTE_BR=%%a"
    REM origin/HEAD はスキップ
    echo !REMOTE_BR! | findstr /C:"origin/HEAD" >nul 2>&1
    IF !ERRORLEVEL! NEQ 0 (
        REM origin/ プレフィックスを除去してローカル名を取得
        SET "LOCAL_NAME=!REMOTE_BR:origin/=!"
        REM ローカルに既にあるかチェック
        SET "FOUND=0"
        for /L %%i in (1,1,!COUNT!) do (
            if "!BRANCH_%%i!"=="!LOCAL_NAME!" SET "FOUND=1"
        )
        if "!FOUND!"=="0" (
            SET /A COUNT+=1
            SET "BRANCH_!COUNT!=!LOCAL_NAME!"
            SET "BRANCH_!COUNT!_SRC=remote"
        )
    )
)

if !COUNT! EQU 0 (
    call :LOG_ERROR "ブランチが見つかりません。"
    pause
    exit /b 1
)

REM ========================================
REM ブランチ一覧を表示
REM ========================================
echo.
call :LOG_INFO "ブランチ一覧:"
echo.
for /L %%i in (1,1,!COUNT!) do (
    SET "DISPLAY_NAME=!BRANCH_%%i!"
    SET "SUFFIX="
    if "!BRANCH_%%i_SRC!"=="remote" SET "SUFFIX= %ESC%[90m(リモートのみ)%ESC%[0m"
    if "!DISPLAY_NAME!"=="!CURRENT_BRANCH!" (
        echo   %ESC%[92m%%i. !DISPLAY_NAME! * （現在のブランチ）%ESC%[0m
    ) else (
        echo   %%i. !DISPLAY_NAME!!SUFFIX!
    )
)
echo.

REM ========================================
REM 番号入力
REM ========================================
set /p "SELECTION=%ESC%[93m[入力]%ESC%[0m 切り替え先の番号を入力 (1-!COUNT!): "

REM 入力チェック
if not defined SELECTION (
    call :LOG_ERROR "番号が入力されていません。"
    pause
    exit /b 1
)

REM 範囲チェック
if !SELECTION! LSS 1 (
    call :LOG_ERROR "無効な番号です。"
    pause
    exit /b 1
)
if !SELECTION! GTR !COUNT! (
    call :LOG_ERROR "無効な番号です。"
    pause
    exit /b 1
)

SET "TARGET=!BRANCH_%SELECTION%!"
SET "TARGET_SRC=!BRANCH_%SELECTION%_SRC!"

REM 現在のブランチと同じ場合
if "!TARGET!"=="!CURRENT_BRANCH!" (
    call :LOG_INFO "既に !TARGET! ブランチにいます。"
    pause
    exit /b 0
)

REM ========================================
REM 未コミット変更の自動コミット
REM ========================================
set "HAS_CHANGES="
for /f "tokens=*" %%a in ('git status --porcelain 2^>nul') do set "HAS_CHANGES=1"
if defined HAS_CHANGES (
    call :LOG_INFO "未コミットの変更を自動コミットしています..."
    git add -A
    git commit -m "Auto-commit before branch switch: %date% %time%"
    IF !ERRORLEVEL! NEQ 0 (
        call :LOG_INFO "自動コミットできませんでした。スタッシュに退避します..."
        git stash --include-untracked
        IF !ERRORLEVEL! NEQ 0 (
            call :LOG_ERROR "警告: スタッシュにも失敗しました。"
        ) else (
            call :LOG_SUCCESS "変更をスタッシュに退避しました。戻った時に git stash pop で復元できます。"
        )
    ) else (
        call :LOG_SUCCESS "変更を自動コミットしました。"
    )
)

REM ========================================
REM ブランチ切り替え
REM ========================================
if "!TARGET_SRC!"=="remote" (
    call :LOG_INFO "リモートブランチ !TARGET! のトラッキングブランチを作成しています..."
    git checkout -b "!TARGET!" "origin/!TARGET!"
) else (
    git checkout "!TARGET!"
)

IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: ブランチの切り替えに失敗しました。"
    pause
    exit /b 1
)

echo.
call :LOG_SUCCESS "ブランチ !TARGET! に切り替えました！"
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
