using UnityEngine;

/// <summary>
/// ボススタブ・フィニッシャー演出ステート。間合い詰め→跳び乗り→頭に突き刺し→離脱を
/// コード駆動の弧で実行する。演出中は Player.Update が UpdateEntity をスキップするため、
/// このステートが transform.position を直接動かす。崩れポーズ（Stagger/Stun）で
/// StabFinisherSettings のプロファイルを分岐する。
/// 基底: EntityState&lt;Player&gt;
/// </summary>
public class BossStabFinisherState : EntityState<Player>
{
    private StabFinisherSettings.Profile m_profile;
    private Transform m_anchor;
    private IStabReceiver m_receiver;
    private EnemyBossAI m_bossAi;

    private Vector3 m_startPos;
    private Vector3 m_standPos;
    private Vector3 m_hitPos;
    private bool m_hitDone;

    private float m_tApproachEnd;
    private float m_tLeapEnd;
    private float m_tPlungeEnd;
    private float m_tRetreatEnd;

    /// <summary>現在フェーズに対応する Animator の State Int。PlayerAnimator が拾って遷移させる（接近=Idle/跳躍=Fall/突き=StabAttack）。</summary>
    public int AnimatorPhaseIndex { get; private set; }

    /// <summary>このフェーズが空中扱いか（跳び上がり中）。PlayerAnimator が IsGrounded に反映する。</summary>
    public bool IsAirbornePhase { get; private set; }

    /// <summary>StabAbility から演出データを渡す。Change 前に呼ぶこと。</summary>
    public void Setup(StabFinisherSettings.Profile profile, IStabReceiver receiver)
    {
        m_profile = profile;
        m_receiver = receiver;
        m_bossAi = receiver as EnemyBossAI;
    }

    protected override void OnEnter(Player player)
    {
        m_hitDone = false;
        m_startPos = player.transform.position;
        AnimatorPhaseIndex = (int)PlayerStateIndex.Idle;
        IsAirbornePhase = false;
        player.lateralVelocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;

        if (m_profile == null || m_receiver == null || m_receiver.StabAnchor == null)
        {
            ChannelLogger.LogGuardReturn("Stab", "演出データ不足 — 即ヒットにフォールバック");
            DoHit(player);
            ReturnToNormal(player);
            return;
        }

        m_anchor = m_receiver.StabAnchor;
        m_hitPos = m_anchor.position;

        // ボス中心からの水平オフセットで「構える位置」を決める。ボス→プレイヤー方向を正面とする。
        Vector3 bossPos = ((MonoBehaviour)m_receiver).transform.position;
        Vector3 toPlayer = player.transform.position - bossPos;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -player.transform.forward;
        toPlayer.Normalize();
        Quaternion face = Quaternion.LookRotation(toPlayer, Vector3.up);
        m_standPos = bossPos + face * m_profile.approachStandOffset;
        m_standPos.y = m_startPos.y;

        m_tApproachEnd = m_profile.approachDuration;
        m_tLeapEnd = m_tApproachEnd + m_profile.leapDuration;
        m_tPlungeEnd = m_tLeapEnd + m_profile.plungeDuration;
        m_tRetreatEnd = m_tPlungeEnd + m_profile.retreatDuration;

        m_bossAi?.BeginStabFinisher();
        player.FireStabFinisherStart(m_anchor, m_receiver.StabChoreographyIndex);
    }

    protected override void OnStep(Player player, float dt)
    {
        if (m_profile == null) { ReturnToNormal(player); return; }

        float t = timeSinceEntered;

        if (t <= m_tApproachEnd)
        {
            // 接近: 起点→構え位置。地上で待機ポーズ。
            AnimatorPhaseIndex = (int)PlayerStateIndex.Idle;
            IsAirbornePhase = false;
            float k = Mathf.InverseLerp(0f, m_tApproachEnd, t);
            player.transform.position = Vector3.Lerp(m_startPos, m_standPos, Smooth(k));
            player.verticalVelocity = 0f;
            FaceTowards(player, m_hitPos);
        }
        else if (t <= m_tLeapEnd)
        {
            // 跳び乗り: 構え位置→頭へ放物線（上って下る）。終端で頭に着地。VerticalSpeed で Jump→Fall を出す。
            AnimatorPhaseIndex = (int)PlayerStateIndex.Fall;
            IsAirbornePhase = true;
            float k = Mathf.InverseLerp(m_tApproachEnd, m_tLeapEnd, t);
            Vector3 ground = Vector3.Lerp(m_standPos, m_hitPos, k);
            float arc = Mathf.Sin(k * Mathf.PI) * m_profile.arcApexHeight;
            player.transform.position = ground + Vector3.up * arc;
            player.verticalVelocity = Mathf.Cos(k * Mathf.PI) * m_profile.arcApexHeight * 2f; // 上昇(+)→下降(-)
            FaceTowards(player, m_hitPos);
        }
        else if (t <= m_tPlungeEnd)
        {
            // 突き刺し: 頭の位置でパイルを最後まで再生。ヒット/VFX はパイルクリップの AnimEvent が正しいフレームで発火する。
            AnimatorPhaseIndex = (int)PlayerStateIndex.StabAttack;
            IsAirbornePhase = false;
            player.transform.position = m_hitPos;
            player.verticalVelocity = 0f;
            FaceTowards(player, m_hitPos);
        }
        else if (t <= m_tRetreatEnd)
        {
            // 保険: AnimEvent でヒットが入っていなければ（ボスがまだ崩れたまま）1回だけ叩く。
            if (!m_hitDone)
            {
                m_hitDone = true;
                if (m_receiver != null && m_receiver.CanReceiveStab) DoHit(player);
            }
            // 離脱: 頭→起点へ戻る。
            AnimatorPhaseIndex = (int)PlayerStateIndex.Idle;
            IsAirbornePhase = false;
            float k = Mathf.InverseLerp(m_tPlungeEnd, m_tRetreatEnd, t);
            player.transform.position = Vector3.Lerp(m_hitPos, m_startPos, Smooth(k));
            player.verticalVelocity = 0f;
        }
        else
        {
            ReturnToNormal(player);
        }
    }

    protected override void OnExit(Player player)
    {
        player.transform.position = new Vector3(m_startPos.x, player.transform.position.y, m_startPos.z);
        player.velocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;
        m_bossAi?.EndStabFinisher();
        player.FireStabFinisherEnd();
    }

    // 既存のダメージ＋VFX＋FireStab(StabアニメTrigger)＋receiver.OnStabHit を再利用する。
    private void DoHit(Player player)
    {
        player.stab.OnStabHitEvent();
    }

    private void FaceTowards(Player player, Vector3 worldTarget)
    {
        Vector3 dir = worldTarget - player.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up)
            * Quaternion.Euler(0f, m_profile.plungeYawOffset, 0f);
        player.transform.rotation = look;
    }

    private static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));

    private void ReturnToNormal(Player player)
    {
        if (player.input.MoveInput.sqrMagnitude > 0.01f)
            player.states.Change<MovePlayerState>();
        else
            player.states.Change<IdlePlayerState>();
    }
}
