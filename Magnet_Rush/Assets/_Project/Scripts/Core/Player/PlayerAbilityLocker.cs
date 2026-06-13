using System;
using UnityEngine;

/// <summary>
/// プレイヤー機能の個別ロック状態を保持する。チュートリアルで「まだ教えていない機能」を封じる用。
/// 実際のゲート処理は Player の Facade ラッパー（Fire() / Jump() 等）が IsLocked() を見て行う。
/// 既定は全機能アンロック（通常ステージは何も変わらない）。
/// 依存: なし（Player 側から参照される）
/// </summary>
public class PlayerAbilityLocker : MonoBehaviour
{
    // enum の int 値をインデックスにしたロックフラグ。既定 false = アンロック
    private bool[] m_locked;

    /// <summary>ロック状態変化時に発火。チュートリアルUI等が購読する。</summary>
    public event Action<PlayerAbilityType, bool> OnLockChanged;

    void Awake()
    {
        m_locked = new bool[Enum.GetValues(typeof(PlayerAbilityType)).Length];
    }

    /// <summary>指定機能がロック中かどうか。</summary>
    public bool IsLocked(PlayerAbilityType ability)
    {
        // Awake 前（他コンポーネントの初期化順）に呼ばれても安全に「アンロック」を返す
        if (m_locked == null) return false;
        return m_locked[(int)ability];
    }

    /// <summary>指定機能のロック状態を設定する。状態が変わったときだけ通知する。</summary>
    public void SetLocked(PlayerAbilityType ability, bool locked)
    {
        if (m_locked == null)
            m_locked = new bool[Enum.GetValues(typeof(PlayerAbilityType)).Length];

        int index = (int)ability;
        if (m_locked[index] == locked) return;

        m_locked[index] = locked;
        ChannelLogger.Log("Player", $"機能ロック変更: {ability} = {(locked ? "ロック" : "解除")}");
        OnLockChanged?.Invoke(ability, locked);
    }

    /// <summary>
    /// 全機能をロックする。移動とカメラは含めない（既存の「全ロック」ゾーンの挙動を変えないため）。
    /// 移動・カメラを封じたい場合は SetLocked(Move/Camera, true) で個別に指定する。
    /// </summary>
    public void LockAll()
    {
        foreach (PlayerAbilityType ability in Enum.GetValues(typeof(PlayerAbilityType)))
        {
            if (ability == PlayerAbilityType.Move || ability == PlayerAbilityType.Camera) continue;
            SetLocked(ability, true);
        }
    }

    /// <summary>全機能のロックを解除する。</summary>
    public void UnlockAll()
    {
        foreach (PlayerAbilityType ability in Enum.GetValues(typeof(PlayerAbilityType)))
            SetLocked(ability, false);
    }
}
