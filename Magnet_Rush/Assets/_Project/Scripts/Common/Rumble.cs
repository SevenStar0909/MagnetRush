using System;

/// <summary>
/// コントローラー振動の窓口（疎結合用ファサード）。
/// 実体の振動処理は RumbleManager(MagnetRush.Game) が起動時にフックを登録する。
/// これにより Game を参照しない層（Core / Bullet / Enemy）からも振動を鳴らせる。
/// 依存: なし（System.Action のみ）
/// </summary>
public static class Rumble
{
    /// <summary>1発振動させるフック。(重いモーター0〜1, 軽いモーター0〜1, 長さ秒)。RumbleManager が登録する。</summary>
    public static Action<float, float, float> OnPulse;

    /// <summary>振動を即停止するフック。</summary>
    public static Action OnStop;

    /// <summary>
    /// 1発振動させる。RumbleManager 未登録時（起動直後・テスト）やパッド未接続時は何もしない。
    /// </summary>
    /// <param name="lowFrequency">重いゴロゴロした振動（低周波モーター）の強さ 0〜1</param>
    /// <param name="highFrequency">軽いブルブルした振動（高周波モーター）の強さ 0〜1</param>
    /// <param name="duration">鳴らす長さ（秒・実時間）。0以下なら何もしない</param>
    public static void Pulse(float lowFrequency, float highFrequency, float duration)
        => OnPulse?.Invoke(lowFrequency, highFrequency, duration);

    /// <summary>振動を即停止する（死亡・シーン遷移時の鳴りっぱなし防止に使う）。</summary>
    public static void Stop() => OnStop?.Invoke();
}
