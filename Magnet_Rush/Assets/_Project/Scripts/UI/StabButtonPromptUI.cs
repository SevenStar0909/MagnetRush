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
    [SerializeField] private GameObject m_electricEffectPrefab;
    [SerializeField] private Color m_electricColor = new Color(0.1f, 0.55f, 1f, 0.95f);
    [SerializeField] private float m_electricOverlayScale = 1.3f;
    [SerializeField] private Vector2 m_electricOverlayOffset = Vector2.zero;
    [SerializeField] private float m_electricEffectScale = 0.34f;
    [SerializeField] private float m_electricThicknessScale = 2.2f;
    [SerializeField] private int m_electricRingInstanceCount = 12;
    [SerializeField] private float m_electricRingRadius = 1.55f;
    [SerializeField] private float m_electricAngleVariation = 55f;
    [SerializeField] private float m_electricTiltAngle = 28f;
    [SerializeField] private float m_electricRingRotateSpeed = 35f;
    [SerializeField] private float m_electricCameraSize = 3f;
    [SerializeField] private int m_electricTextureSize = 256;
    [SerializeField, Range(0, 31)] private int m_electricRenderLayer = 5;

    [Header("Electric Edge")]
    [SerializeField] private bool m_showElectricEdge = true;
    [SerializeField] private int m_electricEdgeInstanceCount = 28;
    [SerializeField] private float m_electricEdgeInnerRadius = 1.25f;
    [SerializeField] private float m_electricEdgeOuterRadius = 1.85f;
    [SerializeField] private float m_electricEdgeScale = 0.32f;
    [SerializeField] private float m_electricEdgeJitter = 0.18f;
    [SerializeField] private float m_electricEdgeAngleJitter = 80f;
    [SerializeField] private float m_electricEdgeRefreshPerSecond = 18f;

    private StabAbility m_stab;
    private float m_nextRefreshTime;
    private bool m_visible;
    private Vector3 m_baseScale = Vector3.one;
    private RectTransform m_rectTransform;
    private RectTransform m_electricOverlay;
    private RawImage m_electricImage;
    private Camera m_electricCamera;
    private RenderTexture m_electricRenderTexture;
    private GameObject m_electricEffectRoot;
    private ParticleSystem[] m_electricParticles;
    private float m_nextElectricEdgeRefreshTime;
    private readonly List<Transform> m_electricEdgeInstances = new();

    private void Awake()
    {
        m_rectTransform = transform as RectTransform;
        if (m_buttonImage == null)
            m_buttonImage = GetComponent<Image>();

        m_baseScale = transform.localScale;
        BuildElectricEffect();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (m_electricRenderTexture != null)
        {
            m_electricRenderTexture.Release();
            Destroy(m_electricRenderTexture);
        }

        if (m_electricCamera != null)
            Destroy(m_electricCamera.gameObject);
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

        bool showElectric = visible && m_showElectricEffect && m_electricImage != null;
        if (m_electricImage != null)
            m_electricImage.enabled = showElectric;

        if (showElectric)
        {
            if (m_electricEffectRoot != null)
                m_electricEffectRoot.SetActive(true);

            PlayElectricEffect();
        }
        else
        {
            StopElectricEffect();

            if (m_electricEffectRoot != null)
                m_electricEffectRoot.SetActive(false);
        }

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
        if (!m_showElectricEffect || m_electricEffectPrefab == null || m_electricOverlay != null)
            return;

        GameObject overlay = new GameObject("ElectricEffectOverlay", typeof(RectTransform), typeof(RawImage));
        overlay.transform.SetParent(transform, false);
        m_electricOverlay = overlay.GetComponent<RectTransform>();
        m_electricOverlay.anchorMin = new Vector2(0.5f, 0.5f);
        m_electricOverlay.anchorMax = new Vector2(0.5f, 0.5f);
        m_electricOverlay.pivot = new Vector2(0.5f, 0.5f);
        m_electricOverlay.SetAsLastSibling();

        m_electricImage = overlay.GetComponent<RawImage>();
        m_electricImage.raycastTarget = false;
        m_electricImage.color = Color.white;

        int textureSize = Mathf.Clamp(m_electricTextureSize, 64, 1024);
        m_electricRenderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        m_electricRenderTexture.name = "StabButtonPrompt_ElectricationRT";
        m_electricRenderTexture.Create();
        m_electricImage.texture = m_electricRenderTexture;

        GameObject cameraObject = new GameObject("StabButtonPrompt_ElectricEffectCamera", typeof(Camera));
        m_electricCamera = cameraObject.GetComponent<Camera>();
        m_electricCamera.enabled = false;
        m_electricCamera.clearFlags = CameraClearFlags.SolidColor;
        m_electricCamera.backgroundColor = Color.clear;
        m_electricCamera.orthographic = true;
        m_electricCamera.nearClipPlane = 0.01f;
        m_electricCamera.farClipPlane = 50f;
        m_electricCamera.targetTexture = m_electricRenderTexture;
        m_electricCamera.cullingMask = 1 << Mathf.Clamp(m_electricRenderLayer, 0, 31);
        m_electricCamera.transform.SetPositionAndRotation(new Vector3(10000f, 10000f, -10000f), Quaternion.identity);

        m_electricEffectRoot = new GameObject("YButton_ElectricationRing");
        m_electricEffectRoot.transform.SetParent(m_electricCamera.transform, false);
        m_electricEffectRoot.transform.localPosition = new Vector3(0f, 0f, 8f);
        m_electricEffectRoot.transform.localRotation = Quaternion.identity;
        m_electricEffectRoot.transform.localScale = Vector3.one;

        int ringCount = Mathf.Clamp(m_electricRingInstanceCount, 1, 16);
        float ringRadius = Mathf.Max(0f, m_electricRingRadius);
        for (int i = 0; i < ringCount; i++)
        {
            float angle = 360f / ringCount * i;
            float radians = angle * Mathf.Deg2Rad;
            GameObject effect = Instantiate(m_electricEffectPrefab, m_electricEffectRoot.transform);
            effect.name = $"YButton_P_FX_Electrication_{i:00}";
            effect.transform.localPosition = new Vector3(Mathf.Cos(radians) * ringRadius, Mathf.Sin(radians) * ringRadius, 0f);
            effect.transform.localRotation = GetElectricRingRotation(i, angle);
            effect.transform.localScale = Vector3.one * Mathf.Max(0.01f, m_electricEffectScale);
        }

        BuildElectricEdgeInstances();
        SetLayerRecursive(m_electricEffectRoot, Mathf.Clamp(m_electricRenderLayer, 0, 31));
        m_electricParticles = m_electricEffectRoot.GetComponentsInChildren<ParticleSystem>(true);
        ApplyElectricColor();
        UpdateElectricOverlayRect();
        m_electricImage.enabled = false;
        m_electricEffectRoot.SetActive(false);
    }

    private void UpdateElectricEffect()
    {
        if (!m_visible || !m_showElectricEffect || m_electricImage == null || m_electricCamera == null)
            return;

        UpdateElectricOverlayRect();
        m_electricCamera.orthographicSize = Mathf.Max(0.1f, m_electricCameraSize);
        if (m_electricEffectRoot != null)
            m_electricEffectRoot.transform.localRotation = m_electricEffectRoot.transform.localRotation * Quaternion.Euler(0f, 0f, m_electricRingRotateSpeed * Time.unscaledDeltaTime);

        UpdateElectricEdge();
        m_electricCamera.Render();
    }

    private void UpdateElectricOverlayRect()
    {
        if (m_rectTransform == null || m_electricOverlay == null)
            return;

        Vector2 size = m_rectTransform.rect.size * Mathf.Max(0.1f, m_electricOverlayScale);
        m_electricOverlay.anchoredPosition = m_electricOverlayOffset;
        m_electricOverlay.sizeDelta = size;
        m_electricOverlay.localScale = Vector3.one;
    }

    private void BuildElectricEdgeInstances()
    {
        if (!m_showElectricEdge || m_electricEffectPrefab == null || m_electricEffectRoot == null)
            return;

        int edgeCount = Mathf.Clamp(m_electricEdgeInstanceCount, 3, 64);
        for (int i = 0; i < edgeCount; i++)
        {
            GameObject effect = Instantiate(m_electricEffectPrefab, m_electricEffectRoot.transform);
            effect.name = $"YButton_P_FX_Electrication_Edge_{i:00}";
            effect.transform.localScale = Vector3.one * Mathf.Max(0.01f, m_electricEdgeScale);
            m_electricEdgeInstances.Add(effect.transform);
        }

        RefreshElectricEdge(true);
    }

    private void UpdateElectricEdge()
    {
        if (!m_showElectricEdge || m_electricEdgeInstances.Count == 0)
            return;

        float refresh = Mathf.Max(1f, m_electricEdgeRefreshPerSecond);
        if (Time.unscaledTime < m_nextElectricEdgeRefreshTime)
            return;

        m_nextElectricEdgeRefreshTime = Time.unscaledTime + 1f / refresh;
        RefreshElectricEdge(false);
    }

    private void RefreshElectricEdge(bool force)
    {
        if (!force && (!m_visible || !m_showElectricEdge))
            return;

        int count = m_electricEdgeInstances.Count;
        if (count == 0)
            return;

        float innerRadius = Mathf.Max(0f, m_electricEdgeInnerRadius);
        float outerRadius = Mathf.Max(innerRadius, m_electricEdgeOuterRadius);
        float jitter = Mathf.Max(0f, m_electricEdgeJitter);
        float angleJitter = Mathf.Max(0f, m_electricEdgeAngleJitter);

        for (int i = 0; i < count; i++)
        {
            Transform edge = m_electricEdgeInstances[i];
            if (edge == null)
                continue;

            float baseAngle = 360f / count * i;
            float angle = baseAngle + Random.Range(-angleJitter, angleJitter);
            float radians = angle * Mathf.Deg2Rad;
            float targetRadius = i % 2 == 0 ? outerRadius : innerRadius;
            float radius = targetRadius + Random.Range(-jitter, jitter);
            edge.localPosition = new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, Random.Range(-0.08f, 0.08f));
            edge.localRotation = GetElectricRingRotation(i, angle + Random.Range(-angleJitter, angleJitter));
            edge.localScale = Vector3.one * Mathf.Max(0.01f, m_electricEdgeScale * Random.Range(0.75f, 1.35f));
        }
    }

    private void PlayElectricEffect()
    {
        if (m_electricParticles == null)
            return;

        foreach (ParticleSystem particle in m_electricParticles)
        {
            if (particle == null)
                continue;

            particle.Play(true);
        }
    }

    private void StopElectricEffect()
    {
        if (m_electricParticles == null)
            return;

        foreach (ParticleSystem particle in m_electricParticles)
        {
            if (particle == null)
                continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ApplyElectricColor()
    {
        if (m_electricParticles != null)
        {
            foreach (ParticleSystem particle in m_electricParticles)
            {
                if (particle == null)
                    continue;

                ParticleSystem.MainModule main = particle.main;
                main.startColor = m_electricColor;
                main.startSizeMultiplier *= Mathf.Max(0.1f, m_electricThicknessScale);
            }
        }

        if (m_electricEffectRoot == null)
            return;

        Renderer[] renderers = m_electricEffectRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer effectRenderer in renderers)
        {
            if (effectRenderer == null)
                continue;

            if (effectRenderer is ParticleSystemRenderer particleRenderer)
                particleRenderer.alignment = ParticleSystemRenderSpace.Local;

            foreach (Material material in effectRenderer.materials)
            {
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", m_electricColor);

                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", m_electricColor);

                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", m_electricColor);
            }
        }
    }

    private Quaternion GetElectricRingRotation(int index, float baseAngle)
    {
        float variation = Mathf.Max(0f, m_electricAngleVariation);
        float[] offsets = { 0f, 90f, -90f, variation, -variation, 180f };
        float z = baseAngle + offsets[index % offsets.Length];
        float tilt = Mathf.Max(0f, m_electricTiltAngle);
        float x = index % 2 == 0 ? tilt : -tilt;
        float y = index % 3 == 0 ? -tilt * 0.5f : tilt * 0.5f;
        return Quaternion.Euler(x, y, z);
    }

    private static void SetLayerRecursive(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
