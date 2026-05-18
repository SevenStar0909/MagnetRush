using UnityEngine;

// Scripts/Core/Magnet/MagneticVirtualMass.cs
public enum MagneticVirtualMass
{
    Light = 1,       // 武器(落下中) / 雑魚エネミー(小型)
    Medium = 2,      // 箱(PhysicsObject) / 雑魚エネミー(中型)
    Heavy = 3,       // プレイヤー / 武器(敵装備中)
    SuperHeavy = 4,  // ボス
    Immovable = 5,   // 壁 / 地面 / 天井 / 地面固定タレット
                     // (接続成立しても動かない予約値)
}