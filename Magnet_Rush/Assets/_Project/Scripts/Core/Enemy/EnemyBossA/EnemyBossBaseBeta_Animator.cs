using UnityEngine;

public class EnemyBossBaseBeta_Animator : EnemyBossBaseA_Animator
{
    private static readonly int s_hRushKeepState = Animator.StringToHash("RushKeepAnim");
    private static readonly int s_hMissileReadyState = Animator.StringToHash("MissileReadyAnim");
    private static readonly int s_hMissileCycleState = Animator.StringToHash("MissileCycleAnim");
    private static readonly int s_hMissileEndState = Animator.StringToHash("MissileEndAnim");

    public override bool IsInRush
    {
        get
        {
            int hash = CurrentStateHash;
            return hash == s_hRushKeepState;
        }
    }

    public override bool IsInMissile
    {
        get
        {
            int hash = CurrentStateHash;
            return hash == s_hMissileReadyState || hash == s_hMissileCycleState || hash == s_hMissileEndState;
        }
    }
}
