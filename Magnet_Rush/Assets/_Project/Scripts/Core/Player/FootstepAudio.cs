using UnityEngine;

// static を削除し、MonoBehaviour を継承する
public class FootstepAudio : MonoBehaviour
{
    // static だった配列も、必要に応じて変えるか、static のまま保持します
    private static readonly string[] FootstepCues =
    {
        SoundData.SE.PlayerFoot01,
        SoundData.SE.PlayerFoot02,
        SoundData.SE.PlayerFoot03,
        SoundData.SE.PlayerFoot04
    };

    // static メソッドのままにしてもアタッチは可能ですが、
    // アニメーションイベントから呼ぶ場合は以下のようにします
    public void TriggerFootstep()
    {
        int randomIndex = Random.Range(0, FootstepCues.Length);
        string selectedCue = FootstepCues[randomIndex];

        var handle = SoundManager.Instance.PlayWithHandle(SoundData.CueSheet.SE, selectedCue);
        float randomPitch = Random.Range(0.95f, 1.05f);
        handle.SetVolumeAndPitch(0.1f, randomPitch);
    }
}