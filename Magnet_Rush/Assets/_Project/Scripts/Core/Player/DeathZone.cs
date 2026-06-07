using UnityEngine;

/// <summary>
/// デスボックス。マップの奈落（地形の底）に薄く広く配置するトリガー領域。
/// 入ったプレイヤーを落下扱いにし、最後に接地していた位置へソフトリスポーンさせる（HPは減らさない）。
///
/// 当たり判定原則準拠: コールバック内で CompareTag / layer 判定はしない（原則4）。
/// GetComponentInParent&lt;Player&gt; で到達し、「何が入れるか」は Layer Matrix 側で制御する。
/// 依存: Collider(同 GameObject, isTrigger を強制), Player.FallRespawn
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeathZone : MonoBehaviour
{
    void Reset()
    {
        // アタッチ直後にトリガー化（デザイナーが手で設定し忘れても安全側に倒す）
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            player.FallRespawn();
        }
    }
}
