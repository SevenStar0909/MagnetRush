using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary> シーンセレクト画面のUIイベントを制御するクラス </summary>
public class SceneSelectUI : MonoBehaviour
{
    [SerializeField] private StageData m_stageData;

    /// <summary>ステージ名で直接ロード</summary>
    public void OnClickStageButtonByMapName(string mapName)
    {
        ChannelLogger.LogGuardReturn("UI", $"ステージ選択: {mapName}");

        for (int i = 0; i < m_stageData.stages.Length; i++)
        {
            if (m_stageData.stages[i].mapSceneName == mapName)
            {
                var stage = m_stageData.stages[i];
                SceneLoader.Instance.LoadGameWithMap(stage.mainSceneName, stage.mapSceneName);
                return;
            }
        }

        ChannelLogger.LogGuardReturn("UI", $"エラー: ステージ '{mapName}' は見つかりません");
    }

    /// <summary>セレクト画面に遷移</summary>
    public void OnClickPlayButton()
    {
        ChannelLogger.LogGuardReturn("UI", "セレクト画面に遷移");
        SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StageSelectScene);
    }

    public void OnClickBackButton()
    {
        ChannelLogger.LogGuardReturn("UI", "タイトルに遷移");
        SceneLoader.Instance.LoadScene(SceneLoader.SceneType.TitleScene);
    }

    public void OnClickSelectButton()
    {
        SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StageSelectScene);
    }
}