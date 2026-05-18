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

    private MagneticConnectionSettings m_settings;
    private float m_remaining; //生成時から連続、待機中も進行
    private bool m_prevActivated; //変化検出用

    public event Action<bool> OnActivatedChanged;

    public void Initialize(Magnetizable p, Magnetizable t, MagneticConnectionSettings s)
    {
        PlayerSide = p;
        TargetSide = t;
        m_settings = s;

        m_remaining= m_settings.Duration;   //5秒からカウントダウン開始
        IsActive = true;
        m_prevActivated = false;

        EvaluateActivation();
    }

    private void FixedUpdate()
    {
        EvaluateActivation();
        if (IsActivated != m_prevActivated)
        {
           OnActivatedChanged?.Invoke(IsActivated);  //State切替
        }
        m_prevActivated = IsActivated;
        m_remaining -= Time.fixedDeltaTime; //(e)待機中も進行

        //(c)距離/(d)Linecast/(e)ゼロ　→ Release
        if (CheckTerminationConditions())
        {
            Release();
            return; // 線が切れたらこれ以降の引力処理は行わない
        }

        if (IsActivated)
        {
            ApplyAttraction();
        }
    }

    private void EvaluateActivation()
    {
        var p = PlayerSide.Pole;
        var t = TargetSide.Pole;

        IsActivated = (p != MagneticPole.None && t != MagneticPole.None && p != t);
    }

    public void Release()
    {
        IsActive = false;
        IsActivated = false;
    }

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

    private void PullObject(Magnetizable target, Vector3 destination)
    {
        // targetのRigidbody等に対し、destination方向への力（m_settings.PullForce）を毎フレーム加える

    }

    // 終了条件のチェック (c, d, e)
    private bool CheckTerminationConditions()
    {
        // (e) 時間経過による寿命（5秒）
        if (m_remaining <= 0f) return true;

        // (c) 距離が maxDistance (15m) を超えたか
        float currentDistance = Vector3.Distance(PlayerSide.transform.position, TargetSide.transform.position);
        if (currentDistance > m_settings.MaxDistance) return true;

        // (d) 壁や地面（Wall / Ground）による遮蔽判定
        // Physics.Linecast を使用して、2点間に遮蔽レイヤーのコライダーがあるか検出
        if (Physics.Linecast(PlayerSide.transform.position, TargetSide.transform.position, m_settings.OccluderMask))
        {
            return true;
        }

        return false;
    }
}