using UnityEngine;
using UnityEngine.UI;

public class SimpleTutorial : MonoBehaviour
{
    public Text tutorialText;    // 画面の文字
    public GameObject gateWall_1; // 通せんぼしている壁
    public GameObject checkPoint;
    public GameObject goalPoint;
    public Transform playerTransform; // プレイヤーのTransform
    public Transform boxTransform;    // 箱のTransform
    public Transform targetArea;      // ①最初にプレイヤーが行く場所
    public Transform goalArea;        // ②次にプレイヤー（と箱）が行く場所
    public Transform spawnArea;       // プレイヤーの初期位置（ワープ先）
    public float detectDistance = 3.0f; // 反応する距離（3メートル）

    private int currentStep = 0;

    // 箱の Magnetizable をキャッシュ。コネクション機能の代替判定に使う。
    private Magnetizable m_boxMagnetizable;

    void Start()
    {
        if (tutorialText != null)
        {
            tutorialText.text = "【チュートリアル】\nBボタンで自分に磁極を付与し\n磁極を逆にしてRT長押しで箱に撃て！";
        }
        if (gateWall_1 != null) gateWall_1.SetActive(true);
        if (goalPoint != null) goalPoint.SetActive(true);
        if (checkPoint != null) checkPoint.SetActive(true);
    }

    // 箱が磁化されている（プレイヤーが磁力を当てた）かを返す。
    // コネクション機能は develop に無いので、その「繋がった」判定をこれで代用する。
    private bool IsBoxMagnetized()
    {
        if (m_boxMagnetizable == null && boxTransform != null)
            m_boxMagnetizable = boxTransform.GetComponent<Magnetizable>();
        return m_boxMagnetizable != null && m_boxMagnetizable.IsActive;
    }

    void Update()
    {
        // 【ステップ0】最初の磁力接続を待つ
        if (currentStep == 0)
        {
            if (IsBoxMagnetized())
            {
                currentStep = 1;

                if (gateWall_1 != null) gateWall_1.SetActive(false);

                if (tutorialText != null)
                {
                    tutorialText.text = "【チュートリアル】\n磁力が繋がり、ゲートが開いた！\n先へ進もう。";
                }
            }
        }
        // 【ステップ1】プレイヤーが最初の目印（targetArea）に近づくのを待つ
        else if (currentStep == 1)
        {
            if (playerTransform != null && targetArea != null)
            {
                float distance = Vector3.Distance(playerTransform.position, targetArea.position);

                if (distance < detectDistance)
                {
                    currentStep = 2;
                    if (checkPoint != null) checkPoint.SetActive(false);

                    if (tutorialText != null)
                    {
                        tutorialText.text = "【チュートリアル】\n固定されている物は、逆に自身が引っ張られる。\n壁の上に見える物体に対して\n先ほどと同じようにやってみよう！";
                    }
                }
            }
        }
        // 【ステップ2】プレイヤーがゴール（goalArea）に近づいたら、説明してリセット（ワープ）
        else if (currentStep == 2)
        {
            if (playerTransform != null && goalArea != null)
            {
                float goalDistance = Vector3.Distance(playerTransform.position, goalArea.position);

                if (goalDistance < detectDistance)
                {
                    currentStep = 3; // ステップ3（おさらいフェーズ）へ

                    if (tutorialText != null)
                    {
                        tutorialText.text = "【チュートリアル】\nそれでは同じことをしながら、今度は箱も一緒にゴールまで持っていこう！\n最初はおさらい";
                    }

                    // プレイヤーをスタート地点にワープさせる！
                    if (spawnArea != null)
                    {
                        playerTransform.position = spawnArea.position;
                    }

                    // ステージの見た目をリセット
                    if (gateWall_1 != null) gateWall_1.SetActive(true);
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
            if (IsBoxMagnetized())
            {
                currentStep = 4; // ステップ4へ

                if (gateWall_1 != null) gateWall_1.SetActive(false); // 繋がったら開く

                if (tutorialText != null)
                {
                    tutorialText.text = "【チュートリアル】\n次に箱を黄色いエリアまで運ぼう！";
                }
            }
        }
        // 【ステップ4】箱がtargetArea（黄色いエリアなど）に近づくのを待つ
        else if (currentStep == 4)
        {
            if (boxTransform != null && targetArea != null)
            {
                float distance = Vector3.Distance(boxTransform.position, targetArea.position);

                if (distance < detectDistance)
                {
                    currentStep = 5; // ステップ5へ

                    if (checkPoint != null) checkPoint.SetActive(false);

                    if (tutorialText != null)
                    {
                        tutorialText.text = "【チュートリアル】\n(TIPS)磁力の線は、間に壁とかの障害物があると切れてしまう";
                    }
                }
            }
        }
        // 【ステップ5】「箱」がゴール（goalArea）に近づくのを待つ
        else if (currentStep == 5)
        {
            if (boxTransform != null && goalArea != null)
            {
                float boxDistance = Vector3.Distance(boxTransform.position, goalArea.position);

                if (boxDistance < detectDistance)
                {
                    currentStep = 6; // チュートリアル完了

                    if (goalPoint != null) goalPoint.SetActive(false);

                    if (tutorialText != null)
                    {
                        tutorialText.text = "【チュートリアル】\nこれでチュートリアルは終了です";
                    }
                }
            }
        }
    }
}