using UnityEngine;

/// <summary>
/// チュートリアル用のプレイヤー機能ロック制御。シーンに置くだけで開始時に指定機能を封じる。
/// Inspector のチェックで「最初にロックする機能」を選び、教えるタイミングで UnlockJump() 等を呼んで1つずつ解除する。
/// 解除はチュートリアル進行スクリプトや UnityEvent（ボタン・トリガー）から直接呼べる。
/// このオブジェクトが無効化・破棄されると全ロックを自動解除する（ロックの掛けっぱなし防止）。
/// 依存: Player（Player.OnPlayerReady で遅延解決）, PlayerAbilityLocker
/// </summary>
public class TutorialAbilityLocker : MonoBehaviour
{
    [Header("開始時にロックする機能（チェック = 使えなくする）")]
    [Tooltip("エイム（LT長押しでゆっくり狙う）をロックする")]
    [SerializeField] private bool m_lockAim = true;

    [Tooltip("通常射撃（RTで磁力弾を撃つ）をロックする")]
    [SerializeField] private bool m_lockFire = true;

    [Tooltip("セルフファイア（自分に磁極を付ける）をロックする")]
    [SerializeField] private bool m_lockSelfFire = true;

    [Tooltip("リロード（撃った弾を全部消す）をロックする")]
    [SerializeField] private bool m_lockReload = true;

    [Tooltip("磁極切替（SとNを入れ替える）をロックする")]
    [SerializeField] private bool m_lockPoleSwitch = true;

    [Tooltip("ジャンプをロックする")]
    [SerializeField] private bool m_lockJump = true;

    [Tooltip("スタブ攻撃（崩れたボスへのとどめ）をロックする")]
    [SerializeField] private bool m_lockStab = true;

    [Tooltip("移動（左スティックで歩く）をロックする。説明を読ませる間など完全に立ち止まらせたいとき用")]
    [SerializeField] private bool m_lockMove = false;

    [Tooltip("カメラ（右スティック/マウスで見回す）をロックする。視点を固定したいとき用")]
    [SerializeField] private bool m_lockCamera = false;

    private PlayerAbilityLocker m_locker;

    void OnEnable()
    {
        // Player より先に起動した場合に備えて、生成済みなら即適用・未生成ならイベント待ち
        Player.OnPlayerReady += OnPlayerReady;
        if (Player.Current != null) OnPlayerReady(Player.Current);
    }

    void OnDisable()
    {
        Player.OnPlayerReady -= OnPlayerReady;

        // チュートリアル終了（シーン遷移・オブジェクト破棄）でロックを残さない
        if (m_locker != null) m_locker.UnlockAll();
    }

    private void OnPlayerReady(Player player)
    {
        m_locker = player.GetComponent<PlayerAbilityLocker>();
        if (m_locker == null)
        {
            ChannelLogger.LogGuardReturn("Player", "PlayerAbilityLocker が Player に付いていない — チュートリアルロック無効");
            return;
        }
        ApplyInitialLocks();
    }

    /// <summary>Inspector で指定した開始時ロックを適用する。再適用（おさらいフェーズ等）にも使える。</summary>
    public void ApplyInitialLocks()
    {
        if (m_locker == null)
        {
            ChannelLogger.LogGuardReturn("Player", "PlayerAbilityLocker 未解決 — ロック適用をスキップ");
            return;
        }
        m_locker.SetLocked(PlayerAbilityType.Aim, m_lockAim);
        m_locker.SetLocked(PlayerAbilityType.Fire, m_lockFire);
        m_locker.SetLocked(PlayerAbilityType.SelfFire, m_lockSelfFire);
        m_locker.SetLocked(PlayerAbilityType.Reload, m_lockReload);
        m_locker.SetLocked(PlayerAbilityType.PoleSwitch, m_lockPoleSwitch);
        m_locker.SetLocked(PlayerAbilityType.Jump, m_lockJump);
        m_locker.SetLocked(PlayerAbilityType.Stab, m_lockStab);
        m_locker.SetLocked(PlayerAbilityType.Move, m_lockMove);
        m_locker.SetLocked(PlayerAbilityType.Camera, m_lockCamera);
    }

    /// <summary>任意の機能のロック状態を変更する。enum を渡せる呼び出し元はこちらを使う。</summary>
    public void SetLock(PlayerAbilityType ability, bool locked)
    {
        if (m_locker == null)
        {
            ChannelLogger.LogGuardReturn("Player", "PlayerAbilityLocker 未解決 — ロック変更をスキップ");
            return;
        }
        m_locker.SetLocked(ability, locked);
    }

    // UnityEvent（引数なし）から直接バインドできる解除/ロックメソッド群

    /// <summary>エイムを解除する。</summary>
    public void UnlockAim() => SetLock(PlayerAbilityType.Aim, false);

    /// <summary>通常射撃を解除する。</summary>
    public void UnlockFire() => SetLock(PlayerAbilityType.Fire, false);

    /// <summary>セルフファイアを解除する。</summary>
    public void UnlockSelfFire() => SetLock(PlayerAbilityType.SelfFire, false);

    /// <summary>リロードを解除する。</summary>
    public void UnlockReload() => SetLock(PlayerAbilityType.Reload, false);

    /// <summary>磁極切替を解除する。</summary>
    public void UnlockPoleSwitch() => SetLock(PlayerAbilityType.PoleSwitch, false);

    /// <summary>ジャンプを解除する。</summary>
    public void UnlockJump() => SetLock(PlayerAbilityType.Jump, false);

    /// <summary>スタブ攻撃を解除する。</summary>
    public void UnlockStab() => SetLock(PlayerAbilityType.Stab, false);

    /// <summary>移動を解除する。</summary>
    public void UnlockMove() => SetLock(PlayerAbilityType.Move, false);

    /// <summary>カメラ操作を解除する。</summary>
    public void UnlockCamera() => SetLock(PlayerAbilityType.Camera, false);

    /// <summary>全機能を解除する（チュートリアル完了時用）。</summary>
    public void UnlockAll()
    {
        if (m_locker == null) return;
        m_locker.UnlockAll();
    }

    /// <summary>エイムをロックする。</summary>
    public void LockAim() => SetLock(PlayerAbilityType.Aim, true);

    /// <summary>通常射撃をロックする。</summary>
    public void LockFire() => SetLock(PlayerAbilityType.Fire, true);

    /// <summary>セルフファイアをロックする。</summary>
    public void LockSelfFire() => SetLock(PlayerAbilityType.SelfFire, true);

    /// <summary>リロードをロックする。</summary>
    public void LockReload() => SetLock(PlayerAbilityType.Reload, true);

    /// <summary>磁極切替をロックする。</summary>
    public void LockPoleSwitch() => SetLock(PlayerAbilityType.PoleSwitch, true);

    /// <summary>ジャンプをロックする。</summary>
    public void LockJump() => SetLock(PlayerAbilityType.Jump, true);

    /// <summary>スタブ攻撃をロックする。</summary>
    public void LockStab() => SetLock(PlayerAbilityType.Stab, true);

    /// <summary>移動をロックする。</summary>
    public void LockMove() => SetLock(PlayerAbilityType.Move, true);

    /// <summary>カメラ操作をロックする。</summary>
    public void LockCamera() => SetLock(PlayerAbilityType.Camera, true);
}
