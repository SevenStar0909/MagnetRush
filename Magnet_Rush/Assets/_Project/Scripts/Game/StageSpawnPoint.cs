using UnityEngine;

/// <summary>
/// マップシーンに配置するスポーン地点。OnEnableでGameManagerに自己登録する。
/// </summary>
public class StageSpawnPoint : MonoBehaviour
{
    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterSpawnPoint(transform);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterSpawnPoint(null);
    }
}
