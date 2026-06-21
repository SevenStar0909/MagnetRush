using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class SimpleTutorial : MonoBehaviour
{
    [Header("=== 必須オブジェクト登録 ===")]
    public TextMeshProUGUI tutorialText;
    public GameObject commentBoardObject;

    [Header("=== 全体共通設定 ===")]
    public float defaultTextSpeed = 0.05f; // 👈【追加】すべてのセリフで共通の流れる速さ（1箇所で一括管理）

    [System.Serializable]
    public class TutorialStepData
    {
        [TextArea(3, 10)]
        public string message = "ここにセリフを入力";
        public float messageWaitTime = 2.0f;  // 出終わったあとの表示（待ち）時間
        public bool hideBoardAfterThis = false;
    }

    [Header("=== 💬 チュートリアルセリフ一覧（数を自由に増減できます） ===")]
    [Header("===チュートリアルステージ開始後すぐ ===")]
    public List<TutorialStepData> phase1_Opening;         // 元のセリフ 1〜4
    [Header("=== 図１の部分まで進んだら ===")]
    public List<TutorialStepData> phase2_Zone1Reached;    // 元のセリフ 5〜8
    [Header("=== No.8表示後にRTボタンが押されたら ===")]
    public List<TutorialStepData> phase3_CrateShot;       // 元のセリフ 9〜11
    [Header("=== No.11表示後にRBボタンが押されたら ===")]
    public List<TutorialStepData> phase4_MagnetSwitched;  // 元のセリフ 12〜13
    [Header("=== No.13表示後にRTボタンが押されたら ===")]
    public List<TutorialStepData> phase5_CratesConnected; // 元のセリフ 14〜17
    [Header("=== 図2の部分まで進んだら ===")]
    public List<TutorialStepData> phase6_WallReached;     // 元のセリフ 18〜19
    [Header("=== No.19表示後にLBボタンが押されたら ===")]
    public List<TutorialStepData> phase7_WallClimbed;     // 元のセリフ 20〜22
    [Header("=== 図3の部分まで進んだら ===")]
    public List<TutorialStepData> phase8_EnemySpotted;    // 元のセリフ 23〜27
    [Header("=== 戦闘開始中に表示 ===")]
    public List<TutorialStepData> phase9_BattleStart;     // 元のセリフ 28〜29
    [Header("=== 敵を撃破したら ===")]
    public List<TutorialStepData> phase10_EnemyDefeated;  // 元のセリフ 30
    [Header("=== 図4の部分まで進んだら ===")]
    public List<TutorialStepData> phase11_TurretSpotted;  // 元のセリフ 31〜32
    [Header("=== 図5の部分まで進んだら ===")]
    public List<TutorialStepData> phase12_Outro;          // 元のセリフ 33〜34

    [Header("=== ゲーム進行用の設定 ===")]
    public Transform playerTransform;     // 👈 プレイヤーのTransform
    // 監視用のクレート3つをインスペクターで登録しておく
    public GameObject[] tutorialCrates;
    public GameObject tutorialEnemy;

    [Header("=== 🎥 カメラズーム用の変数 ===")]
    // ==== 🎥 カメラズーム用の変数を追加 ====
    public Camera mainCamera;       // シーン上のメインカメラを登録する用
    public float zoomFOV = 30f;     // ズームした時の視野角（小さいほどドアップになります）
    private float defaultFOV;       // 元のカメラの視野角を保存しておく変数
    private bool isZooming = false; // 今ズーム中かどうかのフラグ

    private int currentStep = 0;

    /// <summary>
    /// 外部のゾーンコライダーから、プレイヤーが触れた瞬間に呼び出される関数
    /// </summary>
    public void OnPlayerEnterZone(int zoneNumber)
    {
        // 【仕様書 ③】図1（ゾーン1）への移動待ち
        if (zoneNumber == 1 && currentStep == 3) currentStep = 4;

        // 【仕様書 ⑪】図2（ゾーン2）への移動待ち
        if (zoneNumber == 2 && currentStep == 11) currentStep = 12;

        // 【仕様書 ⑭】図3（ゾーン3）への移動待ち
        if (zoneNumber == 3 && currentStep == 14) currentStep = 15;

        // 【仕様書 ⑰】図4（ゾーン4）への移動待ち
        if (zoneNumber == 4 && currentStep == 17) currentStep = 18;

        // 【仕様書 ⑱】図5（ゾーン5）への移動待ち
        if (zoneNumber == 5 && currentStep == 18) currentStep = 19;
    }

    void Start()
    {
        //ステージ開始：すべての操作をロック（窓口）
        SetPlayerLockAll(true);

        // チュートリアルのレールの開始！
        StartCoroutine(TutorialSequenceRoutine());

        if (mainCamera != null)
        {
            defaultFOV = mainCamera.fieldOfView;
        }
    }

    void Update()
    {
        //３つのクレートのどれか一つに磁力が付与されたか判定
        OnCrateMagnetApplied();

        //磁力切り替え（RB）が実行されたか判定
        OnMagnetSwitched();

        //磁力の引き寄せ（反発）が起こったかを判定
        OnMagnetConnect();

        //プレイヤー自身に磁力が付与されたか判定
        OnMagnetAppliedToSelf();

        //カメラの自動ズーム演出処理
        CameraZoom();

        //敵（ロボット）の死亡判定
        OneEnemyDead();
    }

    // チュートリアルの進行管理（一本道のレール）
    private IEnumerator TutorialSequenceRoutine()
    {
        // --- フェーズ 1: 開幕のおしゃべり ---
        currentStep = 2;
        yield return StartCoroutine(PlayPhaseRoutine(phase1_Opening));

        // --- 4つのメッセージが終わったら、移動・カメラ・ジャンプを解除 ---
        currentStep = 3;
        //tutorialText.text = "道なりに進む。"; // 次の目標を画面に残しておく
        SetPlayerMovementLock(false); // 移動系のロックを解除

        //判定待ちプレイヤーが図1のエリアに入るまで、ここでプログラムを一時停止して待つ
        while (currentStep == 3)
        {
            yield return null;
        }

        currentStep = 4;
        // --- フェーズ 2: 図1付近に到達 ---
        yield return StartCoroutine(PlayPhaseRoutine(phase2_Zone1Reached));

        currentStep = 5;
        SetPlayerShootingLock(false); // 射撃のロックを解除

        // ここでクレートに磁極が付くのを待つ（currentStep を 5 にしてから待機する想定）
        while (currentStep == 5)
        {
            yield return null;
        }

        // --- フェーズ 3: クレート射撃後 ---
        currentStep = 6;
        // 一度解除した射撃はそのまま使えるようにする（教えるための再ロックはしない）
        yield return StartCoroutine(PlayPhaseRoutine(phase3_CrateShot));

        // 磁力切り替え解除してRB待ち
        currentStep = 7;
        SetPlayerSwitchMagnetLock(false); // 磁力切り替え解除の窓口

        while (currentStep == 7)
        {
            yield return null;
        }

        // --- フェーズ 4: RBボタンでの磁力切り替え後 ---
        currentStep = 8;
        // 一度解除した磁極切替はそのまま使えるようにする（教えるための再ロックはしない）
        yield return StartCoroutine(PlayPhaseRoutine(phase4_MagnetSwitched));

        // 射撃ロック解除して引き合い待ち
        currentStep = 9;
        SetPlayerShootingLock(false); // 射撃ロック

        while (currentStep == 9)
        {
            yield return null;
        }

        // --- フェーズ 5: クレート結合後 ---
        currentStep = 10;
        // 射撃・磁極切替は解除済みのまま（再ロックしない）
        yield return StartCoroutine(PlayPhaseRoutine(phase5_CratesConnected));

        currentStep = 11;
        // 仕様⑩: 自分への磁力(LB)以外のすべての機能を解除（リロードもここで解禁）
        SetPlayerLockAll(false);
        SetPlayerSelfMagnetLock(true);

        while (currentStep == 11)
        {
            yield return null;
        }

        // --- フェーズ 6: 高い壁の前に到着 ---
        currentStep = 12;
        // 解除済みの機能はそのまま（図2でも再ロックしない）
        yield return StartCoroutine(PlayPhaseRoutine(phase6_WallReached));

        // 仕様⑫: メッセージ19「LBを押してみて」の表示と同時に自分磁力(LB)を解禁し、検知も有効化する
        SetPlayerSelfMagnetLock(false);
        currentStep = 13;

        while (currentStep == 13)
        {
            yield return null;
        }

        // --- フェーズ 7: 自分に磁力付与後 〜 壁超え待ち ---
        currentStep = 14;
        yield return StartCoroutine(PlayPhaseRoutine(phase7_WallClimbed));      //22
        SetPlayerLockAll(false);

        while (currentStep == 14)
        {
            yield return null;
        }

        // --- フェーズ 8: 壁の向こう（ロボット発見） ---
        currentStep = 15;
        SetPlayerLockAll(true);
        isZooming = true;

        yield return StartCoroutine(PlayPhaseRoutine(phase8_EnemySpotted));

        isZooming = false;

        // --- フェーズ 9: 戦闘開始のアドバイス ---
        currentStep = 16;
        SetPlayerLockAll(false);
        yield return StartCoroutine(PlayPhaseRoutine(phase9_BattleStart));

        // 敵の死亡待ち
        while (currentStep == 16)
        {
            yield return null;
        }

        // --- フェーズ 10: 敵撃破後 ---
        currentStep = 17;
        yield return StartCoroutine(PlayPhaseRoutine(phase10_EnemyDefeated));

        // ゾーン4（タレット前）への移動待ち
        while (currentStep == 17)
        {
            yield return null;
        }

        // --- フェーズ 11: タレット発見 ---
        currentStep = 18;
        yield return StartCoroutine(PlayPhaseRoutine(phase11_TurretSpotted));

        // ゾーン5（ゴールエリア）への移動待ち
        while (currentStep == 18)
        {
            yield return null;
        }

        // --- フェーズ 12: ゴール・シミュレーション終了 ---
        currentStep = 19;
        yield return StartCoroutine(PlayPhaseRoutine(phase12_Outro));
    }

    //【追加】1つのフェーズ内のセリフを連続再生する汎用関数
    private IEnumerator PlayPhaseRoutine(List<TutorialStepData> phaseSteps)
    {
        if (phaseSteps == null || phaseSteps.Count == 0) yield break;

        foreach (var step in phaseSteps)
        {
            // 💡 セリフが始まるので、ボードを表示する（消えていた場合自動で復活します）
            if (commentBoardObject != null) commentBoardObject.SetActive(true);

            // タイピング演出へデータを渡して再生
            yield return StartCoroutine(TypeAndAutoAdvance(step.message, defaultTextSpeed, step.messageWaitTime));

            // 💡 待ち時間が終わった後、もし「隠す」チェックがONならボードを非表示にする
            if (step.hideBoardAfterThis)
            {
                if (commentBoardObject != null) commentBoardObject.SetActive(false);
                if (tutorialText != null) tutorialText.text = "";
            }
        }
    }

    // ✍️ リッチテキスト対応・個別速度・時間対応版のタイピング演出
    private IEnumerator TypeAndAutoAdvance(string message, float speed, float waitTime)
    {
        tutorialText.text = message;
        tutorialText.ForceMeshUpdate();

        int totalVisibleCharacters = tutorialText.textInfo.characterCount;
        tutorialText.maxVisibleCharacters = 0;

        for (int i = 0; i <= totalVisibleCharacters; i++)
        {
            tutorialText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(speed);
        }

        yield return new WaitForSeconds(waitTime);
    }

    // 外部から呼ばれる「磁力が付与されたよ」の通知
    private void OnCrateMagnetApplied()
    {
        if (currentStep == 5 && tutorialCrates != null)
        {
            foreach (GameObject crate in tutorialCrates)
            {
                if (crate == null) continue;

                // 相方のスクリプト「Magnetizable」を取得
                Magnetizable magnet = crate.GetComponent<Magnetizable>();

                // Magnetizableの「IsActive（磁力がONか）」を直接見に行く！
                if (magnet != null && magnet.IsActive)
                {
                    currentStep = 6; // 磁力がついたので次のステップへ！
                    break;
                }
            }
        }
    }

    // 外部から呼ばれる「磁力を切り替えたよ」の通知
    // step7突入時の磁極を基準に取り、実際に切り替わった瞬間だけ進める
    private MagneticPole m_poleBaseline;
    private bool m_poleBaselineSet;

    private void OnMagnetSwitched()
    {
        if (currentStep != 7)
        {
            m_poleBaselineSet = false;
            return;
        }

        var p = Player.Current;
        if (p == null || p.pole == null) return;

        // 生の RB/Q 入力ではなく、実際の磁極変化を見る（他の検知と同じく状態ベース）
        if (!m_poleBaselineSet)
        {
            m_poleBaseline = p.pole.CurrentPole;
            m_poleBaselineSet = true;
            return;
        }

        if (p.pole.CurrentPole != m_poleBaseline)
            currentStep = 8;
    }

    //クレート同士がつながったよ
    private void OnMagnetConnect()
    {
        if(currentStep == 9 && tutorialCrates != null)
    {
            foreach (GameObject crate in tutorialCrates)
            {
                if (crate == null) continue;

                Magnetizable magnet = crate.GetComponent<Magnetizable>();

                // 💡「ForceThisFrame」は、磁力で引っ張られたり弾かれたりしている力（合計）の変数です。
                // 力が0より大きくなった＝引き寄せ（反発）が発生したと判定できます！
                if (magnet != null && magnet.ForceThisFrame > 0f)
                {
                    currentStep = 10;
                    break;
                }
            }
        }
    }

    // 外部から呼ばれる「自分に磁力を付与したよ」の通知
    private void OnMagnetAppliedToSelf()
    {
        if (currentStep != 13) return;

        var pt = GetPlayerTransform();
        if (pt == null) return;

        // プレイヤー自身に付いている「Magnetizable」を取得し、磁力がONになったかをチェック
        Magnetizable playerMagnet = pt.GetComponent<Magnetizable>();
        if (playerMagnet != null && playerMagnet.IsActive)
            currentStep = 14;
    }

    // プレイヤーは GameScene 側（Additive ロード）に居るためシーン参照では繋がらない。
    // Inspector 参照があれば優先し、無ければ Player.Current で実行時に解決する。
    private Transform GetPlayerTransform()
    {
        if (playerTransform != null) return playerTransform;
        if (Player.Current != null) return Player.Current.transform;
        return null;
    }

    // カメラは Cinemachine 制御（_Camera.prefab の CameraSettingsApplier）なので mainCamera を直接回すと競合する。
    // ピボットを動かす CameraSettingsApplier.FocusOn 経由でエネミーに向ける。
    private void CameraZoom()
    {
        var applier = GetCameraApplier();
        if (applier == null) return;

        if (isZooming && tutorialEnemy != null)
            applier.FocusOn(tutorialEnemy.transform, zoomFOV);
        else
            applier.ClearFocus();
    }

    private CameraSettingsApplier m_cameraApplier;
    private CameraSettingsApplier GetCameraApplier()
    {
        if (m_cameraApplier == null) m_cameraApplier = FindAnyObjectByType<CameraSettingsApplier>();
        return m_cameraApplier;
    }

    private void OneEnemyDead()
    {
        if (currentStep == 16)
        {
            // 💡 Unityのルール：オブジェクトが倒されて消滅（Destroy）するか、
            // もしくは非アクティブ（SetActive(false)）になると、自動でここを通過します！
            if (tutorialEnemy == null || !tutorialEnemy.activeInHierarchy)
            {
                currentStep = 17; // ➔ 自動的にステップ17（流石ね、シミュレーションとはいえ…）に進む
            }
        }
    }

    // 機能ロックは _Player.prefab の PlayerAbilityLocker に委譲する。
    // ロックの解除/再ロックはすべてメッセージ進行に合わせてこのコルーチンから行う（コライダー不要）。
    // プレイヤーは GameScene 側（Additive）に居るので、locker は Player.Current 等で実行時解決する。
    private PlayerAbilityLocker m_locker;

    private PlayerAbilityLocker GetLocker()
    {
        if (m_locker != null) return m_locker;
        if (playerTransform != null) m_locker = playerTransform.GetComponentInChildren<PlayerAbilityLocker>(true);
        if (m_locker == null && Player.Current != null) m_locker = Player.Current.abilityLocker;
        if (m_locker == null) m_locker = FindAnyObjectByType<PlayerAbilityLocker>();
        if (m_locker == null) Debug.LogWarning("[Tutorial] PlayerAbilityLocker が見つからない — ロックが効きません");
        return m_locker;
    }

    // 移動・カメラを含む全機能をロック/解除する（仕様①開始時・⑭エネミー遭遇時の全ロック）。
    private void SetPlayerLockAll(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        foreach (PlayerAbilityType ability in System.Enum.GetValues(typeof(PlayerAbilityType)))
            locker.SetLocked(ability, isLock);
    }

    // 仕様③メッセージ4で解除する移動・カメラ・ジャンプ。
    private void SetPlayerMovementLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.Move, isLock);
        locker.SetLocked(PlayerAbilityType.Camera, isLock);
        locker.SetLocked(PlayerAbilityType.Jump, isLock);
    }

    // 射撃グループ＝エイム＋射撃＋リロード（銃の一式。射撃が使えるならリロードも使える）。
    private void SetPlayerShootingLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.Aim, isLock);
        locker.SetLocked(PlayerAbilityType.Fire, isLock);
        locker.SetLocked(PlayerAbilityType.Reload, isLock);
    }

    // 磁極切替（RB）。
    private void SetPlayerSwitchMagnetLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.PoleSwitch, isLock);
    }

    // 自分への磁力付与（LB / セルフファイア）。
    private void SetPlayerSelfMagnetLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.SelfFire, isLock);
    }
}