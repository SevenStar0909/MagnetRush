using UnityEngine;

/// <summary>
/// スタブ攻撃能力。RB 入力でボススタン中＋接近時に StabPlayerState へ遷移し、AnimEvent でヒット通知する。
/// 基底: Ability（共通の依存 m_input / m_player / m_events / m_states は基底で取得済み）
/// Boss は Additive シーンで遅延ロードされるため Awake では取得せず、初回 Stab() 呼び出し時に解決する。
/// </summary>
public class StabAbility : Ability
{
    private Transform m_bossTarget;
    private IStabReceiver m_bossReceiver;
    private bool m_warnedNoBossTag;

    public bool CanStabNow
    {
        get
        {
            if (m_states == null || m_player == null) return false;
            if (m_states.IsCurrentOfType<BossStabFinisherState>()) return false;
            if (m_player.IsAbilityLocked(PlayerAbilityType.Stab)) return false;
            if (m_player.Settings == null) return false;
            if (!TryResolveBoss()) return false;
            if (m_bossReceiver == null || !m_bossReceiver.CanReceiveStab) return false;
            if (m_bossTarget == null) return false;

            return Vector3.Distance(m_player.transform.position, m_bossTarget.position) <= m_player.Settings.stabRange;
        }
    }

    /// <summary>RB 入力でボススタン中＋接近時に StabPlayerState へ遷移する。</summary>
    public void Stab()
    {
        if (!m_input.IsStabPressed) return;

        // 既に演出中なら再解決・再突入しない（複数ボス時に対象ボスが切り替わるのを防ぐ）。
        if (m_states.IsCurrentOfType<BossStabFinisherState>())
        { ChannelLogger.LogGuardReturn("Stab", "スタブ演出中のため再突入しない"); return; }

        if (m_player.Settings == null)
        { ChannelLogger.LogGuardReturn("Stab", "PlayerSettings未設定"); return; }

        if (!TryResolveBoss())
        { ChannelLogger.LogGuardReturn("Stab", "Boss未配置"); return; }

        if (m_bossReceiver != null && !m_bossReceiver.CanReceiveStab)
        { ChannelLogger.LogGuardReturn("Stab", "ボスが崩れていない（スタン/よろけでない）"); return; }

        if (Vector3.Distance(m_player.transform.position, m_bossTarget.position) > m_player.Settings.stabRange)
        { ChannelLogger.LogGuardReturn("Stab", "ボスの距離がスタブ攻撃の範囲外"); return; }

        m_input.ConsumeStab();

        // ボスが自分の演出設定を持っていればそれを使う。無ければプレイヤー共通にフォールバック。
        var bossSettings = m_bossReceiver != null ? m_bossReceiver.StabFinisherSettings : null;
        var finisherSettings = bossSettings != null ? bossSettings : m_player.Settings.stabFinisherSettings;
        if (finisherSettings == null || m_bossReceiver == null)
        {
            ChannelLogger.LogGuardReturn("Stab", "StabFinisherSettings未設定 or Receiver無し — 旧その場スタブにフォールバック");
            m_states.Change<StabPlayerState>();
            return;
        }

        var profile = finisherSettings.GetProfile(m_bossReceiver.StabChoreographyIndex);
        var finisher = m_states.Get<BossStabFinisherState>();
        if (finisher == null)
        {
            ChannelLogger.LogGuardReturn("Stab", "BossStabFinisherState未登録 — 旧その場スタブにフォールバック");
            m_states.Change<StabPlayerState>();
            return;
        }
        finisher.Setup(profile, m_bossReceiver);
        m_states.Change<BossStabFinisherState>();
    }

    /// <summary>AnimEvent から呼ばれるヒット通知。突き刺しの瞬間に発火。</summary>
    public void OnStabHitEvent()
    {
        if (m_bossReceiver != null && m_player.Settings != null && m_bossReceiver.CanReceiveStab)
        {
            Vector3 hitPoint = m_bossTarget != null ? m_bossTarget.position : m_player.transform.position;

            SpawnStabHitVfx();
            // VFX と同フレームでカメラへ着弾を通知＝この瞬間に着弾アップ＋スローを開始させる。
            m_player.FireStabFinisherImpact();

            m_bossReceiver.OnStabHit(new StabHitData
            {
                damage = m_player.Settings.stabDamage,
                hitPoint = hitPoint,
                source = m_player.gameObject,
            });
        }
        m_events.FireStab();
    }

    // VFX はボス位置ではなく「プレイヤー前方」に出す。スタブの体感を「自分が刺した感」に寄せるため。
    private void SpawnStabHitVfx()
    {
        var settings = m_player.Settings;
        var prefab = settings.stabHitVfx;
        if (prefab == null) return;

        Vector3 spawnPos = m_player.transform.position + m_player.transform.forward * settings.stabHitVfxForwardOffset;
        Quaternion spawnRot = Quaternion.LookRotation(m_player.transform.forward, Vector3.up);

        var fx = Object.Instantiate(prefab, spawnPos, spawnRot);
        if (settings.stabHitVfxScale > 0f && Mathf.Abs(settings.stabHitVfxScale - 1f) > 0.001f)
            fx.transform.localScale *= settings.stabHitVfxScale;

        Object.Destroy(fx, settings.stabHitVfxLifetime);
    }

    /// <summary>
    /// 最寄りのボスを解決する。複数ボスでもプレイヤーに最も近い IStabReceiver 個体を選ぶ。
    /// キャッシュしない（呼ぶたびに最寄りを取り直す）。タグ未登録時はログ1回のみで false。
    /// </summary>
    private bool TryResolveBoss()
    {
        GameObject[] bosses;
        try { bosses = GameObject.FindGameObjectsWithTag("Boss"); }
        catch (UnityException)
        {
            if (!m_warnedNoBossTag)
            {
                ChannelLogger.LogWarning("Stab", "Bossタグが未登録 (TagManager に Boss を追加)");
                m_warnedNoBossTag = true;
            }
            return false;
        }

        if (bosses == null || bosses.Length == 0) return false;

        Vector3 playerPos = m_player.transform.position;
        float bestSqr = float.MaxValue;
        GameObject best = null;
        IStabReceiver bestReceiver = null;
        foreach (var b in bosses)
        {
            var recv = b.GetComponent<IStabReceiver>();
            if (recv == null) continue;
            float sqr = (b.transform.position - playerPos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = b; bestReceiver = recv; }
        }

        if (best == null) return false;
        m_bossTarget = best.transform;
        m_bossReceiver = bestReceiver;
        return true;
    }
}
