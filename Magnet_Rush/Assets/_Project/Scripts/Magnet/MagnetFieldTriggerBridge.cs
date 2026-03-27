using UnityEngine;

/// <summary>
/// MagnetField のトリガー子GOに付けるブリッジ。
/// OnTrigger イベントを親の MagnetField に転送する。
/// MagnetField レイヤーに配置され、Bullet レイヤーとの衝突を Layer Matrix で遮断する。
/// </summary>
public class MagnetFieldTriggerBridge : MonoBehaviour
{
    private MagnetField m_field;

    public void Initialize(MagnetField field)
    {
        m_field = field;
    }

    void OnTriggerStay(Collider other)
    {
        if (m_field != null)
            m_field.HandleTriggerStay(other);
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_field != null)
            m_field.HandleTriggerEnter(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (m_field != null)
            m_field.HandleTriggerExit(other);
    }
}
