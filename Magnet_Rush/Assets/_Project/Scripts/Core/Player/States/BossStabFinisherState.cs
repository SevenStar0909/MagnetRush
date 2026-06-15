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
    private Collider m_footholdCol;
    private float m_footOffset;
    private bool m_hitDone;
    private Quaternion m_preTurnRot;
    private bool m_turnCaptured;

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
        m_turnCaptured = false;
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

        // ボス基準の水平な右/前ベクトル。瞬間移動先・踏み切り位置はこの座標系で置く。
        Transform bossT = ((MonoBehaviour)m_receiver).transform;
        Vector3 bossPos = bossT.position;
        Vector3 bossRight = bossT.right; bossRight.y = 0f;
        bossRight = bossRight.sqrMagnitude > 0.0001f ? bossRight.normalized : Vector3.right;
        Vector3 bossFwd = bossT.forward; bossFwd.y = 0f;
        bossFwd = bossFwd.sqrMagnitude > 0.0001f ? bossFwd.normalized : Vector3.forward;
        float groundY = m_startPos.y;

        // 瞬間移動先＝ボスの右斜め（走り出し位置）。
        Vector3 rs = m_profile.runStartOffset;
        m_startPos = bossPos + bossRight * rs.x + bossFwd * rs.z;
        m_startPos.y = groundY;

        // 走り寄って跳ぶ位置（踏み切り）。
        Vector3 jo = m_profile.jumpOffOffset;
        m_standPos = bossPos + bossRight * jo.x + bossFwd * jo.z;
        m_standPos.y = groundY;

        // 跳び乗り先（頭/足場）= StabFoothold レイヤーのコライダー。着地点は上面中央＋足オフセット、毎フレーム再計算。
        m_footholdCol = ResolveFootholdCollider();
        m_footOffset = PlayerFootToPivot(player);
        m_hitPos = FootholdTarget();

        // 右斜めへ瞬間移動 → 走り寄り → 跳び乗り → 突き。離脱は右斜めへ戻る。
        player.transform.position = m_startPos;
        FaceTowards(player, m_standPos);

        // 走りフェーズを最初のフレームから出す（State=Move＋走り速度をここでセット）。OnStep を待たないので開始直後から走り(歩き)が再生される。
        AnimatorPhaseIndex = (int)PlayerStateIndex.Move;
        player.lateralVelocity = (m_standPos - m_startPos) / Mathf.Max(0.01f, m_profile.approachDuration);

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

        // 足場（ボーン配下）が動いても追従するよう、毎フレーム着地先を更新する。
        if (m_footholdCol != null) m_hitPos = FootholdTarget();

        if (t <= m_tApproachEnd)
        {
            // 走り寄り: 右斜めの起点→踏み切り位置。Run アニメ。
            AnimatorPhaseIndex = (int)PlayerStateIndex.Move;
            IsAirbornePhase = false;
            float k = Mathf.InverseLerp(0f, m_tApproachEnd, t);
            player.transform.position = Vector3.Lerp(m_startPos, m_standPos, Smooth(k));
            // 走りアニメ用に水平速度を渡す（PlayerAnimator が MoveSpeed に反映）。transform は別駆動なので二重移動なし。
            player.lateralVelocity = (m_standPos - m_startPos) / Mathf.Max(0.01f, m_tApproachEnd);
            player.verticalVelocity = 0f;
            FaceTowards(player, m_standPos);
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
            // 突き開始時の向き(跳び乗り後＝頭向き)を1回だけ記録。ここから後ろ向きへ補間する。
            if (!m_turnCaptured) { m_preTurnRot = player.transform.rotation; m_turnCaptured = true; }
            // 突きの瞬間、足場の上から頭(StabAnchor)側へ寄せる。
            Vector3 headPos = m_anchor != null ? m_anchor.position : m_hitPos;
            player.transform.position = Vector3.Lerp(m_hitPos, headPos, m_profile.plungeHeadPull);
            player.verticalVelocity = 0f;
            // 最終の向き(後ろ向き＋頭側へ傾け)を作り、turnDuration かけてなめらかに振り向く（スナップ廃止）。
            FaceTowards(player, m_standPos);
            LeanToward(player, headPos, m_profile.plungeLeanDegrees);
            float turnK = Mathf.Clamp01((t - m_tLeapEnd) / Mathf.Max(0.01f, m_profile.turnDuration));
            player.transform.rotation = Quaternion.Slerp(m_preTurnRot, player.transform.rotation, Smooth(turnK));
        }
        else if (t <= m_tRetreatEnd)
        {
            // 保険: AnimEvent でヒットが入っていなければ（ボスがまだ崩れたまま）1回だけ叩く。
            if (!m_hitDone)
            {
                m_hitDone = true;
                if (m_receiver != null && m_receiver.CanReceiveStab) DoHit(player);
            }
            // 離脱: 立ち上がり(StandUp)アニメで頭→起点へ戻る。突きポーズのまま戻らないように。
            AnimatorPhaseIndex = (int)PlayerStateIndex.StandUp;
            IsAirbornePhase = false;
            float k = Mathf.InverseLerp(m_tPlungeEnd, m_tRetreatEnd, t);
            player.transform.position = Vector3.Lerp(m_hitPos, m_startPos, Smooth(k));
            player.verticalVelocity = 0f;
            // 突き時の傾き(LeanToward)を解除し、戻る方向(ボス前)を向いて起き上がる。
            FaceTowards(player, m_startPos);
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
        player.lateralVelocity = Vector3.zero;
        player.externalVelocity = Vector3.zero;
        // 突き時の傾き(LeanToward)を持ち越さない。水平(ヨーのみ)の起き上がった向きに戻す。
        Vector3 flatFwd = player.transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude > 0.0001f)
            player.transform.rotation = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
        m_bossAi?.EndStabFinisher();
        player.FireStabFinisherEnd();
    }

    // 既存のダメージ＋VFX＋FireStab(StabアニメTrigger)＋receiver.OnStabHit を再利用する。
    private void DoHit(Player player)
    {
        player.stab.OnStabHitEvent();
    }

    // StabFoothold レイヤーのコライダー（ボスの子・プレイヤーが乗る足場）を探して返す。無ければ null。
    private Collider ResolveFootholdCollider()
    {
        var bossMb = m_receiver as MonoBehaviour;
        if (bossMb == null) return null;
        int layer = LayerMask.NameToLayer("StabFoothold");
        if (layer < 0) return null;
        foreach (var col in bossMb.GetComponentsInChildren<Collider>(true))
        {
            if (col.gameObject.layer == layer) return col;
        }
        return null;
    }

    // 跳び乗り先のワールド座標。足場コライダーの上面中央＋足→ピボット分（プレイヤーが上に立つ）。無ければ頭(StabAnchor)。
    private Vector3 FootholdTarget()
    {
        if (m_footholdCol == null) return m_anchor != null ? m_anchor.position : Vector3.zero;
        Bounds b = m_footholdCol.bounds;
        return new Vector3(b.center.x, b.max.y + m_footOffset, b.center.z);
    }

    // プレイヤーのピボットから足元までの距離（ピボットを足場上面に置くための上オフセット）。
    private static float PlayerFootToPivot(Player player)
    {
        var cap = player.GetComponentInChildren<CapsuleCollider>(true);
        if (cap == null) return 1f;
        float scaleY = Mathf.Abs(cap.transform.lossyScale.y);
        return Mathf.Max(0f, (cap.height * 0.5f - cap.center.y) * scaleY);
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

    // 上体を target 方向へ傾ける近似（FaceTowards 後に呼ぶ）。全身が中心まわりに pitch する。
    // target が前(forward)側なら前傾、背面側なら後傾で、その方向へ倒す。
    private void LeanToward(Player player, Vector3 target, float degrees)
    {
        if (Mathf.Abs(degrees) < 0.01f) return;
        Vector3 to = target - player.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        float sign = Vector3.Dot(player.transform.forward, to.normalized) >= 0f ? 1f : -1f;
        player.transform.rotation *= Quaternion.Euler(sign * Mathf.Abs(degrees), 0f, 0f);
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
