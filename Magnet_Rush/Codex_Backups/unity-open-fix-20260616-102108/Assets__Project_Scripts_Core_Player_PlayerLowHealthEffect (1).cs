using System;
using UnityEngine;

/// <summary>
/// Player のHPが指定値以下になったとき、継続VFXをプレイヤーに追従させて再生する。
/// </summary>
[DisallowMultipleComponent]
public class PlayerLowHealthEffect : MonoBehaviour
{
    private enum ThresholdMode
    {
        Ratio,
        CurrentHp
    }

    [Serializable]
    private sealed class BodyPartEffect
    {
        [Label("表示名")]
        [SerializeField] private string m_label;

        [Label("有効")]
        [SerializeField] private bool m_enabled = true;

        [Label("対象ボーン")]
        [Tooltip("AnimatorがHumanoidならここから自動でTransformを取得")]
        [SerializeField] private HumanBodyBones m_bone = HumanBodyBones.Chest;

        [Label("アンカー上書き")]
        [Tooltip("ボーン自動取得が合わない場合だけ、ここにTransformを直接指定")]
        [SerializeField] private Transform m_anchorOverride;

        [Label("位置オフセット")]
        [SerializeField] private Vector3 m_localPositionOffset = Vector3.zero;

        [Label("回転オフセット")]
        [SerializeField] private Vector3 m_localEulerOffset = Vector3.zero;

        [Label("部位スケール倍率")]
        [Tooltip("この部位に生成するエフェクト全体の大きさ")]
        [Min(0f)]
        [SerializeField] private float m_scaleMultiplier = 0.2f;

        [Label("数倍率")]
        [Tooltip("発生数と最大粒子数に掛ける倍率。0でほぼ非表示、1でPrefab標準")]
        [Min(0f)]
        [SerializeField] private float m_countMultiplier = 0.4f;

        [Label("サイズ倍率")]
        [Tooltip("粒子のStart Sizeに掛ける倍率。1でPrefab標準")]
        [Min(0f)]
        [SerializeField] private float m_sizeMultiplier = 0.35f;

        public string Label => m_label;
        public bool Enabled => m_enabled;
        public HumanBodyBones Bone => m_bone;
        public Transform AnchorOverride => m_anchorOverride;
        public Vector3 LocalPositionOffset => m_localPositionOffset;
        public Quaternion LocalRotationOffset => Quaternion.Euler(m_localEulerOffset);
        public float ScaleMultiplier => Mathf.Max(0f, m_scaleMultiplier);
        public float CountMultiplier => Mathf.Max(0f, m_countMultiplier);
        public float SizeMultiplier => Mathf.Max(0f, m_sizeMultiplier);

        public BodyPartEffect()
        {
        }

        public BodyPartEffect(string label, HumanBodyBones bone, Vector3 localPositionOffset, float scaleMultiplier, float countMultiplier, float sizeMultiplier)
        {
            m_label = label;
            m_bone = bone;
            m_localPositionOffset = localPositionOffset;
            m_scaleMultiplier = scaleMultiplier;
            m_countMultiplier = countMultiplier;
            m_sizeMultiplier = sizeMultiplier;
        }

        public void Validate()
        {
            m_scaleMultiplier = Mathf.Max(0f, m_scaleMultiplier);
            m_countMultiplier = Mathf.Max(0f, m_countMultiplier);
            m_sizeMultiplier = Mathf.Max(0f, m_sizeMultiplier);
        }
    }

    private struct ParticleDefaults
    {
        public ParticleSystem particleSystem;
        public float startSizeMultiplier;
        public float rateOverTimeMultiplier;
        public int maxParticles;
    }

    private sealed class BodyPartEffectInstance
    {
        public BodyPartEffect settings;
        public Transform anchor;
        public GameObject effectInstance;
        public ParticleSystem[] particleSystems;
        public ParticleDefaults[] particleDefaults;
    }

    [Header("[低体力エフェクト]")]
    [Label("監視するHP")]
    [Tooltip("未設定なら同じGameObjectから自動取得")]
    [SerializeField] private Health m_health;

    [Label("エフェクトPrefab")]
    [Tooltip("低体力時にプレイヤー配下へ生成する継続VFX。ボススタン用の P_FX_Electrication を想定")]
    [SerializeField] private GameObject m_effectPrefab;

    [Label("エフェクト親")]
    [Tooltip("ボーンが見つからない部位のフォールバック先。未設定ならこのGameObjectのTransformを使う")]
    [SerializeField] private Transform m_effectParent;

    [Label("しきい値モード")]
    [Tooltip("Ratio=現在HP/最大HPで判定、CurrentHp=現在HPの値で判定")]
    [SerializeField] private ThresholdMode m_thresholdMode = ThresholdMode.Ratio;

    [LabelRange("発動HP割合", 0f, 1f)]
    [Tooltip("現在HP/最大HPがこの値以下になったらエフェクトを出す。0.25 = 25%以下")]
    [SerializeField] private float m_healthRatioThreshold = 0.25f;

    [Label("発動HP値")]
    [Tooltip("しきい値モードが CurrentHp のとき、現在HPがこの値以下になったらエフェクトを出す")]
    [SerializeField] private int m_healthValueThreshold = 10;

    [Header("[配置]")]
    [Label("ローカル位置オフセット")]
    [Tooltip("生成したエフェクトの親からの位置オフセット")]
    [SerializeField] private Vector3 m_localPositionOffset = Vector3.zero;

    [Label("プレイヤー高さで自動スケール")]
    [Tooltip("Entity/Colliderの高さを基準にVFX全体のスケールを調整する")]
    [SerializeField] private bool m_autoScaleToPlayerHeight = true;

    [Label("基準身長")]
    [Tooltip("この高さのときスケール1として扱う")]
    [Min(0.01f)]
    [SerializeField] private float m_referenceHeight = 2f;

    [Label("スケール倍率")]
    [Tooltip("自動計算後に掛ける倍率。見た目が大きすぎる/小さすぎるときに調整")]
    [Min(0f)]
    [SerializeField] private float m_scaleMultiplier = 1f;

    [Label("死亡中も表示")]
    [Tooltip("OFFならHP0の死亡中はエフェクトを止める")]
    [SerializeField] private bool m_showWhileDead;

    [Header("[部位ごとの小電撃]")]
    [Label("部位ごとの設定")]
    [Tooltip("胸、肩、足など、部位ごとに生成する小さな電撃を調整する")]
    [SerializeField] private BodyPartEffect[] m_bodyPartEffects =
    {
        new BodyPartEffect("胸", HumanBodyBones.Chest, new Vector3(0f, 0f, 0.02f), 0.18f, 0.4f, 0.35f),
        new BodyPartEffect("左肩", HumanBodyBones.LeftShoulder, new Vector3(0f, 0f, 0f), 0.14f, 0.35f, 0.3f),
        new BodyPartEffect("右肩", HumanBodyBones.RightShoulder, new Vector3(0f, 0f, 0f), 0.14f, 0.35f, 0.3f),
        new BodyPartEffect("左足", HumanBodyBones.LeftFoot, new Vector3(0f, 0.02f, 0f), 0.13f, 0.3f, 0.28f),
        new BodyPartEffect("右足", HumanBodyBones.RightFoot, new Vector3(0f, 0.02f, 0f), 0.13f, 0.3f, 0.28f)
    };

    private Animator m_animator;
    private BodyPartEffectInstance[] m_effectInstances;
    private bool m_runtimeRebuildRequested;
    private bool m_isPlaying;

    private void Awake()
    {
        if (m_health == null)
            m_health = GetComponent<Health>();

        if (m_effectParent == null)
            m_effectParent = transform;

        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        SubscribeHealth();
    }

    private void Start()
    {
        RefreshEffectState();
    }

    private void Update()
    {
        if (!m_runtimeRebuildRequested) return;

        m_runtimeRebuildRequested = false;
        RebuildRuntimeEffects();
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
        StopEffect();
    }

    private void OnDestroy()
    {
        UnsubscribeHealth();
        DestroyEffectInstances();
    }

    private void SubscribeHealth()
    {
        if (m_health == null) return;

        m_health.OnDamage += HandleHealthChanged;
        m_health.OnHeal += HandleHealthChanged;
        m_health.OnHealthReset += RefreshEffectState;
        m_health.OnDie += HandleDie;
    }

    private void UnsubscribeHealth()
    {
        if (m_health == null) return;

        m_health.OnDamage -= HandleHealthChanged;
        m_health.OnHeal -= HandleHealthChanged;
        m_health.OnHealthReset -= RefreshEffectState;
        m_health.OnDie -= HandleDie;
    }

    private void HandleHealthChanged(int amount)
    {
        RefreshEffectState();
    }

    private void HandleDie()
    {
        RefreshEffectState();
    }

    private void RefreshEffectState()
    {
        if (ShouldPlayEffect())
            PlayEffect();
        else
            StopEffect();
    }

    private bool ShouldPlayEffect()
    {
        if (m_health == null || m_effectPrefab == null) return false;
        if (!m_showWhileDead && m_health.IsDead) return false;

        switch (m_thresholdMode)
        {
            case ThresholdMode.CurrentHp:
                return m_health.CurrentHealth <= Mathf.Max(0, m_healthValueThreshold);
            default:
                return m_health.HealthRatio <= Mathf.Clamp01(m_healthRatioThreshold);
        }
    }

    private void PlayEffect()
    {
        EnsureEffectInstances();
        if (m_effectInstances == null) return;

        ApplyEffectTransforms();

        if (m_isPlaying) return;

        foreach (var instance in m_effectInstances)
        {
            if (instance?.particleSystems == null) continue;

            foreach (var ps in instance.particleSystems)
            {
                if (ps != null)
                    ps.Play(true);
            }
        }

        m_isPlaying = true;
    }

    private void StopEffect()
    {
        if (m_effectInstances == null) return;

        foreach (var instance in m_effectInstances)
        {
            if (instance?.particleSystems == null) continue;

            foreach (var ps in instance.particleSystems)
            {
                if (ps != null)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        m_isPlaying = false;
    }

    private void RebuildRuntimeEffects()
    {
        bool shouldPlay = ShouldPlayEffect();
        DestroyEffectInstances();

        if (shouldPlay)
            PlayEffect();
    }

    private void DestroyEffectInstances()
    {
        if (m_effectInstances == null) return;

        foreach (var instance in m_effectInstances)
        {
            if (instance?.effectInstance == null) continue;

            if (Application.isPlaying)
                Destroy(instance.effectInstance);
            else
                DestroyImmediate(instance.effectInstance);
        }

        m_effectInstances = null;
        m_isPlaying = false;
    }

    private void EnsureEffectInstances()
    {
        if (m_effectInstances != null) return;
        if (m_effectPrefab == null) return;
        if (m_bodyPartEffects == null || m_bodyPartEffects.Length == 0) return;

        m_effectInstances = new BodyPartEffectInstance[m_bodyPartEffects.Length];
        for (int i = 0; i < m_bodyPartEffects.Length; i++)
        {
            BodyPartEffect settings = m_bodyPartEffects[i];
            if (settings == null || !settings.Enabled) continue;

            Transform anchor = ResolveAnchor(settings);
            if (anchor == null) continue;

            GameObject instance = Instantiate(m_effectPrefab, anchor, false);
            instance.name = string.IsNullOrWhiteSpace(settings.Label)
                ? m_effectPrefab.name
                : $"{m_effectPrefab.name}_{settings.Label}";

            var effectInstance = new BodyPartEffectInstance
            {
                settings = settings,
                anchor = anchor,
                effectInstance = instance,
                particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true)
            };
            CacheParticleDefaults(effectInstance);
            ApplyEffectTransform(effectInstance);
            ApplyParticleTuning(effectInstance);

            m_effectInstances[i] = effectInstance;
        }

        StopEffect();
    }

    private void CacheParticleDefaults(BodyPartEffectInstance instance)
    {
        if (instance == null || instance.particleSystems == null)
        {
            if (instance != null)
                instance.particleDefaults = null;
            return;
        }

        instance.particleDefaults = new ParticleDefaults[instance.particleSystems.Length];
        for (int i = 0; i < instance.particleSystems.Length; i++)
        {
            ParticleSystem ps = instance.particleSystems[i];
            if (ps == null) continue;

            var main = ps.main;
            var emission = ps.emission;
            instance.particleDefaults[i] = new ParticleDefaults
            {
                particleSystem = ps,
                startSizeMultiplier = main.startSizeMultiplier,
                rateOverTimeMultiplier = emission.rateOverTimeMultiplier,
                maxParticles = main.maxParticles
            };
        }
    }

    private Transform ResolveAnchor(BodyPartEffect settings)
    {
        if (settings.AnchorOverride != null)
            return settings.AnchorOverride;

        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);

        if (m_animator != null && m_animator.isHuman)
        {
            Transform bone = GetBoneTransformWithFallback(settings.Bone);
            if (bone != null)
                return bone;
        }

        return m_effectParent != null ? m_effectParent : transform;
    }

    private Transform GetBoneTransformWithFallback(HumanBodyBones bone)
    {
        Transform result = m_animator.GetBoneTransform(bone);
        if (result != null)
            return result;

        switch (bone)
        {
            case HumanBodyBones.Chest:
                return m_animator.GetBoneTransform(HumanBodyBones.Spine);
            case HumanBodyBones.LeftShoulder:
                return m_animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            case HumanBodyBones.RightShoulder:
                return m_animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            default:
                return null;
        }
    }

    private void ApplyEffectTransforms()
    {
        if (m_effectInstances == null) return;

        foreach (var instance in m_effectInstances)
        {
            ApplyEffectTransform(instance);
            ApplyParticleTuning(instance);
        }
    }

    private void ApplyEffectTransform(BodyPartEffectInstance instance)
    {
        if (instance?.effectInstance == null || instance.settings == null) return;

        Transform effectTransform = instance.effectInstance.transform;
        effectTransform.localPosition = m_localPositionOffset + instance.settings.LocalPositionOffset;
        effectTransform.localRotation = instance.settings.LocalRotationOffset;
        effectTransform.localScale = Vector3.one * CalculateEffectScale() * instance.settings.ScaleMultiplier;
    }

    private void ApplyParticleTuning(BodyPartEffectInstance instance)
    {
        if (instance?.particleDefaults == null || instance.settings == null) return;

        float countMultiplier = instance.settings.CountMultiplier;
        float sizeMultiplier = instance.settings.SizeMultiplier;

        for (int i = 0; i < instance.particleDefaults.Length; i++)
        {
            ParticleDefaults defaults = instance.particleDefaults[i];
            ParticleSystem ps = defaults.particleSystem;
            if (ps == null) continue;

            var main = ps.main;
            main.startSizeMultiplier = defaults.startSizeMultiplier * sizeMultiplier;
            main.maxParticles = Mathf.Max(0, Mathf.RoundToInt(defaults.maxParticles * countMultiplier));

            var emission = ps.emission;
            emission.rateOverTimeMultiplier = defaults.rateOverTimeMultiplier * countMultiplier;
        }
    }

    private float CalculateEffectScale()
    {
        float scale = Mathf.Max(0f, m_scaleMultiplier);
        if (!m_autoScaleToPlayerHeight)
            return scale;

        float height = 0f;
        var entity = GetComponent<Entity>();
        if (entity != null)
            height = entity.height;

        if (height <= 0f)
        {
            var capsule = GetComponentInChildren<CapsuleCollider>(true);
            if (capsule != null)
                height = capsule.height * Mathf.Abs(capsule.transform.lossyScale.y);
        }

        if (height <= 0f)
            height = m_referenceHeight;

        return scale * Mathf.Max(0.01f, height / Mathf.Max(0.01f, m_referenceHeight));
    }

    private void OnValidate()
    {
        m_healthRatioThreshold = Mathf.Clamp01(m_healthRatioThreshold);
        m_healthValueThreshold = Mathf.Max(0, m_healthValueThreshold);
        m_referenceHeight = Mathf.Max(0.01f, m_referenceHeight);
        m_scaleMultiplier = Mathf.Max(0f, m_scaleMultiplier);

        if (m_bodyPartEffects != null)
        {
            foreach (var part in m_bodyPartEffects)
            {
                if (part != null)
                    part.Validate();
            }
        }

        if (Application.isPlaying)
        {
            m_runtimeRebuildRequested = true;
        }
    }
}
