using UnityEngine;

/// <summary>
/// チュートリアルゾーンの当たり判定スクリプト。
/// 提示された GameClearTrigger の優秀なプレイヤー検知ロジックをそのまま移植。
/// </summary>
public class TutorialZoneTrigger : MonoBehaviour
{
    [Header("チュートリアルゾーン設定")]
    [Tooltip("何番目のゾーン（図）か (1〜5)")]
    [SerializeField] private int m_zoneNumber = 1;

    [Tooltip("プレイヤーオブジェクトに設定されているタグ名")]
    [SerializeField] private string m_playerTag = "Player";

    private SimpleTutorial m_tutorialManager;

    void Start()
    {
        // シーン全体からチュートリアル管理スクリプトを自動で探して接続
        m_tutorialManager = Object.FindFirstObjectByType<SimpleTutorial>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[コライダー接触検知] 触れてきたもの: {other.name} (タグ: {other.tag})");

        // 💡 磁力フィールドは完全に無視する（GameClearTriggerと同じ特別ルール）
        if (other.name.Contains("MagnetField"))
        {
            return; 
        }

        // 💡 触れてきた相手自身、またはその親（ルート）が Player タグを持っているかチェック（確定検知）
        bool isPlayer = other.CompareTag(m_playerTag) ||
                        (other.transform.root != null && other.transform.root.CompareTag(m_playerTag));

        if (isPlayer)
        {
            Debug.Log($"[TutorialZone] 確定検知！ゾーン【 {m_zoneNumber} 】にプレイヤーが侵入しました。");
            
            if (m_tutorialManager != null)
            {
                // チュートリアルマネージャーに「このゾーンに入ったよ」と通知する
                m_tutorialManager.OnPlayerEnterZone(m_zoneNumber);
            }
        }
    }
}