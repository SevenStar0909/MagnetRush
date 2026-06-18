using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TitleBuildingLightFlicker : MonoBehaviour
{
    private enum FlickerPhase
    {
        Waiting,
        Lighting,
        Holding,
        Fading
    }

    private sealed class WindowState
    {
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public System.Random Random;
        public FlickerPhase Phase;
        public float CurrentIntensity;
        public float StartIntensity;
        public float TargetIntensity;
        public float TransitionStartTime;
        public float TransitionDuration;
        public float HoldUntilTime;
        public float NextBlinkTime;
        public Color LitColor;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Mesh s_windowMesh;

    [Header("対象")]
    [Tooltip("ビルのRenderer。空ならこのオブジェクト配下から自動取得します。")]
    [SerializeField] private Renderer[] m_targetRenderers;

    [Tooltip("対象が空の時、子オブジェクトのRendererも対象にします。")]
    [SerializeField] private bool m_includeChildrenWhenEmpty = true;

    [Tooltip("元のビル発光マテリアルのスロット番号。全体発光を抑えるために使います。")]
    [SerializeField, Min(0)] private int m_materialIndex = 0;

    [Header("全体発光")]
    [SerializeField, ColorUsage(false, true)] private Color m_baseEmissionColor = new Color(18f, 20f, 55f, 1f);

    [Tooltip("ビル全体の下地発光。0に近いほど、個別ライトだけが目立ちます。")]
    [SerializeField, Range(0f, 1f)] private float m_baseEmissionIntensity = 0.18f;

    [Header("個別ライト生成")]
    [Tooltip("カメラに向いたビル面へライトを並べます。空ならMainCameraを使います。")]
    [SerializeField] private Camera m_viewCamera;

    [Tooltip("1棟ごとに作るライト候補数。多くしても同時点灯数で制限されます。")]
    [SerializeField, Range(1, 64)] private int m_windowLightCount = 26;

    [Tooltip("ライト候補を置く仮想グリッドの列数。")]
    [SerializeField, Range(1, 32)] private int m_gridColumns = 7;

    [Tooltip("ライト候補を置く仮想グリッドの行数。")]
    [SerializeField, Range(1, 32)] private int m_gridRows = 10;

    [Tooltip("Renderer boundsに対してライト配置エリアをどれだけ使うか。")]
    [SerializeField] private Vector2 m_gridFill = new Vector2(0.48f, 0.62f);

    [Tooltip("Renderer boundsに対するライト1個の大きさ。")]
    [SerializeField] private Vector2 m_windowSize = new Vector2(0.022f, 0.018f);

    [Tooltip("ビル表面から少し手前に出す距離。Zファイト防止用です。")]
    [SerializeField, Min(0f)] private float m_surfaceOffset = 0.4f;

    [Header("個別ライト点滅")]
    [SerializeField, ColorUsage(false, true)] private Color[] m_windowColors =
    {
        new Color(8f, 7.5f, 2.6f, 1f),
        new Color(2.6f, 8f, 9f, 1f),
        new Color(9f, 2.8f, 8f, 1f),
        new Color(3f, 9f, 4f, 1f),
        new Color(10f, 4.5f, 1.5f, 1f),
        new Color(5f, 3f, 10f, 1f)
    };

    [SerializeField, Range(1, 12)] private int m_maxSimultaneousLights = 7;

    [Tooltip("待機ごとに点灯を開始する確率。")]
    [SerializeField, Range(0f, 1f)] private float m_blinkChance = 0.98f;

    [Tooltip("次の点灯判定までの待機時間範囲（秒）。")]
    [SerializeField] private Vector2 m_waitTimeRange = new Vector2(0.05f, 0.45f);

    [Tooltip("点灯までの時間範囲（秒）。")]
    [SerializeField] private Vector2 m_lightUpTimeRange = new Vector2(0.01f, 0.025f);

    [Tooltip("点灯状態を維持する時間範囲（秒）。")]
    [SerializeField] private Vector2 m_litHoldRange = new Vector2(0.05f, 0.22f);

    [Tooltip("消灯までの時間範囲（秒）。")]
    [SerializeField] private Vector2 m_fadeOutTimeRange = new Vector2(0.015f, 0.05f);

    [Tooltip("0ならオブジェクトごとに自動で種を変えます。固定したい時だけ指定します。")]
    [SerializeField] private int m_randomSeed = 0;

    private readonly List<WindowState> m_states = new List<WindowState>();
    private readonly List<GameObject> m_generatedWindows = new List<GameObject>();
    private readonly List<Renderer> m_rendererTargets = new List<Renderer>();
    private Material m_windowMaterial;
    private int m_activeLightCount;

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        DestroyGeneratedWindows();
        ApplyBaseEmissionToAll(0f);
    }

    private void Update()
    {
        float now = Time.time;

        foreach (WindowState state in m_states)
        {
            UpdateState(state, now);
        }
    }

    private void OnValidate()
    {
        m_baseEmissionIntensity = Mathf.Clamp01(m_baseEmissionIntensity);
        m_windowLightCount = Mathf.Max(1, m_windowLightCount);
        m_gridColumns = Mathf.Max(1, m_gridColumns);
        m_gridRows = Mathf.Max(1, m_gridRows);
        m_gridFill = new Vector2(Mathf.Clamp01(m_gridFill.x), Mathf.Clamp01(m_gridFill.y));
        m_windowSize = new Vector2(Mathf.Max(0.001f, m_windowSize.x), Mathf.Max(0.001f, m_windowSize.y));
        m_maxSimultaneousLights = Mathf.Clamp(m_maxSimultaneousLights, 1, m_windowLightCount);
        if (m_windowColors == null || m_windowColors.Length == 0)
        {
            m_windowColors = new[] { new Color(8f, 7.5f, 2.6f, 1f) };
        }

        m_waitTimeRange = SanitizeRange(m_waitTimeRange, 0.01f);
        m_lightUpTimeRange = SanitizeRange(m_lightUpTimeRange, 0.001f);
        m_litHoldRange = SanitizeRange(m_litHoldRange, 0.001f);
        m_fadeOutTimeRange = SanitizeRange(m_fadeOutTimeRange, 0.001f);
    }

    private void Rebuild()
    {
        DestroyGeneratedWindows();
        CollectTargets();
        ApplyBaseEmissionToAll(m_baseEmissionIntensity);

        if (m_rendererTargets.Count == 0)
        {
            return;
        }

        Camera camera = ResolveCamera();
        EnsureWindowMaterial();

        int seed = m_randomSeed != 0 ? NormalizeSeed(m_randomSeed) : CreateAutoSeed();
        var random = new System.Random(seed);

        for (int i = 0; i < m_rendererTargets.Count; i++)
        {
            CreateWindowsForRenderer(m_rendererTargets[i], camera, random, i);
        }
    }

    private void CollectTargets()
    {
        m_rendererTargets.Clear();

        if (m_targetRenderers != null && m_targetRenderers.Length > 0)
        {
            foreach (Renderer target in m_targetRenderers)
            {
                AddTarget(target);
            }

            return;
        }

        if (m_includeChildrenWhenEmpty)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer target in renderers)
            {
                AddTarget(target);
            }
        }
        else if (TryGetComponent(out Renderer ownRenderer))
        {
            AddTarget(ownRenderer);
        }
    }

    private void AddTarget(Renderer target)
    {
        if (target != null && !m_rendererTargets.Contains(target))
        {
            m_rendererTargets.Add(target);
        }
    }

    private void CreateWindowsForRenderer(Renderer target, Camera camera, System.Random random, int rendererIndex)
    {
        Bounds bounds = target.bounds;
        Vector3 normal = GetFacingNormal(bounds, camera);
        Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }

        Vector3 up = Vector3.up;
        float width = Mathf.Abs(Vector3.Dot(bounds.size, AbsVector(right))) * m_gridFill.x;
        float height = bounds.size.y * m_gridFill.y;
        float windowWidth = Mathf.Max(0.01f, width * m_windowSize.x);
        float windowHeight = Mathf.Max(0.01f, height * m_windowSize.y);
        Vector3 faceCenter = GetFaceCenter(bounds, normal) + normal * m_surfaceOffset;

        List<int> cells = BuildShuffledCells(random);
        int count = Mathf.Min(m_windowLightCount, cells.Count);

        for (int i = 0; i < count; i++)
        {
            int cell = cells[i];
            int x = cell % m_gridColumns;
            int y = cell / m_gridColumns;
            float u = m_gridColumns == 1 ? 0.5f : x / (float)(m_gridColumns - 1);
            float v = m_gridRows == 1 ? 0.5f : y / (float)(m_gridRows - 1);
            float horizontal = (u - 0.5f) * width;
            float vertical = (v - 0.5f) * height;
            Vector3 position = faceCenter + right * horizontal + up * vertical;

            GameObject window = new GameObject("Generated Window Light");
            window.transform.SetParent(transform, false);
            window.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal, up));
            window.transform.localScale = new Vector3(windowWidth, windowHeight, 1f);

            var meshFilter = window.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetWindowMesh();

            var meshRenderer = window.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = m_windowMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var state = new WindowState
            {
                Renderer = meshRenderer,
                PropertyBlock = new MaterialPropertyBlock(),
                Random = new System.Random(NormalizeSeed(random.Next() + rendererIndex * 397 + i)),
                Phase = FlickerPhase.Waiting
            };

            ScheduleNextBlink(state, Time.time);
            ApplyWindowIntensity(state, 0f);
            m_states.Add(state);
            m_generatedWindows.Add(window);
        }
    }

    private void UpdateState(WindowState state, float now)
    {
        switch (state.Phase)
        {
            case FlickerPhase.Waiting:
                if (now >= state.NextBlinkTime)
                {
                    TryStartLight(state, now);
                }
                break;

            case FlickerPhase.Lighting:
            case FlickerPhase.Fading:
                UpdateTransition(state, now);
                break;

            case FlickerPhase.Holding:
                if (now >= state.HoldUntilTime)
                {
                    BeginTransition(state, FlickerPhase.Fading, 0f, GetRandomRange(state.Random, m_fadeOutTimeRange), now);
                }
                break;
        }
    }

    private void TryStartLight(WindowState state, float now)
    {
        if (m_activeLightCount >= m_maxSimultaneousLights || state.Random.NextDouble() > m_blinkChance)
        {
            ScheduleNextBlink(state, now);
            return;
        }

        m_activeLightCount++;
        state.LitColor = GetRandomWindowColor(state.Random);
        BeginTransition(state, FlickerPhase.Lighting, 1f, GetRandomRange(state.Random, m_lightUpTimeRange), now);
    }

    private void UpdateTransition(WindowState state, float now)
    {
        float t = state.TransitionDuration <= 0f ? 1f : Mathf.Clamp01((now - state.TransitionStartTime) / state.TransitionDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        state.CurrentIntensity = Mathf.Lerp(state.StartIntensity, state.TargetIntensity, eased);
        ApplyWindowIntensity(state, state.CurrentIntensity);

        if (t < 1f)
        {
            return;
        }

        state.CurrentIntensity = state.TargetIntensity;
        ApplyWindowIntensity(state, state.CurrentIntensity);

        if (state.Phase == FlickerPhase.Lighting)
        {
            state.Phase = FlickerPhase.Holding;
            state.HoldUntilTime = now + GetRandomRange(state.Random, m_litHoldRange);
            return;
        }

        m_activeLightCount = Mathf.Max(0, m_activeLightCount - 1);
        state.Phase = FlickerPhase.Waiting;
        ScheduleNextBlink(state, now);
    }

    private void BeginTransition(WindowState state, FlickerPhase phase, float targetIntensity, float duration, float now)
    {
        state.Phase = phase;
        state.StartIntensity = state.CurrentIntensity;
        state.TargetIntensity = Mathf.Clamp01(targetIntensity);
        state.TransitionStartTime = now;
        state.TransitionDuration = Mathf.Max(0.001f, duration);
    }

    private void ScheduleNextBlink(WindowState state, float now)
    {
        state.NextBlinkTime = now + GetRandomRange(state.Random, m_waitTimeRange);
    }

    private void ApplyBaseEmissionToAll(float intensity)
    {
        Color emission = m_baseEmissionColor * Mathf.Max(0f, intensity);
        emission.a = 1f;

        foreach (Renderer target in m_rendererTargets)
        {
            if (target == null || target.sharedMaterials == null || target.sharedMaterials.Length == 0)
            {
                continue;
            }

            int materialIndex = Mathf.Min(m_materialIndex, target.sharedMaterials.Length - 1);
            var block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block, materialIndex);
            block.SetColor(EmissionColorId, emission);
            target.SetPropertyBlock(block, materialIndex);
        }
    }

    private void ApplyWindowIntensity(WindowState state, float intensity)
    {
        if (state.Renderer == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(intensity);
        Color color = state.LitColor;
        color.r *= clamped;
        color.g *= clamped;
        color.b *= clamped;
        color.a = clamped;

        state.Renderer.GetPropertyBlock(state.PropertyBlock);
        state.PropertyBlock.SetColor(BaseColorId, color);
        state.PropertyBlock.SetColor(ColorId, color);
        state.PropertyBlock.SetColor(EmissionColorId, color);
        state.Renderer.SetPropertyBlock(state.PropertyBlock);
        state.Renderer.enabled = clamped > 0.001f;
    }

    private Color GetRandomWindowColor(System.Random random)
    {
        if (m_windowColors == null || m_windowColors.Length == 0)
        {
            return new Color(8f, 7.5f, 2.6f, 1f);
        }

        return m_windowColors[random.Next(m_windowColors.Length)];
    }

    private Camera ResolveCamera()
    {
        if (m_viewCamera != null)
        {
            return m_viewCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera;
        }

        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.isActiveAndEnabled)
            {
                return camera;
            }
        }

        return null;
    }

    private Vector3 GetFacingNormal(Bounds bounds, Camera camera)
    {
        Vector3 viewDirection = camera != null ? (camera.transform.position - bounds.center).normalized : -transform.forward;
        if (Mathf.Abs(viewDirection.x) > Mathf.Abs(viewDirection.z))
        {
            return new Vector3(Mathf.Sign(viewDirection.x), 0f, 0f);
        }

        return new Vector3(0f, 0f, Mathf.Sign(viewDirection.z));
    }

    private Vector3 GetFaceCenter(Bounds bounds, Vector3 normal)
    {
        return bounds.center + new Vector3(
            normal.x * bounds.extents.x,
            0f,
            normal.z * bounds.extents.z);
    }

    private List<int> BuildShuffledCells(System.Random random)
    {
        int cellCount = m_gridColumns * m_gridRows;
        var cells = new List<int>(cellCount);
        for (int i = 0; i < cellCount; i++)
        {
            cells.Add(i);
        }

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            int value = cells[i];
            cells[i] = cells[swapIndex];
            cells[swapIndex] = value;
        }

        return cells;
    }

    private void EnsureWindowMaterial()
    {
        if (m_windowMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        m_windowMaterial = new Material(shader)
        {
            name = "Generated Title Window Light",
            renderQueue = (int)RenderQueue.Transparent
        };
        m_windowMaterial.SetOverrideTag("RenderType", "Transparent");
        m_windowMaterial.SetFloat("_Surface", 1f);
        m_windowMaterial.SetFloat("_Blend", 1f);
        m_windowMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
        m_windowMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        m_windowMaterial.SetFloat("_ZWrite", 0f);
        m_windowMaterial.DisableKeyword("_ALPHATEST_ON");
        m_windowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void DestroyGeneratedWindows()
    {
        m_states.Clear();
        m_activeLightCount = 0;

        foreach (GameObject window in m_generatedWindows)
        {
            if (window != null)
            {
                Destroy(window);
            }
        }

        m_generatedWindows.Clear();

        if (m_windowMaterial != null)
        {
            Destroy(m_windowMaterial);
            m_windowMaterial = null;
        }
    }

    private static Mesh GetWindowMesh()
    {
        if (s_windowMesh != null)
        {
            return s_windowMesh;
        }

        s_windowMesh = new Mesh
        {
            name = "Generated Title Window Quad"
        };
        s_windowMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        s_windowMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        s_windowMesh.RecalculateBounds();
        s_windowMesh.RecalculateNormals();
        return s_windowMesh;
    }

    private float GetRandomRange(System.Random random, Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
    }

    private int CreateAutoSeed()
    {
        unchecked
        {
            int seed = 17;
            seed = seed * 31 + GetInstanceID();
            seed = seed * 31 + transform.position.GetHashCode();
            return NormalizeSeed(seed);
        }
    }

    private int NormalizeSeed(int seed)
    {
        seed &= int.MaxValue;
        return seed == 0 ? 1 : seed;
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static Vector2 SanitizeRange(Vector2 range, float min)
    {
        float x = Mathf.Max(min, range.x);
        float y = Mathf.Max(min, range.y);
        if (y < x)
        {
            y = x;
        }

        return new Vector2(x, y);
    }
}
