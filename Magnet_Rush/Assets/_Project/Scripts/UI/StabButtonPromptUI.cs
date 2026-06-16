using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class StabButtonPromptUI : MonoBehaviour
{
    [SerializeField] private Image m_buttonImage;
    [SerializeField] private float m_refreshInterval = 0.05f;
    [SerializeField] private float m_pulseCyclesPerSecond = 4f;
    [SerializeField] private float m_pulseScaleAmount = 0.28f;
    [SerializeField] private float m_pulseSnapPower = 2.4f;

    [Header("Electric Effect")]
    [SerializeField] private bool m_showElectricEffect = true;
    [SerializeField] private Color m_electricColor = new Color(1f, 0.86f, 0.05f, 0.95f);
    [SerializeField] private float m_electricRadiusScale = 1.05f;
    [SerializeField] private float m_electricLineWidth = 5f;
    [SerializeField] private int m_electricArcCount = 3;
    [SerializeField] private int m_electricSegmentsPerArc = 7;
    [SerializeField] private float m_electricJitter = 14f;
    [SerializeField] private float m_electricRefreshPerSecond = 18f;
    [SerializeField] private float m_electricRotateSpeed = 50f;

    private StabAbility m_stab;
    private float m_nextRefreshTime;
    private float m_nextElectricRefreshTime;
    private bool m_visible;
    private Vector3 m_baseScale = Vector3.one;
    private RectTransform m_rectTransform;
    private RectTransform m_electricRoot;
    private readonly List<Image> m_electricSegments = new();

    private void Awake()
    {
        m_rectTransform = transform as RectTransform;
        if (m_buttonImage == null)
            m_buttonImage = GetComponent<Image>();

        m_baseScale = transform.localScale;
        BuildElectricEffect();
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
        UpdateElectricEffect();
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

        if (m_electricRoot != null)
            m_electricRoot.gameObject.SetActive(visible && m_showElectricEffect);

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

    private void BuildElectricEffect()
    {
        if (!m_showElectricEffect || m_electricRoot != null)
            return;

        GameObject root = new GameObject("ElectricEffect", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        m_electricRoot = root.GetComponent<RectTransform>();
        m_electricRoot.anchorMin = new Vector2(0.5f, 0.5f);
        m_electricRoot.anchorMax = new Vector2(0.5f, 0.5f);
        m_electricRoot.pivot = new Vector2(0.5f, 0.5f);
        m_electricRoot.anchoredPosition = Vector2.zero;
        m_electricRoot.sizeDelta = Vector2.zero;
        m_electricRoot.SetAsFirstSibling();

        int segmentCount = Mathf.Max(1, m_electricArcCount) * Mathf.Max(1, m_electricSegmentsPerArc);
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject line = new GameObject($"ElectricLine_{i:00}", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(m_electricRoot, false);
            Image image = line.GetComponent<Image>();
            image.color = m_electricColor;
            image.raycastTarget = false;
            m_electricSegments.Add(image);
        }

        m_electricRoot.gameObject.SetActive(false);
    }

    private void UpdateElectricEffect()
    {
        if (!m_visible || !m_showElectricEffect || m_electricRoot == null)
            return;

        m_electricRoot.Rotate(0f, 0f, m_electricRotateSpeed * Time.unscaledDeltaTime);

        if (Time.unscaledTime < m_nextElectricRefreshTime)
            return;

        float refresh = Mathf.Max(1f, m_electricRefreshPerSecond);
        m_nextElectricRefreshTime = Time.unscaledTime + 1f / refresh;
        RefreshElectricSegments();
    }

    private void RefreshElectricSegments()
    {
        if (m_rectTransform == null || m_electricSegments.Count == 0)
            return;

        Vector2 size = m_rectTransform.rect.size;
        float radius = Mathf.Max(size.x, size.y) * 0.5f * Mathf.Max(0.1f, m_electricRadiusScale);
        int arcCount = Mathf.Max(1, m_electricArcCount);
        int segmentsPerArc = Mathf.Max(1, m_electricSegmentsPerArc);
        int lineIndex = 0;

        for (int arc = 0; arc < arcCount; arc++)
        {
            float startAngle = (360f / arcCount) * arc + Random.Range(-18f, 18f);
            float arcLength = Random.Range(70f, 115f);

            Vector2 previous = PointOnElectricRing(startAngle, radius);
            for (int segment = 0; segment < segmentsPerArc && lineIndex < m_electricSegments.Count; segment++)
            {
                float t = (segment + 1f) / segmentsPerArc;
                Vector2 next = PointOnElectricRing(startAngle + arcLength * t, radius);
                Vector2 jitter = Random.insideUnitCircle * Mathf.Max(0f, m_electricJitter);
                ApplyElectricLine(m_electricSegments[lineIndex], previous, next + jitter);
                previous = next;
                lineIndex++;
            }
        }

        for (; lineIndex < m_electricSegments.Count; lineIndex++)
            m_electricSegments[lineIndex].enabled = false;
    }

    private static Vector2 PointOnElectricRing(float angleDegrees, float radius)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }

    private void ApplyElectricLine(Image line, Vector2 start, Vector2 end)
    {
        if (line == null)
            return;

        RectTransform rt = line.rectTransform;
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.1f)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.color = m_electricColor;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (start + end) * 0.5f;
        rt.sizeDelta = new Vector2(length, Mathf.Max(1f, m_electricLineWidth));
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }
}
