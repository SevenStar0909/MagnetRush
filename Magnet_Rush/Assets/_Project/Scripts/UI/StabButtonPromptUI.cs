using UnityEngine;
using UnityEngine.UI;

public class StabButtonPromptUI : MonoBehaviour
{
    [SerializeField] private Image m_buttonImage;
    [SerializeField] private float m_refreshInterval = 0.05f;
    [SerializeField] private float m_pulseCyclesPerSecond = 4f;
    [SerializeField] private float m_pulseScaleAmount = 0.28f;
    [SerializeField] private float m_pulseSnapPower = 2.4f;

    private StabAbility m_stab;
    private float m_nextRefreshTime;
    private bool m_visible;
    private Vector3 m_baseScale = Vector3.one;

    private void Awake()
    {
        if (m_buttonImage == null)
            m_buttonImage = GetComponent<Image>();

        m_baseScale = transform.localScale;
        SetVisible(false);
    }

    private void Update()
    {
        if (Time.unscaledTime >= m_nextRefreshTime)
        {
            m_nextRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, m_refreshInterval);

            if (m_stab == null)
                ResolvePlayerStab();

            SetVisible(m_stab != null && m_stab.CanStabNow);
        }

        UpdatePulse();
    }

    private void ResolvePlayerStab()
    {
        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
            m_stab = playerObj.GetComponent<StabAbility>();
    }

    private void SetVisible(bool visible)
    {
        if (m_visible == visible)
            return;

        m_visible = visible;
        if (m_buttonImage != null)
            m_buttonImage.enabled = visible;

        if (!visible)
            transform.localScale = m_baseScale;
    }

    private void UpdatePulse()
    {
        if (!m_visible)
            return;

        float cycles = Mathf.Max(0f, m_pulseCyclesPerSecond);
        float amount = Mathf.Max(0f, m_pulseScaleAmount);
        float rawWave = (Mathf.Sin(Time.unscaledTime * cycles * Mathf.PI * 2f) + 1f) * 0.5f;
        float wave = Mathf.Pow(rawWave, Mathf.Max(0.1f, m_pulseSnapPower));
        transform.localScale = m_baseScale * (1f + wave * amount);
    }
}
