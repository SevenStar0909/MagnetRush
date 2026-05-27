using UnityEngine;

public class ConnectionPullPlayerState : EntityState<Player>
{
    private MagneticConnection m_connection;
    protected override void OnEnter(Player player)
    {
    }
    protected override void OnExit(Player player) { }

    protected override void OnStep(Player player, float dt)
    {
        Debug.Log("【超重要】ConnectionPlayerStateのOnStepが呼ばれました。");

        // 1. 仕様書通りの2行（エイムの更新と空中制御用の入力）
        player.UpdateAim();
        player.AccelerateToInputDirection(dt);

        // 現在アクティブな磁力の線を取得
        var connection = MagnetManager.Instance?.ActiveConnection;

        // 線が存在していて、かつ「引力発動中(IsActivated)」である場合
        if (connection != null && connection.IsActivated)
        {
            /*
            // ターゲット（壁や重い敵）への方向ベクトルを計算
            Vector3 targetPos = connection.TargetSide.transform.position;
            Vector3 direction = (targetPos - player.transform.position).normalized;

            // 設定SOの引っ張る力を取得
            float pullForce = connection.Settings.PullForce;

            if (player.TryGetComponent<Rigidbody>(out var rb))
            {
                // 通常移動のブレーキを踏み潰し、ターゲット方向への速度を直接上書きする！
                Debug.Log($"【プレイヤー物理適用】ターゲット({connection.TargetSide.name})へ向かって速度 {direction * pullForce} を直接与えています！");
                rb.linearVelocity = direction * pullForce;
            }
            */

            Vector3 targetPos = connection.TargetSide.transform.position;
            Vector3 direction = (targetPos - player.transform.position).normalized;

            float pullForce = connection.Settings.PullForce;
            player.transform.position += direction * pullForce * dt;

            float distance = Vector3.Distance(player.transform.position, targetPos);
            
            if (connection.TargetSide.TryGetComponent<Collider>(out var targetCollider))
            {
                Vector3 closestPoint = targetCollider.ClosestPoint(player.transform.position);

                distance = Vector3.Distance(player.transform.position, closestPoint);
            }

            if (distance < 5.0f)
            {
                Debug.Log("【ステート脱出】ターゲットに到着したため、通常状態に戻ります。");

                connection.Release();
                Debug.Log("【ステート脱出】磁力の線へ引力終了を通知しました。");

                player.states.Change<IdlePlayerState>();
                return;
            }
        }

        if (connection == null || !connection || !connection.IsActive || !connection.IsActivated)
        {
            Debug.Log("【ステート自動脱出】線が切れているのを検知したため、通常状態に戻ります。");
            player.states.Change<IdlePlayerState>();
            return;
        }
    }
}
