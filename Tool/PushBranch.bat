@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

REM カラー出力用の関数を定義
call :DEFINE_COLORS

call :LOG_INFO "=============================================="
call :LOG_INFO "     ブランチプッシュツール                   "
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
REM 現在のブランチを取得
REM ========================================
set "CURRENT_BRANCH="
for /f "tokens=*" %%a in ('git symbolic-ref --short HEAD 2^>nul') do set "CURRENT_BRANCH=%%a"
if not defined CURRENT_BRANCH (
    call :LOG_ERROR "エラー: detached HEAD 状態です。"
    call :LOG_ERROR "SwitchBranch.bat でブランチを選択してください。"
    pause
    exit /b 1
)
call :LOG_INFO "現在のブランチ: !CURRENT_BRANCH!"

REM ========================================
REM main への直接プッシュ警告
REM ========================================
if "!CURRENT_BRANCH!"=="main" (
    echo.
    call :LOG_ERROR "=========================================="
    call :LOG_ERROR " 警告: main ブランチに直接プッシュしようとしています！"
    call :LOG_ERROR "=========================================="
    call :LOG_ERROR "通常は feature ブランチで作業し、PR 経由で"
    call :LOG_ERROR "develop にマージしてください。"
    echo.
    set /p "CONFIRM=%ESC%[93m[確認]%ESC%[0m 本当に main にプッシュしますか？ (Y/N): "
    if /I not "!CONFIRM!"=="Y" (
        call :LOG_INFO "プッシュをキャンセルしました。"
        pause
        exit /b 0
    )
)

REM ========================================
REM develop への直接プッシュ注意
REM ========================================
if "!CURRENT_BRANCH!"=="develop" (
    echo.
    call :LOG_INFO "注意: develop ブランチに直接プッシュします。"
    call :LOG_INFO "通常は feature ブランチで作業し、PR 経由でマージしてください。"
    echo.
)

REM ========================================
REM 未コミット変更の自動コミット
REM ========================================
for /f "tokens=*" %%a in ('git status --porcelain 2^>nul') do set "HAS_CHANGES=1"
if defined HAS_CHANGES (
    call :LOG_INFO "未コミットの変更を自動コミットしています..."
    git add -A
    git commit -m "Auto-commit before push: %date% %time%"
    IF !ERRORLEVEL! NEQ 0 (
        call :LOG_ERROR "警告: 自動コミットに失敗しました。"
    ) else (
        call :LOG_SUCCESS "変更を自動コミットしました。"
    )
)

REM ========================================
REM プッシュ実行
REM ========================================
call :LOG_INFO "ブランチ !CURRENT_BRANCH! をプッシュしています..."
git push -u origin "!CURRENT_BRANCH!"
IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: プッシュに失敗しました。"
    call :LOG_ERROR "インターネット接続を確認してください。"
    call :LOG_ERROR "Fetch.bat でリモートの変更を取り込んでから再度お試しください。"
    pause
    exit /b 1
)

echo.
call :LOG_SUCCESS "プッシュが完了しました！"

REM ========================================
REM feature ブランチの場合は PR URL を表示
REM ========================================
echo !CURRENT_BRANCH! | findstr /B "feature/" >nul 2>&1
IF !ERRORLEVEL! EQU 0 (
    REM リモートURLからPR作成URLを生成
    for /f "tokens=*" %%a in ('git remote get-url origin 2^>nul') do set "REMOTE_URL=%%a"
    if defined REMOTE_URL (
        REM .git サフィックスを除去
        SET "REPO_URL=!REMOTE_URL:.git=!"
        REM git@github.com: を https://github.com/ に変換
        SET "REPO_URL=!REPO_URL:git@github.com:=https://github.com/!"
        echo.
        call :LOG_INFO "=============================================="
        call :LOG_INFO "  次のステップ: GitHub で PR を作成してください"
        call :LOG_INFO "=============================================="
        call :LOG_INFO "PR 作成 URL:"
        echo   !REPO_URL!/compare/develop...!CURRENT_BRANCH!
        call :LOG_INFO "上の URL をブラウザで開いて PR を作成できます。"
    )
)

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
