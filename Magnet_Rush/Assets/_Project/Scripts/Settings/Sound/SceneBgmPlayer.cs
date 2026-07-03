using UnityEngine;
using System.Collections;

public class SceneBgmPlayer : MonoBehaviour
{
    public enum BgmType
    {
        None,
        BossBattle,
        Title,
        StageSelect,
        Tutorial,
        Stage01,
    }

    [SerializeField] private BgmType m_bgmType = BgmType.None;
    [SerializeField] private bool m_stopBgmIfNone = false;

    private IEnumerator Start()
    {
        // SoundManager.Start() Ç™èIÇÌÇÈÇÃÇë“Ç¬
        yield return null;

        string cueName = GetCueName(m_bgmType);

        if (string.IsNullOrEmpty(cueName))
        {
            if (m_stopBgmIfNone)
            {
                Sound.StopBgm();
            }

            yield break;
        }

        Sound.PlayBgm(cueName);
    }

    private string GetCueName(BgmType bgmType)
    {
        /*
                public const string BossBattle = "BossBattle";
    public const string Title = "Title";
    public const string StageSelect = "StageSelect";
    public const string Tutorial = "Tutorial";
    public const string Stage01 = "Stage01";
        */
        switch (bgmType)
        {
            case BgmType.BossBattle:
                return SoundData.BGM.BossBattle;
            case BgmType.Title:
                return SoundData.BGM.Title;
            case BgmType.StageSelect:
                return SoundData.BGM.StageSelect;
            case BgmType.Tutorial:
                return SoundData.BGM.Tutorial;
            case BgmType.Stage01:
                return SoundData.BGM.Stage01;

            case BgmType.None:
            default:
                return "";
        }
    }
}