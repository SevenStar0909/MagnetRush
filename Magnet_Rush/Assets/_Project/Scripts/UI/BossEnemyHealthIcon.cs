using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossEnemyHealthIcon : MonoBehaviour
{
    [Header("出現させるプレイヤーとの距離")]
    [SerializeField] private float m_displayDistance = 15f;

    [Header("UI設定")]
    [SerializeField] private GameObject m_iconPrefab;
    [SerializeField] private Transform m_iconContainer;

    private EnemyBossAI m_boss;
    private Transform m_bossTarget;
    private Transform m_playerTarget;

    private bool m_warnedNoBossTag;
    private bool m_isSpawned = false;
    private int m_alive;
    private bool m_subscribed;

    private List<BossIcon> m_activeIcons = new List<BossIcon>();

    void Start()
    {
        // プレイヤーのTransformを取得
        GameObject playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_playerTarget = playerObj.transform;
        }

        if (!TryResolveBoss())
        { ChannelLogger.LogGuardReturn("Stab", "Boss未配置"); }
    }

    void OnEnable()
    {
        // OnEnable は Start より前に走るため、初回は m_boss がまだ未解決(null)。
        // ボスが見つかった時点(TryResolveBoss)で購読するので、ここでは解決済みのときだけ購読する。
        TrySubscribe();
    }

    void OnDisable()
    {
        if (m_boss != null && m_subscribed)
        {
            m_boss.OnStabHitSucceeded -= DestroyOneIcon;
            m_subscribed = false;
        }
    }

    /// <summary>ボスが解決済みでまだ購読していなければイベント購読する。多重購読しない。</summary>
    private void TrySubscribe()
    {
        if (m_subscribed || m_boss == null) return;

        m_boss.OnStabHitSucceeded += DestroyOneIcon;
        m_subscribed = true;
    }

    void Update()
    {
        // すでに生成済み、またはプレイヤーやボスが見つからない場合は何もしない
        if (m_isSpawned || m_playerTarget == null || !TryResolveBoss()) return;

        // プレイヤーとボスの距離を計測
        float distance = Vector3.Distance(m_playerTarget.position, m_bossTarget.position);

        // 一定距離以内に近づいたらアイコンを生成
        if (distance <= m_displayDistance)
        {
            SpawnBossIcons();
        }
    }

    /// <summary>
    /// ボスのHP本数分、アイコンを生成して並べる
    /// </summary>
    private void SpawnBossIcons()
    {
        m_isSpawned = true;
        m_alive = m_boss.Settings.healthBarSegments;

        for (int i = 0; i < m_alive; i++)
        {
            GameObject iconObj = Instantiate(m_iconPrefab, m_iconContainer);
            BossIcon icon = iconObj.GetComponent<BossIcon>();

            if (icon != null)
            {
                m_activeIcons.Add(icon);
            }
        }
    }

    /// <summary>
    /// スタブ成功時、一番右端のアイコンを演出付きで破壊する
    /// </summary>
    private void DestroyOneIcon()
    {
        if (m_alive <= 0 || m_activeIcons.Count == 0) return;

        m_alive--;

        // リストの「最後（一番右端）」のアイコンを取得して演出開始、リストからは除外
        int lastIndex = m_activeIcons.Count - 1;
        BossIcon targetIcon = m_activeIcons[lastIndex];
        m_activeIcons.RemoveAt(lastIndex);

        if (targetIcon != null)
        {
            targetIcon.PlayExplosionAndDestroy();
        }
    }

    private bool TryResolveBoss()
    {
        if (m_bossTarget != null) return true;

        GameObject bossObj;
        try { bossObj = GameObject.FindWithTag("Boss"); }
        catch (UnityException)
        {
            if (!m_warnedNoBossTag)
            {
                ChannelLogger.LogWarning("Stab", "Bossタグが未登録 (TagManager に Boss を追加)");
                m_warnedNoBossTag = true;
            }
            return false;
        }

        if (bossObj == null) return false;
        m_bossTarget = bossObj.transform;

        if (!bossObj.TryGetComponent<EnemyBossAI>(out m_boss))
        {
            ChannelLogger.LogWarning("Stab", "Bossオブジェクトに EnemyBossAI コンポーネントが見つかりません");
            return false;
        }

        // ボスが解決できたこのタイミングで初めてイベント購読できる（OnEnable では間に合わない）
        TrySubscribe();
        return true;
    }
}