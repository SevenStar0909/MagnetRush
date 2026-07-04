using UnityEngine;

/// <summary>
/// ボスのミサイル発射。AnimationEvent 経由で EnemyBossAI から呼ばれ、各発射点から弧を描くミサイルを生成する。
/// 角度・弧などの数値は BossMissileSettings(SO) に外出し。発射点とミサイルPrefabはプレハブ階層依存なのでここで持つ。
/// アニメの2イベントで 上2発 → 左上/右上1発ずつ と交互に撃つ（計4発）。
/// 依存: BossMissileSettings, EnemyMissile, EnemyBossBase, Sound
/// </summary>
[RequireComponent(typeof(EnemyBossBase))]
public class BossMissileLauncher : MonoBehaviour
{
    [Tooltip("ミサイル発射パラメータ（角度・弧・自爆防止）")]
    [SerializeField] private BossMissileSettings m_settings;

    [Tooltip("生成するミサイルPrefab")]
    [SerializeField] private EnemyMissile m_missilePrefab;

    [Tooltip("ミサイル発射時に発射口から出す爆発エフェクト。AxeEnemy死亡時と同じPrefabを想定")]
    [SerializeField] private GameObject m_launchExplosionEffectPrefab;

    [SerializeField] private float m_launchExplosionEffectLifetime = 0.75f;

    [SerializeField] private float m_launchExplosionEffectScale = 2f;

    [Tooltip("ミサイル生成位置。未設定ならこのオブジェクト位置を使用")]
    [SerializeField] private Transform[] m_missileSpawnPoints;

    [Tooltip("各生成位置のローカルオフセット。m_missileSpawnPoints と同じ順番で指定")]
    [SerializeField] private Vector3[] m_missileSpawnOffsets;

    private EnemyBossBase m_boss;
    // 次の発射で左右上ミサイルを撃つか。アニメの2イベントで 上→左右上 と切り替える。
    private bool m_nextMissileIsSideLob;
    private int m_firedWaveCount;
    private const int MaxWavesPerAttack = 2;

    private enum MissileWavePattern { Forward, Up, LeftRightUp }

    private void Awake()
    {
        m_boss = GetComponent<EnemyBossBase>();
    }

    /// <summary>Missile ステート突入時に呼び、発射パターンを先頭（上2発）へ戻す。</summary>
    public void ResetWave()
    {
        m_nextMissileIsSideLob = false;
        m_firedWaveCount = 0;
    }

    /// <summary>
    /// 1波発射する。設定 or ミサイルPrefab 未アサイン時は何もしない。
    /// </summary>
    /// <returns>このミサイル攻撃で必要な波数を撃ち終えたら true。</returns>
    public bool FireNextWave()
    {
        if (m_firedWaveCount >= MaxWavesPerAttack)
            return true;

        if (m_settings == null || m_missilePrefab == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "BossMissileLauncher: Settings か MissilePrefab が未アサイン");
            return true;
        }

        MissileWavePattern pattern = m_settings.fireLobMissiles
            ? (m_nextMissileIsSideLob ? MissileWavePattern.LeftRightUp : MissileWavePattern.Up)
            : MissileWavePattern.Forward;

        FireMissileWave(pattern);
        // ボス位置の3D再生（離れていると小さく聞こえる）。音量・減衰距離はサウンド調整シートで管理
        Sound.PlayAt(SoundData.CueSheet.SE, SoundData.SE.MissileShot, transform.position);
        m_nextMissileIsSideLob = !m_nextMissileIsSideLob;
        m_firedWaveCount++;

        return m_firedWaveCount >= MaxWavesPerAttack;
    }

    /// <summary>1波分。全発射点からパターンに応じた初期方向で1発ずつ撃つ。</summary>
    private void FireMissileWave(MissileWavePattern pattern)
    {
        if (!HasAnyMissileSpawnPoint())
        {
            SpawnMissileAt(transform, Vector3.zero, pattern, 0, pattern == MissileWavePattern.LeftRightUp ? 2 : 1);
            if (pattern == MissileWavePattern.LeftRightUp)
                SpawnMissileAt(transform, Vector3.zero, pattern, 1, 2);
            return;
        }

        int spawnCount = m_missileSpawnPoints.Length;
        for (int i = 0; i < m_missileSpawnPoints.Length; i++)
        {
            Transform spawnPoint = m_missileSpawnPoints[i];
            if (spawnPoint == null) continue;

            Vector3 offset = Vector3.zero;
            if (m_missileSpawnOffsets != null && i < m_missileSpawnOffsets.Length)
                offset = m_missileSpawnOffsets[i];

            SpawnMissileAt(spawnPoint, offset, pattern, i, spawnCount);
        }
    }

    private bool HasAnyMissileSpawnPoint()
    {
        if (m_missileSpawnPoints == null || m_missileSpawnPoints.Length == 0)
            return false;

        for (int i = 0; i < m_missileSpawnPoints.Length; i++)
        {
            if (m_missileSpawnPoints[i] != null)
                return true;
        }

        return false;
    }

    private Vector3 ComputeMissileDirection(MissileWavePattern pattern, int spawnIndex)
    {
        switch (pattern)
        {
            case MissileWavePattern.Up:
                return ComputeUpDirection();

            case MissileWavePattern.LeftRightUp:
                return ComputeSideUpDirection(spawnIndex);

            default:
                return transform.forward;
        }
    }

    /// <summary>上ミサイルの初期方向。ボス正面を上へ lobAngle 度だけ傾ける。</summary>
    private Vector3 ComputeUpDirection()
    {
        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude <= 0.0001f) fwd = Vector3.forward;
        return Vector3.RotateTowards(fwd, Vector3.up, m_settings.lobAngle * Mathf.Deg2Rad, 0f).normalized;
    }

    /// <summary>左上/右上ミサイルの初期方向。</summary>
    private Vector3 ComputeSideUpDirection(int spawnIndex)
    {
        float sideSign = spawnIndex % 2 == 0 ? -1f : 1f;
        Vector3 side = transform.right * sideSign;
        float angle = Mathf.Clamp(m_settings.sideLobAngle, 0f, 89f) * Mathf.Deg2Rad;
        Vector3 direction = Vector3.up * Mathf.Cos(angle) + side * Mathf.Sin(angle);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
    }

    private Vector3 ComputeLaunchDirection(Transform spawnPoint)
    {
        Vector3 bossForward = transform.forward;
        if (bossForward.sqrMagnitude <= 0.0001f)
            bossForward = Vector3.forward;

        Vector3 fwd = spawnPoint != null ? spawnPoint.forward : bossForward;
        if (fwd.sqrMagnitude <= 0.0001f)
            fwd = bossForward;

        // 左右でミラーされた発射口は forward が後ろ向きになることがある。
        // その場合だけボス正面を使い、発射直後に背面へ飛ぶ見た目を防ぐ。
        if (Vector3.Dot(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.ProjectOnPlane(bossForward, Vector3.up)) < 0f)
            fwd = bossForward;

        float launchAngle = Mathf.Clamp(m_settings.launchAngle, 0f, 89f) * Mathf.Deg2Rad;
        return Vector3.RotateTowards(fwd.normalized, Vector3.up, launchAngle, 0f).normalized;
    }

    private float ComputeArcLaneOffset(MissileWavePattern pattern, int spawnIndex, int spawnCount)
    {
        if (pattern != MissileWavePattern.Up || spawnCount <= 1)
            return 0f;

        float center = (spawnCount - 1) * 0.5f;
        return (spawnIndex - center) * m_settings.arcLaneSpacing;
    }

    private void SpawnMissileAt(Transform spawnPoint, Vector3 localOffset, MissileWavePattern pattern, int spawnIndex, int spawnCount)
    {
        Vector3 spawnPos = spawnPoint.position + spawnPoint.TransformDirection(localOffset);
        Vector3 formationDirection = ComputeMissileDirection(pattern, spawnIndex);
        bool useArcFlight = pattern != MissileWavePattern.Forward;
        Vector3 launchDirection = useArcFlight ? ComputeLaunchDirection(spawnPoint) : formationDirection;

        if (launchDirection.sqrMagnitude <= 0.0001f)
            launchDirection = spawnPoint.forward;
        launchDirection = launchDirection.normalized;

        Quaternion rotation = Quaternion.LookRotation(launchDirection, Vector3.up);
        SpawnLaunchExplosionEffect(spawnPos, rotation);

        EnemyMissile missile = Instantiate(m_missilePrefab, spawnPos, rotation);
        Transform player = m_boss != null ? m_boss.Player : null;
        if (useArcFlight)
        {
            float laneOffset = ComputeArcLaneOffset(pattern, spawnIndex, spawnCount);
            missile.InitializeArc(
                player,
                launchDirection,
                formationDirection,
                m_settings.lobRiseTime,
                m_settings.arcHeight,
                m_settings.arcSpreadDistance,
                laneOffset
            );
        }
        else
        {
            missile.Initialize(player, launchDirection, -1f);
        }
        // ミサイルは PhysicsObject なので発射元ボスの Pushbox 等と衝突してしまう。spawn 即爆発・自傷を防ぐ。
        missile.IgnoreCollisionsWith(gameObject, m_settings.collisionGrace);
    }

    private void SpawnLaunchExplosionEffect(Vector3 position, Quaternion rotation)
    {
        if (m_launchExplosionEffectPrefab == null)
            return;

        GameObject effectObject = Instantiate(m_launchExplosionEffectPrefab, position, rotation);
        effectObject.transform.localScale *= Mathf.Max(0f, m_launchExplosionEffectScale);
        if (m_launchExplosionEffectLifetime > 0f)
            Destroy(effectObject, m_launchExplosionEffectLifetime);
    }
}
