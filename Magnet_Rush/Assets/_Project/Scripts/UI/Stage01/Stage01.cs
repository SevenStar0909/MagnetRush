using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Stage01Message : MonoBehaviour
{
    [Header("=== 必須オブジェクト登録 ===")]
    public TextMeshProUGUI stage01Text;
    public GameObject commentBoardObject;

    [Header("=== 🏛️ 連動するアリーナゲート（ここに各エリアのGateを登録） ===")]
    [Tooltip("図1のエリアにある EnemyArenaGate")]
    public EnemyArenaGate zone1Arena;
    [Tooltip("図2のエリアにある EnemyArenaGate")]
    public EnemyArenaGate zone2Arena;
    [Tooltip("図3（ボス部屋）にある EnemyBossArenaGate（あれば登録、なければ空でOK）")]
    public EnemyBossArenaGate zone3Arena;

    [Header("=== 全体共通設定 ===")]
    public float defaultTextSpeed = 0.05f;

    [System.Serializable]
    public class Stage01StepData
    {
        [TextArea(3, 10)]
        public string message = "ここにセリフを入力";
        public float messageWaitTime = 2.0f;
        public bool hideBoardAfterThis = false;
    }

    [Header("=== 💬 ステージ1 セリフ一覧 ===")]
    [Header("▼ Phase 1: ステージ1開始後すぐ（No.1〜2）")]
    public List<Stage01StepData> phase1_Start;

    [Header("▼ Phase 2: 図１のアリーナが【封鎖（発動）】されたら（No.3〜5）")]
    public List<Stage01StepData> phase2_Zone1Sealed;

    [Header("▼ Phase 3: 図１のアリーナが【解除】されたら（No.6〜7）")]
    public List<Stage01StepData> phase3_Zone1Cleared;

    [Header("▼ Phase 4: 図２のアリーナが【封鎖（発動）】されたら（No.8〜9）")]
    public List<Stage01StepData> phase4_Zone2Sealed;

    [Header("▼ Phase 5: 図２のアリーナが【解除】されたら（No.10〜11）")]
    public List<Stage01StepData> phase5_Zone2Cleared;

    [Header("▼ Phase 6: 図３（ボス部屋）に突入したら（No.12）")]
    public List<Stage01StepData> phase6_BossSpotted;

    [Header("▼ Phase 7: ボスが振り下ろし攻撃をしてきたら（No.13）")]
    public List<Stage01StepData> phase7_BossSlamAttack;

    [Header("▼ Phase 8: ボスのスタンゲージが削れきったら（No.14〜15）")]
    public List<Stage01StepData> phase8_BossStunned;

    [Header("▼ Phase 9: ボスが倒されたら（No.16）")]
    public List<Stage01StepData> phase9_BossDefeated;

    [Header("References")]
    [SerializeField] private EnemyBossAI bossAI;

    public Transform playerTransform;

    // 🏛️ アリーナの常時監視用フラグ
    private bool isZone1Sealed = false;
    private bool isZone1Cleared = false;
    private bool isZone2Sealed = false;
    private bool isZone2Cleared = false;
    private bool isZone3Sealed = false;

    // ボス戦用フラグ
    private bool isBossSlammed = false;
    private bool isBossStunned = false;
    private bool isBossDefeated = false;

    void Start()
    {
        // 🔔 アリーナのイベント（発動・解除）に自作関数を自動登録
        RegisterArenaEvents();

        SetPlayerLockAll(true);
        SetPlayerCameraLock(false);

        // メッセージの一本道レールを開始
        StartCoroutine(TutorialSequenceRoutine());

        if (bossAI != null)
        {
            bossAI.OnArmAttackStarted += OnBossArmAttack;
            bossAI.OnStaminaBroken += OnBossStaminaBroken;
        }
    }

    void OnDestroy()
    {
        UnregisterArenaEvents();
        if (bossAI != null)
        {
            bossAI.OnArmAttackStarted -= OnBossArmAttack;
            bossAI.OnStaminaBroken -= OnBossStaminaBroken;
        }
    }

    /// <summary>
    /// アリーナの挙動と完全に同期した一本道のメッセージタイムライン
    /// </summary>
    private IEnumerator TutorialSequenceRoutine()
    {
        // --- Phase 1: ステージ1開始後すぐ (No.1 ~ No.2) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase1_Start));
        SetPlayerLockAll(false);

        // ⏳【トリガー待ち】図1のアリーナにプレイヤーが入り、壁が「封鎖」されるまで待機
        while (!isZone1Sealed) { yield return null; }

        // --- Phase 2: 図１が封鎖されたら (No.3 ~ No.5) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase2_Zone1Sealed));

        // ⏳【トリガー待ち】図1の敵を全滅させ、壁が「解除」されるまで待機
        while (!isZone1Cleared) { yield return null; }

        // --- Phase 3: 図１の壁が解除されたら (No.6 ~ No.7) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase3_Zone1Cleared));

        // ⏳【トリガー待ち】図2のアリーナに入り、壁が「封鎖」されるまで待機
        while (!isZone2Sealed) { yield return null; }

        // --- Phase 4: 図２が封鎖されたら (No.8 ~ No.9) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase4_Zone2Sealed));

        // ⏳【トリガー待ち】図2の敵を全滅させ、壁が「解除」されるまで待機
        while (!isZone2Cleared) { yield return null; }

        // --- Phase 5: 図２の壁が解除されたら (No.10 ~ No.11) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase5_Zone2Cleared));

        // ⏳【トリガー待ち】図3（ボス部屋）に到達するまで待機（アリーナ封鎖）
        while (!isZone3Sealed) { yield return null; }

        // --- Phase 6: 図３の部分まで進んだら (No.12) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase6_BossSpotted));

        // ⏳【トリガー待ち】ボスが振り下ろし攻撃をしてくるまで待機
        while (!isBossSlammed) { yield return null; }

        // --- Phase 7: ボスが振り下ろし攻撃をしてきたら (No.13) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase7_BossSlamAttack));

        // ⏳【トリガー待ち】ボスのスタンゲージが削れきるまで待機
        while (!isBossStunned) { yield return null; }

        // --- Phase 8: ボスのスタンゲージが削れきったら (No.14 ~ No.15) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase8_BossStunned));

        // ⏳【トリガー待ち】ボスが倒される（アリーナ解除、または死亡フラグ）まで待機
        while (!isBossDefeated) { yield return null; }

        // --- Phase 9: ボスが倒されたら (No.16) ---
        yield return StartCoroutine(PlayPhaseRoutine(phase9_BossDefeated));
    }

    // ==========================================
    // 🚪 アリーナのイベントを検知するコールバック関数群
    // ==========================================
    private void OnZone1Sealed() { isZone1Sealed = true; }
    private void OnZone1Cleared() { isZone1Cleared = true; }
    private void OnZone2Sealed() { isZone2Sealed = true; }
    private void OnZone2Cleared() { isZone2Cleared = true; }
    private void OnZone3Sealed() { isZone3Sealed = true; }
    private void OnZone3Cleared() { isBossDefeated = true; } // ボスアリーナ解除＝ボス撃破とみなす

    // ==========================================
    // ⚔️ ボスのアクション等、外部からのイベント通知窓口
    // ==========================================

    // 🎯 ボスが振り下ろし攻撃（構え）に入った瞬間に呼ばれる
    private void OnBossArmAttack()
    {
        Debug.Log("ボスが振り下ろし攻撃をしてきた！");
        isBossSlammed = true; // 💡ここを追加！これでコルーチンの待機が解除されます
    }

    // 🎯 ボスのスタンゲージが削れきった瞬間に呼ばれる
    private void OnBossStaminaBroken()
    {
        Debug.Log("ボスのスタンゲージが削れきった（チャンス状態）！");
        isBossStunned = true;
    }

    // 予備用の公開関数（インスペクターの別トリガー等から叩く用として残す場合）
    public void OnBossStunned() { isBossStunned = true; }


    // ==========================================
    // ⚙️ イベント登録・解除・プレイヤーロック用ヘルパー
    // ==========================================
    private void RegisterArenaEvents()
    {
        if (zone1Arena != null) { zone1Arena.Sealed += OnZone1Sealed; zone1Arena.Cleared += OnZone1Cleared; }
        if (zone2Arena != null) { zone2Arena.Sealed += OnZone2Sealed; zone2Arena.Cleared += OnZone2Cleared; }
        if (zone3Arena != null) { zone3Arena.Sealed += OnZone3Sealed; zone3Arena.Cleared += OnZone3Cleared; }
    }

    private void UnregisterArenaEvents()
    {
        if (zone1Arena != null) { zone1Arena.Sealed -= OnZone1Sealed; zone1Arena.Cleared -= OnZone1Cleared; }
        if (zone2Arena != null) { zone2Arena.Sealed -= OnZone2Sealed; zone2Arena.Cleared -= OnZone2Cleared; }
        if (zone3Arena != null) { zone3Arena.Sealed -= OnZone3Sealed; zone3Arena.Cleared -= OnZone3Cleared; }
    }

    private IEnumerator PlayPhaseRoutine(List<Stage01StepData> phaseSteps)
    {
        if (phaseSteps == null || phaseSteps.Count == 0) yield break;
        for (int i = 0; i < phaseSteps.Count; i++)
        {
            var step = phaseSteps[i];
            if (commentBoardObject != null) commentBoardObject.SetActive(true);
            yield return StartCoroutine(TypeAndAutoAdvance(step.message, defaultTextSpeed, step.messageWaitTime));
            if (step.hideBoardAfterThis || i == phaseSteps.Count - 1)
            {
                if (commentBoardObject != null) commentBoardObject.SetActive(false);
                if (stage01Text != null) stage01Text.text = "";
            }
        }
    }

    private IEnumerator TypeAndAutoAdvance(string message, float speed, float waitTime)
    {
        stage01Text.text = message;
        stage01Text.ForceMeshUpdate();
        int totalVisibleCharacters = stage01Text.textInfo.characterCount;
        stage01Text.maxVisibleCharacters = 0;
        for (int i = 0; i <= totalVisibleCharacters; i++)
        {
            stage01Text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(speed);
        }
        yield return new WaitForSeconds(waitTime);
    }

    private PlayerAbilityLocker m_locker;
    private PlayerAbilityLocker GetLocker()
    {
        if (m_locker != null) return m_locker;
        if (playerTransform != null) m_locker = playerTransform.GetComponentInChildren<PlayerAbilityLocker>(true);
        if (m_locker == null && Player.Current != null) m_locker = Player.Current.abilityLocker;
        if (m_locker == null) m_locker = FindAnyObjectByType<PlayerAbilityLocker>();
        return m_locker;
    }

    private void SetPlayerLockAll(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        foreach (PlayerAbilityType ability in System.Enum.GetValues(typeof(PlayerAbilityType)))
            locker.SetLocked(ability, isLock);
    }

    private void SetPlayerCameraLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.Camera, isLock);
    }
}