using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class SimpleTutorial : MonoBehaviour
{
    public TextMeshProUGUI tutorialText;
    public float textSpeed = 0.05f;
    public float messageWaitTime = 2.0f; // 文字が出終わったあと、次の文字に進むまでの待ち時間（2秒）

    public Transform playerTransform;     // 👈 プレイヤーのTransform

    public float approachDistance = 7.5f; // 👈 何メートルまで近づいたら合格にするか（初期値は7.5マス分）

    // 監視用のクレート3つをインスペクターで登録しておく
    public GameObject[] tutorialCrates;

    public GameObject tutorialEnemy;

    // ==== 🎥 カメラズーム用の変数を追加 ====
    public Camera mainCamera;       // シーン上のメインカメラを登録する用
    public float zoomFOV = 30f;     // ズームした時の視野角（小さいほどドアップになります）
    private float defaultFOV;       // 元のカメラの視野角を保存しておく変数
    private bool isZooming = false; // 今ズーム中かどうかのフラグ

    private int currentStep = 0;

    /// <summary>
    /// 外部のゾーンコライダーから、プレイヤーが触れた瞬間に呼び出される関数
    /// </summary>
    public void OnPlayerEnterZone(int zoneNumber)
    {
        // 【仕様書 ③】図1（ゾーン1）への移動待ち
        if (zoneNumber == 1 && currentStep == 3) currentStep = 4;

        // 【仕様書 ⑪】図2（ゾーン2）への移動待ち
        if (zoneNumber == 2 && currentStep == 11) currentStep = 12;

        // 【仕様書 ⑭】図3（ゾーン3）への移動待ち
        if (zoneNumber == 3 && currentStep == 14) currentStep = 15;

        // 【仕様書 ⑰】図4（ゾーン4）への移動待ち
        if (zoneNumber == 4 && currentStep == 17) currentStep = 18;

        // 【仕様書 ⑱】図5（ゾーン5）への移動待ち
        if (zoneNumber == 5 && currentStep == 18) currentStep = 19;
    }

    void Start()
    {
        //ステージ開始：すべての操作をロック（窓口）
        SetPlayerLockAll(true);

        // チュートリアルのレールの開始！
        StartCoroutine(TutorialSequenceRoutine());

        if (mainCamera != null)
        {
            defaultFOV = mainCamera.fieldOfView;
        }
    }

    void Update()
    {
        //３つのクレートのどれか一つに磁力が付与されたか判定
        OnCrateMagnetApplied();

        //磁力切り替え（RB）が実行されたか判定
        OnMagnetSwitched();

        //磁力の引き寄せ（反発）が起こったかを判定
        OnMagnetConnect();

        //プレイヤー自身に磁力が付与されたか判定
        OnMagnetAppliedToSelf();

        //カメラの自動ズーム演出処理
        CameraZoom();

        //敵（ロボット）の死亡判定
        OneEnemyDead();
    }

    // チュートリアルの進行管理（一本道のレール）
    private IEnumerator TutorialSequenceRoutine()
    {
        // --- メッセージ 1〜4 を自動で連続表示 ---
        currentStep = 2;
        yield return StartCoroutine(TypeAndAutoAdvance("<color=yellow>通信確認</color>\n担当オペレーターの<color=#FF4500>A.R.I.A</color>です\n以後よろしくね"));        //1
        yield return StartCoroutine(TypeAndAutoAdvance("ここは私たち警察の\n訓練プログラムシミュレータ内の仮想空間"));     //2
        yield return StartCoroutine(TypeAndAutoAdvance("ここで実戦での動き方や\nあなたのメイン装備についての理解を深めてもらうわ"));   //3
        yield return StartCoroutine(TypeAndAutoAdvance("早速はじめましょう\nまずは左スティックと右スティックを動かして\n周囲を警戒しつつ道なりに進んで\n障害物はAボタンのジャンプで\n飛び越えるといいわ"));    //4

        // --- 4つのメッセージが終わったら、移動・カメラ・ジャンプを解除 ---
        currentStep = 3;
        //tutorialText.text = "道なりに進む。"; // 次の目標を画面に残しておく
        SetPlayerMovementLock(false); // 移動系のロックを解除

        //判定待ちプレイヤーが図1のエリアに入るまで、ここでプログラムを一時停止して待つ
        while (currentStep == 3)
        {
            yield return null;
        }

        currentStep = 4;
        // --- 図1付近に到達したら、メッセージ 5〜8 を自動表示 ---
        yield return StartCoroutine(TypeAndAutoAdvance("OK、基本的な移動は問題ないようね"));                                                                   //5
        yield return StartCoroutine(TypeAndAutoAdvance("ではこれからあなたのメイン装備について説明するわ\nこれからの任務の生命線よ\nよく聞いておいてね"));   //6
        yield return StartCoroutine(TypeAndAutoAdvance("あなたの右腕の銃は\n磁力の弾丸を打ち出して\n命中したところから磁場を発生させる機能を備えているの"));   //7
        yield return StartCoroutine(TypeAndAutoAdvance("試しにLTボタンを長押ししながら\n右スティックで近くのクレートに狙いをつけて\nRTボタンを押してみて"));         //8

        currentStep = 5;
        //tutorialText.text = "クレートに射撃";
        SetPlayerShootingLock(false); // 射撃のロックを解除

        // ここでクレートに磁極が付くのを待つ（currentStep を 5 にしてから待機する想定）
        while (currentStep == 5)
        {
            yield return null;
        }

        currentStep = 6;
        SetPlayerShootingLock(true); // 射撃ロック
        yield return StartCoroutine(TypeAndAutoAdvance("弾が発射されて\nクレートからドームが展開されているでしょう？\nこれは磁場が可視化されたものよ"));                      //9
        yield return StartCoroutine(TypeAndAutoAdvance("ところで磁力にはNとSの２つの磁極があるのは知ってるわね\n同じ磁極同士なら反発し\n異なる磁極同士なら引き寄せあう…"));  //10
        yield return StartCoroutine(TypeAndAutoAdvance("実はあなたの銃はこのSとNの磁極を切り替える機能も備わっているの\nRBボタンを押してみて"));  //11

        currentStep = 7;
        //tutorialText.text = "RBボタンで磁力を切り替えろ";
        SetPlayerSwitchMagnetLock(false); // 磁力切り替え解除の窓口

        while (currentStep == 7)
        {
            yield return null;
        }

        currentStep = 8;
        SetPlayerSwitchMagnetLock(true); // 切り替えロック
        yield return StartCoroutine(TypeAndAutoAdvance("右下のインタフェースが変わったのに気付いた？\nこれであなたの弾で付与できる磁極を\n切り替えられるのよ")); //12
        yield return StartCoroutine(TypeAndAutoAdvance("じゃあそのままさっきとは別のクレートに向けて\n銃を撃ってみましょう")); //13

        currentStep = 9;
        SetPlayerShootingLock(false); // 射撃ロック

        while (currentStep == 9)
        {
            yield return null;
        }

        currentStep = 10;
        SetPlayerSwitchMagnetLock(true); // 切り替えロック
        SetPlayerShootingLock(true); // 射撃ロック
        yield return StartCoroutine(TypeAndAutoAdvance("異なる磁極を付与したことでオブジェクト同士が引き合ったわね\n成功よ"));        //14
        yield return StartCoroutine(TypeAndAutoAdvance("逆に反発を起こしたいときは\n同じ磁極を付与すればOKよ\nこの機能を活用して\n目の前の障害物を突破して進んでちょうだい"));        //15
        yield return StartCoroutine(TypeAndAutoAdvance("そうそう\n右下のインタフェースは\nあなたの銃の残弾数も示しているの"));        //16
        yield return StartCoroutine(TypeAndAutoAdvance("リロードしたくなったら\n<color=yellow>Xボタン</color>を押すのよ\nただリロードすると\n今の磁力がリセットされる点には注意して"));        //17

        currentStep = 11;
        SetPlayerSwitchMagnetLock(false); // 切り替えロック
        SetPlayerShootingLock(false); // 射撃ロック
        SetPlayerSwitchMagnetLock(false); // 磁力切り替え

        while (currentStep == 11)
        {
            yield return null;
        }

        currentStep = 12;
        SetPlayerSwitchMagnetLock(true); // 切り替えロック
        SetPlayerShootingLock(true); // 射撃ロック
        yield return StartCoroutine(TypeAndAutoAdvance("高い壁ね..\nでも問題ないわ\nあなたの銃の機能を応用すればね"));        //18
        yield return StartCoroutine(TypeAndAutoAdvance("実はあなたの銃には\nあなた自身にもＮかＳの磁力を\n付与する機能があるの\nLBを押してみて"));        //19


        currentStep = 13;
        SetPlayerSelfMagnetLock(false);

        while (currentStep == 13)
        {
            yield return null;
        }

        currentStep = 14;
        yield return StartCoroutine(TypeAndAutoAdvance("見えるかしら？\nあなたの体からドームが展開されているでしょ？"));        //20
        yield return StartCoroutine(TypeAndAutoAdvance("この間はあなた自身が\n周りの磁力を与えた物体の動きへ\n影響を与えるようになるわ"));        //21
        yield return StartCoroutine(TypeAndAutoAdvance("それだけじゃなく\nあなたが空中に居る間はあなた自身も\n磁力の影響を受けるようになるの\nそれを活かして\nこの壁を超えてみせて"));        //22
        SetPlayerLockAll(false);

        currentStep = 14;
        while (currentStep == 14)
        {
            yield return null;
        }

        currentStep = 15;
        SetPlayerLockAll(true);
        isZooming = true;

        yield return StartCoroutine(TypeAndAutoAdvance("やるわね\nもうかなり磁力を使いこなしているんじゃない？"));        //23
        yield return StartCoroutine(TypeAndAutoAdvance("でも本番はここからよ\n目の前の不気味なロボットが見える？"));        //24
        yield return StartCoroutine(TypeAndAutoAdvance("これが私たちの街の安全を脅かす敵よ\nあなたの任務におけるターゲットってところね"));        //25
        yield return StartCoroutine(TypeAndAutoAdvance("次はこの敵を相手に\n戦闘のシミュレーションをしてみましょう"));        //26
        yield return StartCoroutine(TypeAndAutoAdvance("この敵は\n巨大な斧を振り回してあなたを攻撃してくるわ\nでも動きは遅いから\n回避は簡単なはずよ"));        //27

        isZooming = false;

        currentStep = 16;
        SetPlayerLockAll(false);
        yield return StartCoroutine(TypeAndAutoAdvance("そしてあなたの銃はこの敵にも\n磁力を付与することができるの"));        //28
        yield return StartCoroutine(TypeAndAutoAdvance("さっきと同じように\nこいつにオブジェクトを引き寄せて\n思いっきりぶつけてやりなさい！"));        //29

        while (currentStep == 16)
        {
            yield return null;
        }

        currentStep = 17;
        yield return StartCoroutine(TypeAndAutoAdvance("流石ね\nシミュレーションとはいえ見事な手際だわ\nこの調子で進んでいきましょう"));        //30

        while (currentStep == 17)
        {
            yield return null;
        }

        currentStep = 18;
        yield return StartCoroutine(TypeAndAutoAdvance("気をつけて\nタレットが設置されているわ"));        //31
        yield return StartCoroutine(TypeAndAutoAdvance("あのタレットはなかなかの脅威ね\nでもあなたの磁力は何にでも付けることができる\nタレットだって例外じゃないわ\nそれで狙いを誘導できるはずよ"));        //32

        while (currentStep == 18)
        {
            yield return null;
        }

        currentStep = 19;
        yield return StartCoroutine(TypeAndAutoAdvance("よくやったわ\n今回のシミュレーションはここまでよ"));        //33
        yield return StartCoroutine(TypeAndAutoAdvance("これからあなたには実際の任務についてもらうわ\n今回のシミュレーションの内容を忘れずに\n頑張ってね"));        //34
    }

    // ✍️ 1文字ずつ表示し、終わったら自動で数秒待つ機能
    private IEnumerator TypeAndAutoAdvance(string message)
    {
        tutorialText.text = message;
        tutorialText.maxVisibleCharacters = 0;

        // タイピング演出
        for (int i = 0; i <= message.Length; i++)
        {
            tutorialText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(textSpeed);
        }

        // ボタン入力は待たずに、設定した秒数（2秒）だけ自動で待機して次に進む
        yield return new WaitForSeconds(messageWaitTime);
    }

    // 外部から呼ばれる「磁力が付与されたよ」の通知
    private void OnCrateMagnetApplied()
    {
        if (currentStep == 5 && tutorialCrates != null)
        {
            foreach (GameObject crate in tutorialCrates)
            {
                if (crate == null) continue;

                // 相方のスクリプト「Magnetizable」を取得
                Magnetizable magnet = crate.GetComponent<Magnetizable>();

                // Magnetizableの「IsActive（磁力がONか）」を直接見に行く！
                if (magnet != null && magnet.IsActive)
                {
                    currentStep = 6; // 磁力がついたので次のステップへ！
                    break;
                }
            }
        }
    }

    // 外部から呼ばれる「磁力を切り替えたよ」の通知
    private void OnMagnetSwitched()
    {
        if (currentStep == 7)
        {
            bool rbPressed = Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame; // コントローラーのRBボタン
            bool ePressed = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;       // キーボードのEキー

            if (rbPressed || ePressed)
            {
                currentStep = 8;
            }
        }
    }

    //クレート同士がつながったよ
    private void OnMagnetConnect()
    {
        if(currentStep == 9 && tutorialCrates != null)
    {
            foreach (GameObject crate in tutorialCrates)
            {
                if (crate == null) continue;

                Magnetizable magnet = crate.GetComponent<Magnetizable>();

                // 💡「ForceThisFrame」は、磁力で引っ張られたり弾かれたりしている力（合計）の変数です。
                // 力が0より大きくなった＝引き寄せ（反発）が発生したと判定できます！
                if (magnet != null && magnet.ForceThisFrame > 0f)
                {
                    currentStep = 10;
                    break;
                }
            }
        }
    }

    // 外部から呼ばれる「自分に磁力を付与したよ」の通知
    private void OnMagnetAppliedToSelf()
    {
        if (currentStep == 13 && playerTransform != null)
        {
            // プレイヤー自身に付いている「Magnetizable」を取得
            Magnetizable playerMagnet = playerTransform.GetComponent<Magnetizable>();

            // プレイヤー自身の磁力がONになったかをチェック！
            if (playerMagnet != null && playerMagnet.IsActive)
            {
                currentStep = 14;
            }
        }
    }

    private void CameraZoom()
    {
        // ==========================================
        // 🎥 カメラの自動ズーム演出処理
        // ==========================================
        if (mainCamera != null)
        {
            if (isZooming && tutorialEnemy != null)
            {
                // 1. 敵の方を滑らかに向く
                Vector3 targetDir = tutorialEnemy.transform.position - mainCamera.transform.position;
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetRot, Time.deltaTime * 3f);

                // 2. 滑らかにズームインする
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, zoomFOV, Time.deltaTime * 3f);
            }
            else
            {
                // ズームOFFの時は、滑らかに元の視野角に戻す
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, defaultFOV, Time.deltaTime * 5f);
            }
        }
    }

    private void OneEnemyDead()
    {
        if (currentStep == 16)
        {
            // 💡 Unityのルール：オブジェクトが倒されて消滅（Destroy）するか、
            // もしくは非アクティブ（SetActive(false)）になると、自動でここを通過します！
            if (tutorialEnemy == null || !tutorialEnemy.activeInHierarchy)
            {
                currentStep = 17; // ➔ 自動的にステップ17（流石ね、シミュレーションとはいえ…）に進む
            }
        }
    }

    //窓口
    private void SetPlayerLockAll(bool isLock)
    {
        Debug.Log($"全操作ロック: {isLock}");
    }
    private void SetPlayerMovementLock(bool isLock)
    {
        Debug.Log($"移動ロック: {isLock}");
    }
    private void SetPlayerShootingLock(bool isLock)
    {
        Debug.Log($"射撃ロック: {isLock}");
    }
    private void SetPlayerSwitchMagnetLock(bool isLock)
    {
        Debug.Log($"磁力切り替えロック: {isLock}");
    }
    private void SetPlayerSelfMagnetLock(bool isLock)
    {
        Debug.Log($"自分への磁力ロック: {isLock}");
    }
}
