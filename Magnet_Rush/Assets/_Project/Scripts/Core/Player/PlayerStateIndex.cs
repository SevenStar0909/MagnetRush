/// <summary>
/// Animator の State Int パラメータに渡す index の固定定義。
/// Inspector の states 配列順ではなくこの enum が単一の真実。
/// 新 State 追加時はこの enum に追加 + PlayerAnimator.s_stateTypeToIndex に登録。
/// </summary>
public enum PlayerStateIndex
{
    Idle       = 0,
    Move       = 1,
    Die        = 2,
    Aim        = 3,
    Fall       = 4,
    StabAttack = 5,
}