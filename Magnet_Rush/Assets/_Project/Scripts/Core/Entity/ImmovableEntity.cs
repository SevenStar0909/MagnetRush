using UnityEngine;

/// <summary>
/// このEntityは EntityController 経由で他Entityから押されないことを示すマーカー。
/// 磁力 (Magnetizable.ApplyForce -> Rigidbody.AddForceAtPosition) や物理衝突など他経路の力は通常通り作用する。
/// 例: ボスはPlayerに押されないが磁力では動く。
/// </summary>
public class ImmovableEntity : MonoBehaviour {}
