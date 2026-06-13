using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ用「クリア演出再生」ボタン。OnGUI でゲームビューにボタンを描画する。
/// Editor / DebugBuild 限定で動作。ボス撃破を待たずに GameClearPresentation.PlayClear() を呼んで仮のクリア演出を流す。
/// Active Input Handler が「New Input System」のみのため、クリック検出は Mouse.current で行う(DebugBossStunButton と同方式)。
/// </summary>
public class DebugGameClearButton : MonoBehaviour
{
    private GameClearPresentation m_clear;
    private float m_nextScanTime;
    private const float k_ScanInterval = 1f;

    void Start()
    {
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            Destroy(this);
            return;
        }
        ScanTarget();
    }

    void Update()
    {
        if (Time.time < m_nextScanTime) return;
        m_nextScanTime = Time.time + k_ScanInterval;
        if (m_clear == null) ScanTarget();
    }

    void ScanTarget()
    {
        // GameClearCanvas は _Managers 配下で常時アクティブ。演出本体(Root)だけが非アクティブなので通常検索で見つかる
        m_clear = FindFirstObjectByType<GameClearPresentation>();
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
        // リリースビルドでは Start で Destroy 済みだが、その1フレームの描画も防ぐ。
        // 併せて GameClearPresentation 未検出時も描かない(DebugBossStunButton の早期 return と同方式)。
        if ((!Debug.isDebugBuild && !Application.isEditor) || m_clear == null) return;

        const float panelW = 220f;
        const float panelH = 58f;
        var rect = new Rect(10f, 10f, panelW, panelH);
        GUI.Box(rect, "[Debug] Game Clear");

        if (ClickButton(new Rect(rect.x + 12f, rect.y + 28f, panelW - 24f, 24f), "Play Clear演出"))
            m_clear.PlayClear();
    }
}
