using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ用ボス強制スタンボタン。OnGUI でゲームビューにボタンを描画する。
/// Editor / DebugBuild 限定で動作。スタブ動作確認用。
/// Active Input Handler が「New Input System」のみのため、クリック検出は Mouse.current で行う。
/// </summary>
public class DebugBossStunButton : MonoBehaviour
{
    [SerializeField] private float m_teleportDistance = 1.0f;
    [SerializeField] private int m_restoreHpValue = 100;

    private EnemyBossAI m_bossAI;
    private Stamina m_bossStamina;
    private Health m_bossHealth;
    private EnemyBossBaseA_Animator m_bossAnimator;
    private Transform m_playerTransform;

    private float m_nextScanTime;
    private const float k_ScanInterval = 1f;

    void Start()
    {
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            Destroy(this);
            return;
        }
        ScanTargets();
    }

    void Update()
    {
        if (Time.time < m_nextScanTime) return;
        m_nextScanTime = Time.time + k_ScanInterval;
        if (m_bossAI == null || m_playerTransform == null)
            ScanTargets();
    }

    void ScanTargets()
    {
        m_bossAI = FindFirstObjectByType<EnemyBossAI>();
        if (m_bossAI != null)
        {
            m_bossStamina = m_bossAI.GetComponent<Stamina>();
            m_bossHealth = m_bossAI.GetComponent<Health>();
            m_bossAnimator = m_bossAI.GetComponentInChildren<EnemyBossBaseA_Animator>(true);
        }

        var playerObj = GameObject.FindWithTag("Player");
        m_playerTransform = playerObj != null ? playerObj.transform : null;
    }

    private int m_lastClickFrame = -1;

    bool ClickButton(Rect r, string label)
    {
        GUI.Button(r, label);
        if (Event.current.type != EventType.Repaint) return false;
        if (Time.frameCount == m_lastClickFrame) return false;

        var mouse = Mouse.current;
        if (mouse == null) return false;
        if (!mouse.leftButton.wasPressedThisFrame) return false;

        var p = mouse.position.ReadValue();
        var gp = new Vector2(p.x, Screen.height - p.y);
        if (!r.Contains(gp)) return false;

        m_lastClickFrame = Time.frameCount;
        return true;
    }

    void OnGUI()
    {
        if (m_bossAI == null) return;

        const float panelW = 240f;
        const float panelH = 260f;
        var rect = new Rect(10, 10, panelW, panelH);

        GUI.Box(rect, "[Debug] Stab Test");

        float y = 32f;
        const float lineH = 18f;

        GUI.Label(new Rect(20, y, panelW - 20, lineH), "State: " + m_bossAI.State);
        y += lineH;

        GUI.Label(new Rect(20, y, panelW - 20, lineH), "Battle AI: " + (m_bossAI.isBattleing ? "ON" : "OFF"));
        y += lineH;

        if (m_bossHealth != null)
            GUI.Label(new Rect(20, y, panelW - 20, lineH), "HP: " + m_bossHealth.CurrentHealth);
        y += lineH;

        if (m_bossStamina != null)
            GUI.Label(new Rect(20, y, panelW - 20, lineH), "Stamina: " + m_bossStamina.CurrentStamina + "/" + m_bossStamina.MaxStamina);
        y += lineH;

        if (m_playerTransform != null)
        {
            float dist = Vector3.Distance(m_playerTransform.position, m_bossAI.transform.position);
            GUI.Label(new Rect(20, y, panelW - 20, lineH), "Distance: " + dist.ToString("F2") + "m");
        }
        y += lineH + 4f;

        if (ClickButton(new Rect(15, y, panelW - 25, 24f), m_bossAI.isBattleing ? "Battle AI OFF" : "Battle AI ON"))
            m_bossAI.setisBattleing();
        y += 28f;

        if (ClickButton(new Rect(15, y, panelW - 25, 24f), "Force Stun Boss"))
            ForceStun();
        y += 28f;

        if (ClickButton(new Rect(15, y, panelW - 25, 24f), "Force Stagger Boss"))
            ForceStagger();
        y += 28f;

        if (ClickButton(new Rect(15, y, panelW - 25, 24f), "Restore HP"))
            RestoreHp();
        y += 28f;

        float halfW = (panelW - 25) * 0.5f - 2f;
        if (ClickButton(new Rect(15, y, halfW, 24f), "TP Player"))
            TeleportPlayerToBoss();
        if (ClickButton(new Rect(15 + halfW + 4f, y, halfW, 24f), "Setup All"))
        {
            RestoreHp();
            TeleportPlayerToBoss();
            ForceStun();
        }
    }

    // スタン（振り上げカウンター）ルートの確認用。腕カウンターと同じく IsStunned を立てて StunAnim へ入れる。
    // ※SO(StatusData)の値は実行時に書き換えない（アセットに焼き付いてチューニング値が壊れるため）。継続時間は SO の値をそのまま使う。
    void ForceStun()
    {
        if (m_bossAnimator == null) return;
        m_bossAnimator.SetIsStunnedTrue();
    }

    // よろけ（蓄積ゲージ満タン）ルートの確認用。ゲージを満タン消費して OnBreak→Stagger を正規ルートで発火する。
    void ForceStagger()
    {
        if (m_bossStamina == null) return;
        m_bossStamina.ResetStamina();
        m_bossStamina.Consume(m_bossStamina.MaxStamina);
    }

    void RestoreHp()
    {
        if (m_bossHealth == null) return;
        m_bossHealth.SetMaxHealth(m_restoreHpValue, true);
    }

    void TeleportPlayerToBoss()
    {
        if (m_playerTransform == null || m_bossAI == null) return;
        m_playerTransform.position = m_bossAI.transform.position + new Vector3(m_teleportDistance, 0f, 0f);
    }
}
