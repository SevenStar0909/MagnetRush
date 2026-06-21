using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ用「ゲームオーバー演出再生」ボタン。OnGUI でゲームビューにボタンを描画する。
/// Editor / DebugBuild 限定で動作。プレイヤー死亡を待たずに GameOverPresentation.PlayGameOver() を呼んで仮の演出を流す。
/// Active Input Handler が「New Input System」のみのため、クリック検出は Mouse.current で行う(DebugGameClearButton と同方式)。
/// </summary>
public class DebugGameOverButton : MonoBehaviour
{
    private GameOverPresentation m_over;
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
        if (m_over == null) ScanTarget();
    }

    void ScanTarget()
    {
        // GameOverCanvas は _Managers 配下で常時アクティブ。演出本体(Root)だけが非アクティブなので通常検索で見つかる
        m_over = FindFirstObjectByType<GameOverPresentation>();
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
        // 併せて GameOverPresentation 未検出時も描かない(DebugGameClearButton と同方式)。
        if ((!Debug.isDebugBuild && !Application.isEditor) || m_over == null) return;

        const float panelW = 220f;
        const float panelH = 58f;
        // クリア演出ボタン([Debug] Game Clear)の下に並べる
        var rect = new Rect(10f, 76f, panelW, panelH);
        GUI.Box(rect, "[Debug] Game Over");

        if (ClickButton(new Rect(rect.x + 12f, rect.y + 28f, panelW - 24f, 24f), "Play GameOver演出"))
            m_over.PlayGameOver();
    }
}
