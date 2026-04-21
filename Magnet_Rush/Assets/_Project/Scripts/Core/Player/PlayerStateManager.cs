using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤー用のステートマシン。Inspector の states 配列から動的にステートを生成する。
/// 新 State を追加したい場合は Scripts/Core/Player/States/ に新クラスを置いて Inspector で選ぶ。
/// </summary>
public class PlayerStateManager : EntityStateManager<Player>
{
    [ClassTypeName(typeof(EntityState<Player>))]
    [Tooltip("使用するステートの一覧。Inspector のドロップダウンで選択。登録順が State Int index になる")]
    public string[] states;

    protected override List<EntityState<Player>> GetStateList()
    {
        return CreateListFromStringArray(states);
    }
}
