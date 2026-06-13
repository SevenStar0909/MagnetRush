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
        yield return StartCoroutine(TypeAndAutoAdvance("通信確認。\n担当オペレーターのA.R.I.Aです。\n以後よろしくね。"));        //1
        yield return StartCoroutine(TypeAndAutoAdvance("ここは私たち警察の、訓練プログラムシミュレータ内の仮想空間。"));     //2
        yield return StartCoroutine(TypeAndAutoAdvance("ここで実戦での動き方や、あなたのメイン装備についての理解を深めてもらうわ。"));   //3
        yield return StartCoroutine(TypeAndAutoAdvance("早速はじめましょう。まずは左スティックと右スティックを動かして、周囲を警戒しつつ道なりに進んで。障害物はAボタンのジャンプで飛び越えるといいわ。"));    //4

        // --- 4つのメッセージが終わったら、移動・カメラ・ジャンプを解除 ---
        currentStep = 3;
        tutorialText.text = "道なりに進む。"; // 次の目標を画面に残しておく
        SetPlayerMovementLock(false); // 移動系のロックを解除

        //判定待ちプレイヤーが図1のエリアに入るまで、ここでプログラムを一時停止して待つ
        while (currentStep == 3)
        {
            yield return null;
        }

        currentStep = 4;
        // --- 図1付近に到達したら、メッセージ 5〜8 を自動表示 ---
        yield return StartCoroutine(TypeAndAutoAdvance("OK、基本的な移動は問題ないようね。"));                                                                   //5
        yield return StartCoroutine(TypeAndAutoAdvance("ではこれからあなたのメイン装備について説明するわ。\nこれからの任務の生命線よ、よく聞いておいてね。"));   //6
        yield return StartCoroutine(TypeAndAutoAdvance("あなたの右腕の銃は、磁力の弾丸を打ち出して、命中したところから磁場を発生させる機能を備えているの。"));   //7
        yield return StartCoroutine(TypeAndAutoAdvance("試しにLTボタンを長押ししながら右スティックで近くのクレートに狙いをつけて、RTボタンを押してみて。"));         //8

        currentStep = 5;
        tutorialText.text = "クレートに射撃";
        SetPlayerShootingLock(false); // 射撃のロックを解除

        // ここでクレートに磁極が付くのを待つ（currentStep を 5 にしてから待機する想定）
        while (currentStep == 5)
        {
            yield return null;
        }

        currentStep = 6;
        // 一度解除した射撃はそのまま使えるようにする（教えるための再ロックはしない）
        yield return StartCoroutine(TypeAndAutoAdvance("弾が発射されて、クレートからドームが展開されているでしょう？これは磁場が可視化されたものよ。"));                      //9
        yield return StartCoroutine(TypeAndAutoAdvance("ところで磁力にはNとSの２つの磁極があるのは知ってるわね。同じ磁極同士なら反発し、異なる磁極同士なら引き寄せあう…"));  //10
        yield return StartCoroutine(TypeAndAutoAdvance("実はあなたの銃はこのSとNの磁極を切り替える機能も備わっているの。RBボタンを押してみて。"));  //11

        currentStep = 7;
        tutorialText.text = "RBボタンで磁力を切り替えろ";
        SetPlayerSwitchMagnetLock(false); // 磁力切り替え解除の窓口

        while (currentStep == 7)
        {
            yield return null;
        }

        currentStep = 8;
        // 一度解除した磁極切替はそのまま使えるようにする（教えるための再ロックはしない）
        yield return StartCoroutine(TypeAndAutoAdvance("右下のインタフェースが変わったのに気付いた？これであなたの弾で付与できる磁極を切り替えられるのよ。")); //12
        yield return StartCoroutine(TypeAndAutoAdvance("じゃあそのままさっきとは別のクレートに向けて、銃を撃ってみましょう。")); //13

        currentStep = 9;
        SetPlayerShootingLock(false); // 射撃ロック

        while (currentStep == 9)
        {
            yield return null;
        }

        currentStep = 10;
        // 射撃・磁極切替は解除済みのまま（再ロックしない）
        yield return StartCoroutine(TypeAndAutoAdvance("異なる磁極を付与したことでオブジェクト同士が引き合ったわね、成功よ。"));        //14
        yield return StartCoroutine(TypeAndAutoAdvance("逆に反発を起こしたいときは、同じ磁極を付与すればOKよ。この機能を活用して、目の前の障害物を突破して進んでちょうだい。"));        //15
        yield return StartCoroutine(TypeAndAutoAdvance("そうそう、右下のインタフェースはあなたの銃の残弾数も示しているの。"));        //16
        yield return StartCoroutine(TypeAndAutoAdvance("リロードしたくなったら、Xボタンを押すのよ。ただリロードすると、今の磁力がリセットされる点には注意して。"));        //17

        currentStep = 11;
        // 仕様⑩: 自分への磁力(LB)以外のすべての機能を解除（リロードもここで解禁）
        SetPlayerLockAll(false);
        SetPlayerSelfMagnetLock(true);

        while (currentStep == 11)
        {
            yield return null;
        }

        currentStep = 12;
        // 解除済みの機能はそのまま（図2でも再ロックしない）。自分への磁力だけ⑫で解禁する
        yield return StartCoroutine(TypeAndAutoAdvance("高い壁ね..、でも問題ないわ、あなたの銃の機能を応用すればね。"));        //18
        yield return StartCoroutine(TypeAndAutoAdvance("実はあなたの銃には、あなた自身にもＮかＳの磁力を付与する機能があるの。LBを押してみて。"));        //19


        currentStep = 13;
        SetPlayerSelfMagnetLock(false);

        while (currentStep == 13)
        {
            yield return null;
        }

        currentStep = 14;
        yield return StartCoroutine(TypeAndAutoAdvance("見えるかしら？あなたの体からドームが展開されているでしょ？"));        //20
        yield return StartCoroutine(TypeAndAutoAdvance("この間はあなた自身が、周りの磁力を与えた物体の動きへ影響を与えるようになるわ。"));        //21
        yield return StartCoroutine(TypeAndAutoAdvance("それだけじゃなく、あなたが空中に居る間はあなた自身も磁力の影響を受けるようになるの。それを活かして、この壁を超えてみせて。"));        //22
        SetPlayerLockAll(false);

        currentStep = 14;
        while (currentStep == 14)
        {
            yield return null;
        }

        currentStep = 15;
        SetPlayerLockAll(true);
        isZooming = true;

        yield return StartCoroutine(TypeAndAutoAdvance("やるわね、もうかなり磁力を使いこなしているんじゃない？"));        //23
        yield return StartCoroutine(TypeAndAutoAdvance("でも本番はここからよ。目の前の不気味なロボットが見える？"));        //24
        yield return StartCoroutine(TypeAndAutoAdvance("これが私たちの街の安全を脅かす敵よ。あなたの任務におけるターゲットってところね。"));        //25
        yield return StartCoroutine(TypeAndAutoAdvance("次はこの敵を相手に、戦闘のシミュレーションをしてみましょう。"));        //26
        yield return StartCoroutine(TypeAndAutoAdvance("この敵は、巨大な斧を振り回してあなたを攻撃してくるわ。でも動きは遅いから、回避は簡単なはずよ。"));        //27

        isZooming = false;

        currentStep = 16;
        SetPlayerLockAll(false);
        yield return StartCoroutine(TypeAndAutoAdvance("そしてあなたの銃はこの敵にも磁力を付与することができるの。"));        //28
        yield return StartCoroutine(TypeAndAutoAdvance("さっきと同じようにこいつにオブジェクトを引き寄せて、思いっきりぶつけてやりなさい！"));        //29

        while (currentStep == 16)
        {
            yield return null;
        }

        currentStep = 17;
        yield return StartCoroutine(TypeAndAutoAdvance("流石ね、シミュレーションとはいえ見事な手際だわ。この調子で進んでいきましょう。"));        //30

        while (currentStep == 17)
        {
            yield return null;
        }

        currentStep = 18;
        yield return StartCoroutine(TypeAndAutoAdvance("気をつけて、タレットが設置されているわ。"));        //31
        yield return StartCoroutine(TypeAndAutoAdvance("あのタレットはなかなかの脅威ね。でもあなたの磁力は何にでも付けることができる。タレットだって例外じゃないわ。それで狙いを誘導できるはずよ。"));        //32

        while (currentStep == 18)
        {
            yield return null;
        }

        currentStep = 19;
        yield return StartCoroutine(TypeAndAutoAdvance("よくやったわ。今回のシミュレーションはここまでよ。"));        //33
        yield return StartCoroutine(TypeAndAutoAdvance("これからあなたには実際の任務についてもらうわ。今回のシミュレーションの内容を忘れずに頑張ってね。"));        //34
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
    // step7突入時の磁極を基準に取り、実際に切り替わった瞬間だけ進める
    private MagneticPole m_poleBaseline;
    private bool m_poleBaselineSet;

    private void OnMagnetSwitched()
    {
        if (currentStep != 7)
        {
            m_poleBaselineSet = false;
            return;
        }

        var p = Player.Current;
        if (p == null || p.pole == null) return;

        // 生の RB/Q 入力ではなく、実際の磁極変化を見る（他の検知と同じく状態ベース）
        if (!m_poleBaselineSet)
        {
            m_poleBaseline = p.pole.CurrentPole;
            m_poleBaselineSet = true;
            return;
        }

        if (p.pole.CurrentPole != m_poleBaseline)
            currentStep = 8;
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
        if (currentStep != 13) return;

        var pt = GetPlayerTransform();
        if (pt == null) return;

        // プレイヤー自身に付いている「Magnetizable」を取得し、磁力がONになったかをチェック
        Magnetizable playerMagnet = pt.GetComponent<Magnetizable>();
        if (playerMagnet != null && playerMagnet.IsActive)
            currentStep = 14;
    }

    // プレイヤーは GameScene 側（Additive ロード）に居るためシーン参照では繋がらない。
    // Inspector 参照があれば優先し、無ければ Player.Current で実行時に解決する。
    private Transform GetPlayerTransform()
    {
        if (playerTransform != null) return playerTransform;
        if (Player.Current != null) return Player.Current.transform;
        return null;
    }

    // カメラは Cinemachine 制御（_Camera.prefab の CameraSettingsApplier）なので mainCamera を直接回すと競合する。
    // ピボットを動かす CameraSettingsApplier.FocusOn 経由でエネミーに向ける。
    private void CameraZoom()
    {
        var applier = GetCameraApplier();
        if (applier == null) return;

        if (isZooming && tutorialEnemy != null)
            applier.FocusOn(tutorialEnemy.transform, zoomFOV);
        else
            applier.ClearFocus();
    }

    private CameraSettingsApplier m_cameraApplier;
    private CameraSettingsApplier GetCameraApplier()
    {
        if (m_cameraApplier == null) m_cameraApplier = FindAnyObjectByType<CameraSettingsApplier>();
        return m_cameraApplier;
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

    // 機能ロックは _Player.prefab の PlayerAbilityLocker に委譲する。
    // ロックの解除/再ロックはすべてメッセージ進行に合わせてこのコルーチンから行う（コライダー不要）。
    // プレイヤーは GameScene 側（Additive）に居るので、locker は Player.Current 等で実行時解決する。
    private PlayerAbilityLocker m_locker;

    private PlayerAbilityLocker GetLocker()
    {
        if (m_locker != null) return m_locker;
        if (playerTransform != null) m_locker = playerTransform.GetComponentInChildren<PlayerAbilityLocker>(true);
        if (m_locker == null && Player.Current != null) m_locker = Player.Current.abilityLocker;
        if (m_locker == null) m_locker = FindAnyObjectByType<PlayerAbilityLocker>();
        if (m_locker == null) Debug.LogWarning("[Tutorial] PlayerAbilityLocker が見つからない — ロックが効きません");
        return m_locker;
    }

    // 移動・カメラを含む全機能をロック/解除する（仕様①開始時・⑭エネミー遭遇時の全ロック）。
    private void SetPlayerLockAll(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        foreach (PlayerAbilityType ability in System.Enum.GetValues(typeof(PlayerAbilityType)))
            locker.SetLocked(ability, isLock);
    }

    // 仕様③メッセージ4で解除する移動・カメラ・ジャンプ。
    private void SetPlayerMovementLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.Move, isLock);
        locker.SetLocked(PlayerAbilityType.Camera, isLock);
        locker.SetLocked(PlayerAbilityType.Jump, isLock);
    }

    // 射撃グループ＝エイム＋射撃＋リロード（銃の一式。射撃が使えるならリロードも使える）。
    private void SetPlayerShootingLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.Aim, isLock);
        locker.SetLocked(PlayerAbilityType.Fire, isLock);
        locker.SetLocked(PlayerAbilityType.Reload, isLock);
    }

    // 磁極切替（RB）。
    private void SetPlayerSwitchMagnetLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.PoleSwitch, isLock);
    }

    // 自分への磁力付与（LB / セルフファイア）。
    private void SetPlayerSelfMagnetLock(bool isLock)
    {
        var locker = GetLocker();
        if (locker == null) return;
        locker.SetLocked(PlayerAbilityType.SelfFire, isLock);
    }
}