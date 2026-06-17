using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Playables;

public class SceneSelectUI : MonoBehaviour
{
    [SerializeField] private StageData m_stageData;

    [Header("Preview Settings")]
    [SerializeField] private Transform m_previewSpawnPoint;

    [Header("First Select Settings")]
    [SerializeField] private Button m_firstSelectedButton; // 最初に選択されるボタン

    private GameObject m_currentPreviewInstance;
    private string m_lastSelectedMapName = "";
    private Coroutine m_previewCoroutine;

    private void Start()
    {
        // タイトルから来たとき、先頭ステージ(チュートリアル)を初期選択としてプレビュー表示する。
        if (m_stageData == null || m_stageData.stages == null || m_stageData.stages.Length == 0)
            return;

        StageData.Stage first = m_stageData.stages[0];
        if (first != null && !string.IsNullOrEmpty(first.mapSceneName))
            OnSelectStage(first.mapSceneName);

        if (m_firstSelectedButton != null)
        {
            m_firstSelectedButton.Select();

            // 【重要】コントローラー用に選択状態にしたことをEventSystemにも通知する
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(m_firstSelectedButton.gameObject);

            Debug.Log($"[Debug] シーン開始時に '{m_firstSelectedButton.name}' を初期選択しました。");
        }
        else
        {
            Debug.LogWarning("[Debug] 警告: 最初に選択するボタン（m_firstSelectedButton）がインスペクターで設定されていません。");
        }
    }

    // --- 既存のコードはそのままで、クラスの中に以下のメソッドを追加・修正してください ---

    /// <summary>
    /// 【インスペクター用窓口A】マウスオーバー・コントローラー選択時に呼び出す
    /// </summary>
    public void OnSelectStage(string mapName)
    {
        // 内部で第2引数を false にして共通処理を呼び出す
        HandleStageSelection(mapName, false);
    }

    /// <summary>
    /// 【インスペクター用窓口B】ボタンクリック・決定キー押し時に呼び出す
    /// </summary>
    public void OnConfirmStage(string mapName)
    {
        // 内部で第2引数を true にして共通処理を呼び出す
        HandleStageSelection(mapName, true);
    }

    /// <summary>
    /// 【内部共通処理】（※インスペクターには表示されなくなりますが、上の2つから呼ばれます）
    /// </summary>
    private void HandleStageSelection(string mapName, bool isClick)
    {
        // 先ほど作成した中身（foreachなどのロジック）はそのままここにおいておきます
        if (m_stageData == null || m_stageData.stages == null) return;

        if (!isClick)
        {
            if (m_lastSelectedMapName == mapName) return;
            m_lastSelectedMapName = mapName;

            if (m_previewCoroutine != null) StopCoroutine(m_previewCoroutine);
            if (m_currentPreviewInstance != null) Destroy(m_currentPreviewInstance);

            foreach (var stage in m_stageData.stages)
            {
                if (stage != null && stage.mapSceneName == mapName)
                {
                    if (stage.previewPrefab != null && m_previewSpawnPoint != null)
                    {
                        m_previewCoroutine = StartCoroutine(SpawnAndPlayPreviewRoutine(stage.previewPrefab));
                    }
                    break;
                }
            }
        }
        else
        {
            if (m_lastSelectedMapName != mapName)
            {
                HandleStageSelection(mapName, false);
                return;
            }

            foreach (var stage in m_stageData.stages)
            {
                if (stage != null && stage.mapSceneName == mapName)
                {
                    SceneLoader.Instance.LoadGameWithMap(stage.mainSceneName, stage.mapSceneName);
                    break;
                }
            }
        }
    }

    /// <summary>共通の mapName から StageData 内の要素を検索するヘルパー関数</summary>
    private StageData.Stage GetStageDataByMapName(string mapName)
    {
        for (int i = 0; i < m_stageData.stages.Length; i++)
        {
            if (m_stageData.stages[i] != null && m_stageData.stages[i].mapSceneName == mapName)
            {
                return m_stageData.stages[i];
            }
        }
        return null;
    }

    /// <summary>1フレーム待ってからタイムラインとカメラ位置を完全に同期して再生するコルーチン</summary>
    private IEnumerator SpawnAndPlayPreviewRoutine(GameObject previewPrefab)
    {
        m_currentPreviewInstance = Instantiate(previewPrefab, m_previewSpawnPoint.position, m_previewSpawnPoint.rotation);

        // Unity内部の初期化・カメラ認識が終わるのを1フレーム待つ
        yield return null;

        if (m_currentPreviewInstance == null) yield break;

        if (m_currentPreviewInstance.TryGetComponent<PlayableDirector>(out var director))
        {
            // タイムラインを0秒時点に巻き戻して強制確定
            director.time = 0;
            director.Evaluate();

            // 生成されたプレハブ内の CinemachineCamera を探す
            var vcam = m_currentPreviewInstance.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                // 画面を実際に映しているメインカメラを一瞬でワープさせ、起動直後のカメラ迷子を完全に防ぐ
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.transform.position = vcam.transform.position;
                    mainCam.transform.rotation = vcam.transform.rotation;
                }
            }
            // 完璧な状態でタイムライン（カメラアニメーション）を再生
            director.Play();
        }
    }

    // 既存のボタン用ショートカット（そのまま維持）
    public void OnClickPlayButton() => SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StageSelectScene);
    public void OnClickBackButton() => SceneLoader.Instance.LoadScene(SceneLoader.SceneType.TitleScene);
    public void OnClickSelectButton() => SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StageSelectScene);
}