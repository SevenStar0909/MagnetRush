using UnityEngine;

// Scripts/Core/Magnet/Connection/MagneticConnectionVisualizer.cs
public class MagneticConnectionVisualizer : MonoBehaviour
{
    private LineRenderer m_line;
    private GameObject m_lineGo;
    private MagneticConnection m_connection;
    private MagneticConnectionSettings m_settings;
    private Material m_mat;

    private void Awake()
    {
        m_mat = new Material(Shader.Find("Sprites/Default")); // カスタムシェーダ不要
    }
    public void Show(MagneticConnection c, MagneticConnectionSettings s)
    {
        m_connection = c; m_settings = s;
        m_lineGo = new GameObject("ConnectionLine");
        m_line = m_lineGo.AddComponent<LineRenderer>();
        m_line.positionCount = 2;
        m_line.startWidth = m_line.endWidth = 0.05f;
        m_line.material = m_mat;
        // 色は LateUpdate で IsActivated を見て毎フレーム反映する
    }
    public void Hide()
    {
        Destroy(m_lineGo);
        m_lineGo = null;
        m_line = null;
        m_connection = null;
    }
    private void LateUpdate()
    {
        if (m_connection == null)
        {
            return;
        }
        m_line.enabled = m_connection.IsActive;
        m_line.SetPosition(0, m_connection.PlayerSide.transform.position);
        m_line.SetPosition(1, m_connection.TargetSide.transform.position);

        Color targetCol = ColorOf(m_connection.TargetSide.Pole, m_settings);
        if (m_connection.IsActivated)
        { // 発動中: 両端 2 色
            m_line.startColor = ColorOf(m_connection.PlayerSide.Pole, m_settings);
            m_line.endColor = targetCol;
        }
        else
        { // 待機中: 接続先の極性色 1 色
            m_line.startColor = m_line.endColor = targetCol;
        }
    }
    private Color ColorOf(MagneticPole pole, MagneticConnectionSettings m_settings)
    {
        if (pole == MagneticPole.S)
        {
            return m_settings.SColor; // 青
        }
        if (pole == MagneticPole.N)
        {
            return m_settings.NColor; // 赤
        }
        return Color.white; // 磁極なし（None）の時は仮に白
    }
}