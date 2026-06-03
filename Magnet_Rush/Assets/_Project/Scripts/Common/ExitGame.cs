using UnityEngine;

/// <summary>
/// ゲームを終了するためのクラス
/// </summary>
public class ExitGame : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("ゲームを終了します");

#if UNITY_EDITOR
        // エディタ上ではプレイモードを停止する
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // ビルドされたゲームではアプリケーションを終了する
        Application.Quit();
#endif
    }
}