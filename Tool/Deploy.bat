@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

REM カラー出力用の関数を定義
call :DEFINE_COLORS

REM ビルド開始時の情報表示
call :LOG_INFO "=============================================="
call :LOG_INFO "         Unity ビルド自動化ツール 開始         "
call :LOG_INFO "=============================================="
call :LOG_INFO "開始日時: %date% %time%"

REM リポジトリルートを取得（Tool/ の親フォルダ）
for %%i in ("%~dp0..") do SET "REPO_ROOT=%%~fi\"
REM バッチファイルのディレクトリパスを取得
SET "BATCH_DIR=%~dp0"

REM 設定ファイルのパスを絶対パスで指定
SET "PATH_INI=%BATCH_DIR%path.ini"

IF NOT EXIST "!PATH_INI!" (
    call :LOG_ERROR "エラー: %PATH_INI% ファイルが見つかりません。"
    call :LOG_ERROR "path.ini.template をコピーして path.ini を作成してください。"
    call :LOG_ERROR "1行目: Unity.exeのフルパス"
    call :LOG_ERROR "2行目(省略可): Unityプロジェクトフォルダのフルパス"
    pause
    exit /b 1
)

REM ファイルから行を読み込む
SET LINE_NUM=0
FOR /F "usebackq tokens=* delims=" %%a IN ("%PATH_INI%") DO (
    SET /A LINE_NUM+=1
    IF !LINE_NUM! EQU 1 SET "UNITY_PATH=%%a"
    IF !LINE_NUM! EQU 2 SET "PROJECT_PATH=%%a"
)

IF "%UNITY_PATH%"=="" (
    call :LOG_ERROR "エラー: Unityエディタのパスが path.ini に指定されていません。"
    pause
    exit /b 1
)

IF "%PROJECT_PATH%"=="" (
    call :LOG_INFO "path.ini に2行目が未指定のため、Unityプロジェクトを自動検出します..."
    SET "DETECT_COUNT=0"
    SET "DETECTED_PATH="
    FOR /D %%d IN ("!REPO_ROOT!*") DO (
        IF EXIST "%%d\Assets\" IF EXIST "%%d\ProjectSettings\" (
            SET /A DETECT_COUNT+=1
            SET "DETECTED_PATH=%%d"
        )
    )
    IF !DETECT_COUNT! EQU 0 (
        call :LOG_ERROR "エラー: Unityプロジェクトフォルダが見つかりませんでした。"
        call :LOG_ERROR "Assets\ と ProjectSettings\ を含むフォルダがリポジトリ直下にありません。"
        call :LOG_ERROR "path.ini の2行目にプロジェクトフォルダのフルパスを指定してください。"
        pause
        exit /b 1
    )
    IF !DETECT_COUNT! GTR 1 (
        call :LOG_ERROR "エラー: Unityプロジェクトフォルダが複数見つかりました（!DETECT_COUNT!個）。"
        call :LOG_ERROR "path.ini の2行目にプロジェクトフォルダのフルパスを明記してください。"
        pause
        exit /b 1
    )
    SET "PROJECT_PATH=!DETECTED_PATH!"
    call :LOG_SUCCESS "自動検出: !PROJECT_PATH!"
)

REM 引用符の処理
SET "UNITY_PATH_CLEAN=%UNITY_PATH:"=%"
SET "PROJECT_PATH_CLEAN=%PROJECT_PATH:"=%"

REM Unity.exeの存在確認
IF NOT EXIST "!UNITY_PATH_CLEAN!" (
    call :LOG_ERROR "エラー: Unity.exeが見つかりません: !UNITY_PATH_CLEAN!"
    call :LOG_ERROR "path.ini の1行目を確認してください。"
    pause
    exit /b 1
)

REM プロジェクトフォルダの存在確認
IF NOT EXIST "!PROJECT_PATH_CLEAN!" (
    call :LOG_ERROR "エラー: プロジェクトフォルダが見つかりません: !PROJECT_PATH_CLEAN!"
    call :LOG_ERROR "path.ini の2行目を確認してください。"
    pause
    exit /b 1
)

REM 情報表示
call :LOG_PATH "Unityエディタ:" "!UNITY_PATH_CLEAN!"
call :LOG_PATH "プロジェクト:" "!PROJECT_PATH_CLEAN!"

REM Git ブランチ・コミット情報の表示
where git >nul 2>&1
IF !ERRORLEVEL! EQU 0 (
    IF EXIST "!REPO_ROOT!.git" (
        for /f "tokens=*" %%a in ('git -C "!REPO_ROOT!." symbolic-ref --short HEAD 2^>nul') do set "BUILD_BRANCH=%%a"
        if not defined BUILD_BRANCH set "BUILD_BRANCH=detached HEAD"
        for /f "tokens=*" %%a in ('git -C "!REPO_ROOT!." rev-parse --short HEAD 2^>nul') do set "BUILD_COMMIT=%%a"
        call :LOG_PATH "ブランチ:" "!BUILD_BRANCH!"
        call :LOG_PATH "コミット:" "!BUILD_COMMIT!"
    )
)

REM ログファイルのパスを設定
SET "LOG_FILE=!REPO_ROOT!build_log.txt"
call :LOG_INFO "ログファイル: %LOG_FILE%"
call :LOG_INFO "ビルド処理を開始します..."

REM コマンドライン引数
SET "ARGS=-batchmode -quit -projectPath "!PROJECT_PATH_CLEAN!" -executeMethod AutoBuildSystem.PerformAutoBuild -logFile "!LOG_FILE!""

REM Unityを実行
"!UNITY_PATH_CLEAN!" !ARGS!

IF %ERRORLEVEL% NEQ 0 (
    call :LOG_ERROR "エラー: Unityの実行に失敗しました（エラーコード: %ERRORLEVEL%）"
    call :LOG_PATH "Unity実行パス:" "!UNITY_PATH_CLEAN!"
    call :LOG_INFO "引数: !ARGS!"

    REM ログファイルからエラー詳細を表示
    IF EXIST "%LOG_FILE%" (
        call :LOG_ERROR "--- ビルドログからエラーを検出 ---"
        type "%LOG_FILE%" | findstr /C:"error CS" /C:"Build failed" /C:"CompilerErrorException" /I
    )
    pause
    exit /b 1
)

REM ビルド結果をログから確認
IF EXIST "%LOG_FILE%" (
    type "%LOG_FILE%" | findstr /C:"error CS" /I > nul
    IF !ERRORLEVEL! EQU 0 (
        call :LOG_ERROR "ビルドエラーが発生しました:"
        type "%LOG_FILE%" | findstr /C:"error CS" /I
        pause
        exit /b 1
    )

    type "%LOG_FILE%" | findstr /C:"CompilerErrorException" /I > nul
    IF !ERRORLEVEL! EQU 0 (
        call :LOG_ERROR "コンパイルエラーが発生しました"
        pause
        exit /b 1
    )

    type "%LOG_FILE%" | findstr /C:"Build failed" /I > nul
    IF !ERRORLEVEL! EQU 0 (
        call :LOG_ERROR "ビルドが失敗しました。詳細は %LOG_FILE% を確認してください。"
        pause
        exit /b 1
    )
)

call :LOG_SUCCESS "ビルドが正常に完了しました。"
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
