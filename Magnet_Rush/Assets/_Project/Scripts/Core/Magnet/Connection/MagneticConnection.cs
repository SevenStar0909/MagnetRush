using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Scripts/Core/Magnet/Connection/MagneticConnection.cs
public class MagneticConnection : MonoBehaviour
{
    public Magnetizable PlayerSide {get; private set; }
    public Magnetizable TargetSide { get; private set; }
    public bool IsActive { get; private set; }  //線あり
    public bool IsActivated { get; private set; }   //発動中
    //＊IsPlayerMovingは撤廃（1.6で動作中/静止中の区別を廃止）

    private MagneticConnectionVisualizer m_visualizer;

    private MagneticConnectionSettings m_settings;
    public MagneticConnectionSettings Settings => m_settings;
    private float m_remaining; //生成時から連続、待機中も進行
    private bool m_prevActivated; //変化検出用

    public event Action<bool> OnActivatedChanged;

    private void Awake()
    {
        m_visualizer = GetComponent<MagneticConnectionVisualizer>();
    }

    public void Initialize(Magnetizable p, Magnetizable t, MagneticConnectionSettings s)
    {
        if (p == null) Debug.LogError("【デバッグ】第1引数（player）が空っぽ（null）です！");
        if (t == null) Debug.LogError("【デバッグ】第2引数（target）が空っぽ（null）です！");
        if (s == null) Debug.LogError("【デバッグ】第3引数（settings）が空っぽ（null）です！");

        PlayerSide = p;
        TargetSide = t;
        m_settings = s;

        m_remaining　= m_settings.Duration;   //5秒からカウントダウン開始

        IsActive = true;
        m_prevActivated = false;

        EvaluateActivation();

        if (m_visualizer != null)
        {
            m_visualizer.Show(this, m_settings);
        }
    }

    private void FixedUpdate()
    {

        EvaluateActivation();
        if (IsActivated != m_prevActivated)
        {
            Debug.Log($"【状態変化】IsActivated が {m_prevActivated} から {IsActivated} に変わりました！" +
                      $"(Player: {PlayerSide.Pole} / Target: {TargetSide.Pole})");
            OnActivatedChanged?.Invoke(IsActivated);
        }
        m_prevActivated = IsActivated;
        m_remaining -= Time.fixedDeltaTime; //(e)待機中も進行

        //(c)距離/(d)Linecast/(e)ゼロ　→ Release
        if (CheckTerminationConditions())
        {
            Debug.Log("【線切断】終了条件（寿命・距離・遮蔽のいずれか）を満たしたため切断します。");
            Release();
            return; // 線が切れたらこれ以降の引力処理は行わない
        }

        if (!IsActive || !IsActivated) return;

        if (IsActivated)
        {
            Debug.Log($"【引力動作中】IsActivatedはTrueです！プレイヤー質量: {PlayerSide.VirtualMass} / 対象質量: {TargetSide.VirtualMass}");
            ApplyAttraction();
        }
    }

    private void EvaluateActivation()
    {
        var p = PlayerSide.Pole;
        var t = TargetSide.Pole;

        bool case1 = (p != MagneticPole.S && t != MagneticPole.N && p != t);
        bool case2 = (p != MagneticPole.N && t != MagneticPole.S && p != t);

        // ①か②が成立していれば基本は発動（true）
        bool logicResult = (case1 || case2);

        // もし「両方ともNone」の時は、先輩のロジックだとtrueをすり抜けてしまうバグがあったため、
        // 両方Noneの時だけは確実にfalse（待機状態）にします！
        if (p == MagneticPole.None && t == MagneticPole.None)
        {
            IsActivated = false;
            return;
        }

        IsActivated = logicResult;
    }

    public void Release()
    {
        PlayerSide.SetPole(MagneticPole.None);
        TargetSide.SetPole(MagneticPole.None);

        IsActive = false;
        IsActivated = false;
        this.enabled = false;

        if (m_visualizer != null)
        {
            m_visualizer.Hide();
        }

        Destroy(gameObject);
    }

    /*
    // 引力の適用ロジック
    private void ApplyAttraction()
    {
        // 1.4 仮想質量の比較（仕様：数値が小さい方が軽い）
        int pMass = (int)PlayerSide.VirtualMass;
        int tMass = (int)TargetSide.VirtualMass;

        // 同質量のマッピング（例：Heavy同士）の場合は、プレイヤーを優先して動かす
        if (pMass == tMass)
        {
            // プレイヤーをターゲット側へ動かす処理
            PullObject(PlayerSide, TargetSide.transform.position);
        }
        else if (pMass < tMass)
        {
            // プレイヤーの方が軽い（壁やボス、中型敵に対してなど）→ プレイヤーが飛ぶ
            PullObject(PlayerSide, TargetSide.transform.position);
        }
        else
        {
            // 対象の方が軽い（小型敵や箱など）→ 敵やオブジェクトを引き寄せる
            PullObject(TargetSide, PlayerSide.transform.position);
        }
    }
    */

    private void ApplyAttraction()
    {
        int pMass = (PlayerSide.Pole == MagneticPole.None) ? 3 : (int)PlayerSide.VirtualMass;
        //int pMass = (int)PlayerSide.VirtualMass;
        int tMass = (int)TargetSide.VirtualMass;

        Debug.Log($"【引力発動中！】IsActivatedはTrueです。現在、物理の力を加えています！");
        Debug.Log($"【引力計算中】PlayerMass: {PlayerSide.VirtualMass}({(int)pMass}) vs TargetMass: {TargetSide.VirtualMass}({(int)tMass})");

        if (pMass <= tMass)
        {
            // プレイヤー側が動く条件（同質量、またはプレイヤーの方が軽い）
            // ➔ プレイヤーの移動は「ConnectionPullPlayerState」側が面倒を見るので、
            //    ここでは何もしない（またはプレイヤーに通知するだけにする
            Debug.Log("➔ 【判定】プレイヤー側が動く条件です。ConnectionPullPlayerStateが動いているか確認してください。");
        }
        else
        {
            // 対象（敵や箱）の方が軽い場合
            // ➔ 敵や箱のRigidbodyをここで引っ張る
            Debug.Log($"➔ 【判定】対象の方が軽いため、オブジェクト({TargetSide.name})を引っ張ります！");
            PullObject(TargetSide, PlayerSide.transform.position);
        }
    }

    private void PullObject(Magnetizable target, Vector3 destination)
    {
        // 動かす対象がプレイヤー自身なら、ステート側に任せるのでスルー
        if (target == PlayerSide) return;

        Vector3 direction = (destination - target.transform.position).normalized;
        float distance = Vector3.Distance(target.transform.position, destination);

        if (target.TryGetComponent<Collider>(out var targetCollider))
        {
            // 目的地（プレイヤー位置など）から、オブジェクトの表面で一番近い点を割り出す
            Vector3 closestPoint = targetCollider.ClosestPoint(destination);
            distance = Vector3.Distance(destination, closestPoint);
        }

        if (distance < 2.0f)
        {
            Debug.Log($"【引力終了】{target.name} が目的地に到着したため、磁力の線をReleaseします。");

            // 線を消滅させて、これ以上 FixedUpdate の引力計算が走らないようにする
            Release();
            return;
        }

        if (target.TryGetComponent<Rigidbody>(out var rb))
        {
            // 敵やオブジェクトを引き寄せる物理処理
            if (rb.linearVelocity.magnitude > m_settings.PullForce)
            {
                // 既に最高速度に達しているので、AddForceをスキップして現在の速度を維持する
                rb.linearVelocity = rb.linearVelocity.normalized * m_settings.PullForce;
                return;
            }
            Debug.Log($"【物理適用】{target.name} の Rigidbody に力({direction * m_settings.PullForce})を加えます！(ForceMode: VelocityChange)");
            rb.AddForce(direction * m_settings.PullForce, ForceMode.VelocityChange);
        }
        else
        {
            Debug.LogError($"【物理エラー】{target.name} に Rigidbody が見つからないため、引っ張れません！");
        }
    }

    // 終了条件のチェック (c, d, e)
    //private bool CheckTerminationConditions()
    //{
    //    // (e) 時間経過による寿命（5秒）
    //    if (m_remaining <= 0f) return true;

    //    // (c) 距離が maxDistance (15m) を超えたか
    //    float currentDistance = Vector3.Distance(PlayerSide.transform.position, TargetSide.transform.position);
    //    if (currentDistance > m_settings.MaxDistance) return true;

    //    // (d) 壁や地面（Wall / Ground）による遮蔽判定
    //    // Physics.Linecast を使用して、2点間に遮蔽レイヤーのコライダーがあるか検出
    //    if (Physics.Linecast(PlayerSide.transform.position, TargetSide.transform.position, m_settings.OccluderMask))
    //    {
    //        return true;
    //    }

    //    return false;
    //}
    private bool CheckTerminationConditions()
    {
        // 1. 5秒の寿命チェック
        if (m_remaining <= 0f)
        {
            Debug.Log($"【切断原因】5秒の寿命が尽きました。(残り時間: {m_remaining})");
            return true;
        }

        // 2. 15mの距離チェック
        float currentDistance = Vector3.Distance(PlayerSide.transform.position, TargetSide.transform.position);
        if (currentDistance > m_settings.MaxDistance)
        {
            Debug.Log($"【切断原因】距離が離れすぎました！ (現在距離: {currentDistance}m / 最大制限: {m_settings.MaxDistance}m)");
            return true;
        }

        // 3. 壁や地面による遮蔽チェック
        if (Physics.Linecast(PlayerSide.transform.position, TargetSide.transform.position, m_settings.OccluderMask))
        {
            Debug.Log($"【切断原因】プレイヤーと対象の間に、壁や地面(OccluderMask)が挟まりました！" +
                      $" (PlayerPos: {PlayerSide.transform.position} / TargetPos: {TargetSide.transform.position})");
            return true;
        }

        return false;
    }
}