using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;   

public class SimpleTutorial : MonoBehaviour
{
    public TextMeshProUGUI tutorialText;    // 画面の文字
    public GameObject gateWall_1; // 通せんぼしている壁
    public GameObject checkPoint;   // プレイヤーが近づくと反応する目印（黄色いエリア）
    public GameObject goalPoint;    // ゴールの目印（黄色いエリア）
    public Transform playerTransform; // プレイヤーのTransform
    public Transform boxTransform;    // 箱のTransform
    public Transform spawnArea;       // プレイヤーの初期位置（ワープ先）
    [Header("反応する距離を設定")]
    public float detectDistance = 3.0f; // 反応する距離（3メートル）

    private int currentStep = 0;

    [Header("文字表示のスピードを設定(秒)")]
    public float textSpeed = 0.05f; // 文字が表示されるスピード（0.05秒に1文字）
    public void ShowTextTypewriter(string message)
    {
        // もしすでに前の文字がタイピング中なら、一旦止める（バグ防止）
        StopAllCoroutines();

        // タイピング演出をスタート！
        StartCoroutine(TypeTextRoutine(message));
    }

    private IEnumerator TypeTextRoutine(string message)
    {
        // 1. まず全体の文章をセットする（この時点ではまだ画面には表示されないようにする）
        tutorialText.text = message;
        tutorialText.maxVisibleCharacters = 0; // 表示文字数をゼロにする

        // 2. 文字数を数えて、1文字ずつ表示を増やしていくループ
        //（TMPがタグを自動で無視してくれるので、message.LengthのままでOK！）
        for (int i = 0; i <= message.Length; i++)
        {
            tutorialText.maxVisibleCharacters = i; // 表示する文字数を1つずつ増やす

            // 3. 指定した秒数（0.05秒）だけ待ってから、次の文字へ進む
            yield return new WaitForSeconds(textSpeed);
        }
    }

    void Start()
    {
        if (tutorialText != null)
        {
            //tutorialText.text = "【チュートリアル】\nBボタンで自分に磁極を付与し\n磁極を逆にしてRT長押しで箱に撃て！";
            ShowTextTypewriter("Bボタンで自分に磁極を付与し\n磁極を逆にしてRT長押しで箱に撃て！");
        }
        if (gateWall_1 != null) gateWall_1.SetActive(true);
        if (goalPoint != null) 
        {
            goalPoint.transform.localScale = new Vector3(detectDistance * 2.0f, 1.0f, detectDistance * 2.0f);
            goalPoint.SetActive(true);
        }
        if (checkPoint != null) 
        {
            checkPoint.transform.localScale = new Vector3(detectDistance * 2.0f, 1.0f, detectDistance * 2.0f);
            checkPoint.SetActive(true);
        }
    }

    private void OnValidate()
    {
        // ゲームを実行していなくても、エディタ上でリアルタイムに大きさが変わる！
        if (goalPoint != null)
        {
            goalPoint.transform.localScale = new Vector3(detectDistance * 2.0f, 1.0f, detectDistance * 2.0f);
        }
        if (checkPoint != null)
        {
            checkPoint.transform.localScale = new Vector3(detectDistance * 2.0f, 1.0f, detectDistance * 2.0f);
        }
    }

    void Update()
    {
        // 【ステップ0】最初の磁力接続を待つ
        if (currentStep == 0)
        {
            if (MagnetManager.Instance != null &&
                MagnetManager.Instance.ActiveConnection != null &&
                MagnetManager.Instance.ActiveConnection.IsActivated)
            {
                currentStep = 1;

                if (gateWall_1 != null) gateWall_1.SetActive(false);

                if (tutorialText != null)
                {
                    //tutorialText.text = "【チュートリアル】\n磁力が繋がりゲートが開いた！\n先へ進もう。";
                    ShowTextTypewriter("磁力が繋がりゲートが開いた！\n先へ進もう");
                }
            }
        }
        // 【ステップ1】プレイヤーが最初の目印（targetArea）に近づくのを待つ
        else if (currentStep == 1)
        {
            if (playerTransform != null && checkPoint != null)
            {
                float distance = Vector3.Distance(playerTransform.position, checkPoint.transform.position);

                if (distance < detectDistance)
                {
                    currentStep = 2;
                    if (checkPoint != null) checkPoint.SetActive(false);

                    if (tutorialText != null)
                    {
                        //tutorialText.text = "【チュートリアル】\n固定されている物は\n逆に自身が引っ張られる。\n壁の上に見える物体に対して\n先ほどと同じようにやってみよう！";
                        ShowTextTypewriter("固定されている物は\n逆に自身が引っ張られる\n壁の上に見える物体に対して\n先ほどと同じようにやってみよう！");
                    }
                }
            }
        }
        // 【ステップ2】プレイヤーがゴール（goalArea）に近づいたら、説明してリセット（ワープ）
        else if (currentStep == 2)
        {
            if (playerTransform != null && goalPoint != null)
            {
                float goalDistance = Vector3.Distance(playerTransform.position, goalPoint.transform.position);

                if (goalDistance < detectDistance)
                {
                    currentStep = 3; // ステップ3（おさらいフェーズ）へ

                    if (tutorialText != null)
                    {
                        //tutorialText.text = "【チュートリアル】\nそれでは同じことをしながら\n今度は箱も一緒にゴールまで持っていこう！\n最初はおさらい";
                        ShowTextTypewriter("それでは同じことをしながら\n今度は箱も一緒にゴールまで持っていこう！\n最初は");
                    }

                    // プレイヤーをスタート地点にワープさせる！
                    if (spawnArea != null)
                    {
                        playerTransform.position = spawnArea.position;
                    }

                    // ステージの見た目をリセット
                    //if (gateWall_1 != null) gateWall_1.SetActive(true);
                    if (goalPoint != null) goalPoint.SetActive(true);
                    if (checkPoint != null) checkPoint.SetActive(true);

                    // 箱も初期位置に戻す
                    if (boxTransform != null)
                    {
                        boxTransform.position = new Vector3(30f, 0.75f, -2f);
                    }
                }
            }
        }
        //【ステップ3】ワープ後、もう一度おさらいの磁力接続を待つ
        else if (currentStep == 3)
        {
            if (MagnetManager.Instance != null &&
                MagnetManager.Instance.ActiveConnection != null &&
                MagnetManager.Instance.ActiveConnection.IsActivated)
            {
                currentStep = 4; // ステップ4へ

                if (gateWall_1 != null) gateWall_1.SetActive(false); // 繋がったら開く

                if (tutorialText != null)
                {
                    //tutorialText.text = "【チュートリアル】\n次に箱を黄色いエリアまで運ぼう！";
                    ShowTextTypewriter("次に箱を黄色いエリアまで運ぼう！");
                }
            }
        }
        // 【ステップ4】箱がtargetArea（黄色いエリアなど）に近づくのを待つ
        else if (currentStep == 4)
        {
            if (boxTransform != null && checkPoint != null)
            {
                float distance = Vector3.Distance(boxTransform.position, checkPoint.transform.position);

                if (distance < detectDistance)
                {
                    currentStep = 5; // ステップ5へ

                    if (checkPoint != null) checkPoint.SetActive(false);

                    if (tutorialText != null)
                    {
                        //tutorialText.text = "【チュートリアル】\n(注意)磁力の線は\n間に壁とかの障害物があると切れてしまう";
                        ShowTextTypewriter("(注意)磁力の線は\n間に壁とかの障害物があると切れてしまう");
                    }
                }
            }
        }
        // 【ステップ5】「箱」がゴール（goalArea）に近づくのを待つ
        else if (currentStep == 5)
        {
            if (boxTransform != null && goalPoint != null)
            {
                float boxDistance = Vector3.Distance(boxTransform.position, goalPoint.transform.position);

                if (boxDistance < detectDistance)
                {
                    currentStep = 6; // チュートリアル完了

                    if (goalPoint != null) goalPoint.SetActive(false);

                    if (tutorialText != null)
                    {
                        //tutorialText.text = "【チュートリアル】\nこれでチュートリアルは終了です";
                        ShowTextTypewriter("これでチュートリアルは終了です");
                    }
                }
            }
        }
    }
}