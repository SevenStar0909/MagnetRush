using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// PlayerAnimator.controller の StateMachine を再構築する Editor 専用ビルダー。
/// FBX に同梱された AnimationClip を Idle / Locomotion / Attack の各 State に割り当てる。
/// パラメータ定義は既存 Controller のものを保持し、State / Transition / サブアセットのみ作り直す。
/// 依存: UnityEditor.Animations
/// </summary>
public static class PlayerAnimatorBuilder
{
    private const string ControllerPath = "Assets/_Project/Asset/Animations/Player/PlayerAnimator.controller";
    private const string IdleFbxPath    = "Assets/_Project/Asset/Animations/Player/A_Player_Animation_Idle.fbx";
    private const string AttackFbxPath  = "Assets/_Project/Asset/Animations/Player/A_Player_Animation_AttackMotion.fbx";
    private const string RunFbxPath     = "Assets/_Project/Asset/Animations/Player/Player_Run_v_1.fbx";
    private const string RoughFbxPath   = "Assets/_Project/Asset/Models/Player/Alpha/Player_Rough.fbx";
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/_Player.prefab";
    private const string ModelChildName = "Model";

    private const string MoveSpeedParam = "MoveSpeed";
    private const string ShootParam     = "Shoot";
    private const float  MoveSpeedThreshold = 0.1f;

    [MenuItem("Tools/Player/Build Animator Controller")]
    public static void Build()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] Controller が見つかりません: {ControllerPath}");
            return;
        }

        var idleClip   = LoadFirstClip(IdleFbxPath);
        var attackClip = LoadFirstClip(AttackFbxPath);
        var runClip    = LoadFirstClip(RunFbxPath);
        if (idleClip == null || attackClip == null || runClip == null) return;

        // パラメータが揃っているか軽く検証（PlayerAnimator.cs と整合）
        var paramNames = controller.parameters.Select(p => p.name).ToHashSet();
        foreach (var required in new[] { MoveSpeedParam, ShootParam })
        {
            if (!paramNames.Contains(required))
            {
                Debug.LogError($"[PlayerAnimatorBuilder] Controller に必須パラメータ '{required}' が無い");
                return;
            }
        }

        var rootSm = controller.layers[0].stateMachine;

        // 旧サブアセット（State / BlendTree / Transition）を一掃
        CleanSubAssets(controller, rootSm);
        rootSm.states              = new ChildAnimatorState[0];
        rootSm.anyStateTransitions = new AnimatorStateTransition[0];
        rootSm.entryTransitions    = new AnimatorTransition[0];

        // Idle (Default)
        var idleState = rootSm.AddState("Idle", new Vector3(290, 110, 0));
        idleState.motion = idleClip;
        idleState.writeDefaultValues = true;

        // Locomotion (BlendTree: 1Dブレンド、現状はRunのみだが将来Walk等を追加できる)
        var locoState = rootSm.AddState("Locomotion", new Vector3(290, 220, 0));
        var blendTree = new BlendTree
        {
            name = "Run BlendTree",
            blendType = BlendTreeType.Simple1D,
            blendParameter = MoveSpeedParam,
            useAutomaticThresholds = true,
        };
        blendTree.AddChild(runClip, 0f);
        AssetDatabase.AddObjectToAsset(blendTree, controller);
        locoState.motion = blendTree;
        locoState.writeDefaultValues = true;

        // Attack (1ショット)
        var attackState = rootSm.AddState("Attack", new Vector3(530, 110, 0));
        attackState.motion = attackClip;
        attackState.writeDefaultValues = true;

        rootSm.defaultState = idleState;

        // Idle -> Locomotion: MoveSpeed > threshold
        var t1 = idleState.AddTransition(locoState);
        t1.hasExitTime = false;
        t1.duration = 0.1f;
        t1.AddCondition(AnimatorConditionMode.Greater, MoveSpeedThreshold, MoveSpeedParam);

        // Locomotion -> Idle: MoveSpeed < threshold
        var t2 = locoState.AddTransition(idleState);
        t2.hasExitTime = false;
        t2.duration = 0.1f;
        t2.AddCondition(AnimatorConditionMode.Less, MoveSpeedThreshold, MoveSpeedParam);

        // Any State -> Attack: Shoot trigger（連射対応で canTransitionToSelf=true）
        var ta = rootSm.AddAnyStateTransition(attackState);
        ta.hasExitTime = false;
        ta.duration = 0.05f;
        ta.canTransitionToSelf = true;
        ta.AddCondition(AnimatorConditionMode.If, 0, ShootParam);

        // Attack -> Idle: Has Exit Time（クリップ末尾で復帰、その後 MoveSpeed で Locomotion へ流れる）
        var t3 = attackState.AddTransition(idleState);
        t3.hasExitTime = true;
        t3.exitTime = 0.9f;
        t3.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PlayerAnimatorBuilder] AnimatorController 再構築完了: Idle={idleClip.name}, Run={runClip.name}, Attack={attackClip.name}");
    }

    /// <summary>
    /// 新規追加 FBX のデフォルトクリップに名前と Loop 設定を適用する。
    /// defaultClipAnimations の internalID をそのまま流用するため、AnimatorController の Motion 参照は壊れない。
    /// </summary>
    [MenuItem("Tools/Player/Rename Animation Clips")]
    public static void RenameClips()
    {
        RenameClip(IdleFbxPath,   "A_Idle",   loop: true);
        RenameClip(AttackFbxPath, "A_Attack", loop: false);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerAnimatorBuilder] AnimationClip リネーム完了");
    }

    private static void RenameClip(string fbxPath, string newName, bool loop)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] ModelImporter が取得できない: {fbxPath}");
            return;
        }

        // 既に clipAnimations が設定されていればそれを、無ければ default を流用（internalID 保持）
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }
        if (clips == null || clips.Length == 0)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] AnimationClip が見つからない: {fbxPath}");
            return;
        }

        clips[0].name = newName;
        clips[0].loopTime = loop;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// SwitchPlayerModelToRough が壊した Importer 設定を初期状態（Generic / NoAvatar）に戻す。
    /// Player.prefab と Run_v_1.fbx.meta は別途 git checkout で復元する。
    /// </summary>
    [MenuItem("Tools/Player/Rollback Importer Settings")]
    public static void RollbackImporterSettings()
    {
        AssetDatabase.SaveAssets();
        ResetImporterToGenericNoAvatar(IdleFbxPath);
        ResetImporterToGenericNoAvatar(AttackFbxPath);
        ResetImporterToGenericNoAvatar(RoughFbxPath);
        Debug.Log("[PlayerAnimatorBuilder] Importer 設定をロールバック完了 (Idle/Attack/Rough)");
    }

    private static void ResetImporterToGenericNoAvatar(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] ModelImporter が取得できない: {fbxPath}");
            return;
        }
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }
        if (importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            changed = true;
        }
        if (importer.sourceAvatar != null)
        {
            importer.sourceAvatar = null;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    /// <summary>
    /// Player_Rough と Player_Run_v_1 のボーン階層を出力して構造比較する。
    /// Generic Animator で動かせるかの判断材料にする。パスが一致するボーンが多ければ動く。
    /// </summary>
    [MenuItem("Tools/Player/Print Skeleton Hierarchy")]
    public static void PrintSkeletonHierarchy()
    {
        PrintHierarchy(RoughFbxPath);
        PrintHierarchy(RunFbxPath);
    }

    /// <summary>
    /// アニメ再生失敗時の診断: 各FBXのスケルトン階層・AnimationClipのバインディングpath・Importer 設定を Temp/ に出力。
    /// Generic 再生では Animator 側スケルトンの "path" と Clip 内 "binding.path" が一致する必要がある。
    /// </summary>
    /// <summary>
    /// シーン上の _Player を見つけ、Animator/SkinnedMeshRenderer の現在状態を Temp/scene_player.txt に出力する。
    /// PlayMode 中に呼べば current state や parameter 値も取れる。
    /// </summary>
    [MenuItem("Tools/Player/Inspect Scene Player")]
    public static void InspectScenePlayer()
    {
        var sb = new System.Text.StringBuilder();
        var roots = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects();
        GameObject playerGo = null;
        foreach (var root in roots)
        {
            var found = root.GetComponentsInChildren<Transform>(true)
                .Select(t => t.gameObject)
                .FirstOrDefault(g => g.name == "_Player" || g.name == "Player");
            if (found != null) { playerGo = found; break; }
        }
        if (playerGo == null)
        {
            sb.AppendLine("シーンに '_Player' / 'Player' が見つからない");
        }
        else
        {
            sb.AppendLine($"=== Scene Player ===");
            sb.AppendLine($"GameObject: {playerGo.name} active={playerGo.activeInHierarchy}");
            var animator = playerGo.GetComponentInChildren<Animator>(true);
            if (animator == null) sb.AppendLine("Animator が見つからない");
            else
            {
                sb.AppendLine($"Animator: on={animator.gameObject.name} enabled={animator.enabled}");
                sb.AppendLine($"  controller={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}");
                sb.AppendLine($"  avatar={(animator.avatar != null ? animator.avatar.name : "null")} valid={(animator.avatar != null && animator.avatar.isValid)}");
                sb.AppendLine($"  cullingMode={animator.cullingMode} updateMode={animator.updateMode} applyRootMotion={animator.applyRootMotion}");
                if (Application.isPlaying)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    sb.AppendLine($"  CURRENT_STATE shortNameHash={info.shortNameHash} normalizedTime={info.normalizedTime:F2} length={info.length:F2}");
                    foreach (var p in animator.parameters)
                    {
                        string val = p.type switch
                        {
                            AnimatorControllerParameterType.Float => animator.GetFloat(p.name).ToString("F3"),
                            AnimatorControllerParameterType.Int   => animator.GetInteger(p.name).ToString(),
                            AnimatorControllerParameterType.Bool  => animator.GetBool(p.name).ToString(),
                            _ => "(trigger)",
                        };
                        sb.AppendLine($"  param {p.name} = {val}");
                    }
                }
                else
                {
                    sb.AppendLine("  (PlayMode ではないため CurrentState は取得不可)");
                }
            }
            var smr = playerGo.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null) sb.AppendLine("SkinnedMeshRenderer が見つからない");
            else
            {
                sb.AppendLine($"SkinnedMeshRenderer: on={smr.gameObject.name} enabled={smr.enabled}");
                sb.AppendLine($"  mesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "null")} bones={smr.bones?.Length ?? 0}");
                sb.AppendLine($"  rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")}");
                sb.AppendLine($"  updateWhenOffscreen={smr.updateWhenOffscreen}");
            }
        }
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText("Temp/scene_player.txt", sb.ToString());
        Debug.Log("[PlayerAnimatorBuilder] InspectScenePlayer: Temp/scene_player.txt");
    }

    [MenuItem("Tools/Player/Diagnose Animation")]
    public static void DiagnoseAnimation()
    {
        PrintHierarchy(RunFbxPath);
        PrintHierarchy(IdleFbxPath);
        PrintHierarchy(AttackFbxPath);
        PrintClipInfo(RunFbxPath);
        PrintClipInfo(IdleFbxPath);
        PrintClipInfo(AttackFbxPath);
        PrintImporterSummary();
        PrintPrefabAnimatorState();
    }

    private static void PrintClipInfo(string fbxPath)
    {
        var clip = LoadFirstClip(fbxPath);
        if (clip == null) return;
        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        var objBindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== ClipInfo: {fbxPath} ===");
        sb.AppendLine($"  name={clip.name} length={clip.length:F3}s frameRate={clip.frameRate} legacy={clip.legacy} humanMotion={clip.humanMotion} isLooping={clip.isLooping}");
        sb.AppendLine($"  curveBindings={bindings.Length} objBindings={objBindings.Length}");
        var paths = new System.Collections.Generic.HashSet<string>();
        foreach (var b in bindings) paths.Add(b.path);
        sb.AppendLine($"  uniquePaths={paths.Count}");
        foreach (var p in paths.OrderBy(p => p)) sb.AppendLine($"    {p}");
        var outPath = $"Temp/{System.IO.Path.GetFileNameWithoutExtension(fbxPath)}_clipinfo.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"[PlayerAnimatorBuilder] ClipInfo: {outPath}");
    }

    private static void PrintImporterSummary()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var path in new[] { RunFbxPath, IdleFbxPath, AttackFbxPath })
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            sb.AppendLine($"{path}");
            sb.AppendLine($"  animationType={imp.animationType} avatarSetup={imp.avatarSetup} sourceAvatar={(imp.sourceAvatar != null ? imp.sourceAvatar.name : "null")} importAnimation={imp.importAnimation}");
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            sb.AppendLine($"  hasAvatar={avatar != null} isHuman={(avatar != null ? avatar.isHuman : false)} isValid={(avatar != null ? avatar.isValid : false)}");
        }
        System.IO.File.WriteAllText("Temp/importer_summary.txt", sb.ToString());
        Debug.Log("[PlayerAnimatorBuilder] ImporterSummary: Temp/importer_summary.txt");
    }

    private static void PrintPrefabAnimatorState()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var animator = prefabRoot.GetComponentInChildren<Animator>(true);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Player.prefab Animator ===");
            if (animator == null)
            {
                sb.AppendLine("  Animator が見つからない");
            }
            else
            {
                sb.AppendLine($"  GameObject: {animator.gameObject.name}");
                sb.AppendLine($"  enabled={animator.enabled} runtimeAnimatorController={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}");
                sb.AppendLine($"  avatar={(animator.avatar != null ? animator.avatar.name : "null")} isHuman={(animator.avatar != null ? animator.avatar.isHuman : false)} applyRootMotion={animator.applyRootMotion}");
                sb.AppendLine($"  cullingMode={animator.cullingMode} updateMode={animator.updateMode}");
            }
            // PlayerAnimator.m_animator のSerialized値
            foreach (var mb in prefabRoot.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "PlayerAnimator") continue;
                var so = new SerializedObject(mb);
                var prop = so.FindProperty("m_animator");
                sb.AppendLine($"  PlayerAnimator.m_animator = {(prop != null && prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null")}");
                break;
            }
            System.IO.File.WriteAllText("Temp/prefab_animator.txt", sb.ToString());
            Debug.Log("[PlayerAnimatorBuilder] PrefabAnimatorState: Temp/prefab_animator.txt");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void PrintHierarchy(string fbxPath)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (go == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] FBX ロード失敗: {fbxPath}");
            return;
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {fbxPath} ===");
        PrintTransform(go.transform, 0, sb);
        var outPath = $"Temp/{System.IO.Path.GetFileNameWithoutExtension(fbxPath)}_skeleton.txt";
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"[PlayerAnimatorBuilder] スケルトン書き出し: {outPath}");
    }

    private static void PrintTransform(Transform t, int depth, System.Text.StringBuilder sb)
    {
        for (int i = 0; i < depth; i++) sb.Append("  ");
        sb.AppendLine(t.name);
        foreach (Transform c in t)
            PrintTransform(c, depth + 1, sb);
    }

    /// <summary>
    /// Player.prefab の Model 子（Run_v_1 Prefab Instance）を Player_Rough.fbx ベースに差し替える。
    /// バインドポーズが Run の最初フレームになる問題を解消し、Tポーズの素体モデルでアニメをリターゲット再生する。
    /// 1) Player_Rough.fbx の Avatar を生成（CreateFromThisModel）
    /// 2) Run/Idle/Attack モーションFBX の SourceAvatar を Rough に切替（Humanoidリターゲット）
    /// 3) Player.prefab の Model 子を Player_Rough Prefab Instance に置換し、Animator/Controller/PlayerAnimator.m_animator を再アサイン
    /// </summary>
    [MenuItem("Tools/Player/Switch Model To Rough")]
    public static void SwitchPlayerModelToRough()
    {
        var roughAvatar = EnsureRoughAvatar();
        if (roughAvatar == null) return;

        RetargetMotionFbx(RunFbxPath,    roughAvatar);
        RetargetMotionFbx(IdleFbxPath,   roughAvatar);
        RetargetMotionFbx(AttackFbxPath, roughAvatar);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var roughFbx   = AssetDatabase.LoadAssetAtPath<GameObject>(RoughFbxPath);
        if (controller == null || roughFbx == null)
        {
            Debug.LogError("[PlayerAnimatorBuilder] Controller か Rough FBX のロードに失敗");
            return;
        }

        var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // 旧 Model 子を検出（Run_v_1 由来の Prefab Instance）して削除
            var oldModel = FindModelChild(prefabRoot.transform);
            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            // Player_Rough.fbx を Prefab Instance として追加
            var newModel = (GameObject)PrefabUtility.InstantiatePrefab(roughFbx, prefabRoot.transform);
            newModel.name = ModelChildName;
            newModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            newModel.transform.localScale = Vector3.one;

            // Animator アサイン
            var animator = newModel.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("[PlayerAnimatorBuilder] Player_Rough Prefab Instance に Animator が無い");
                return;
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // PlayerAnimator MB の m_animator フィールドを SerializedObject 経由で再アサイン（asmdef越え回避）
            foreach (var mb in prefabRoot.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "PlayerAnimator") continue;
                var so = new SerializedObject(mb);
                var prop = so.FindProperty("m_animator");
                if (prop != null)
                {
                    prop.objectReferenceValue = animator;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                break;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log("[PlayerAnimatorBuilder] Player.prefab の Model を Player_Rough.fbx に差し替え完了");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
    }

    private static Avatar EnsureRoughAvatar()
    {
        var importer = AssetImporter.GetAtPath(RoughFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] Rough FBX の ModelImporter が取得できない: {RoughFbxPath}");
            return null;
        }
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }
        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }
        if (changed)
        {
            importer.SaveAndReimport();
        }
        var avatar = AssetDatabase.LoadAllAssetsAtPath(RoughFbxPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] Rough FBX から Avatar を生成できなかった: {RoughFbxPath}");
        }
        return avatar;
    }

    private static void RetargetMotionFbx(string fbxPath, Avatar sourceAvatar)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] ModelImporter が取得できない: {fbxPath}");
            return;
        }
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }
        if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            changed = true;
        }
        if (importer.sourceAvatar != sourceAvatar)
        {
            importer.sourceAvatar = sourceAvatar;
            changed = true;
        }
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Transform FindModelChild(Transform root)
    {
        // 名前一致を最優先
        var named = root.Find(ModelChildName);
        if (named != null) return named;

        // 名前未一致でも Run_v_1 由来の Prefab Instance を探す
        foreach (Transform child in root)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (src == null) continue;
            var srcPath = AssetDatabase.GetAssetPath(src);
            if (srcPath == RunFbxPath) return child;
        }
        return null;
    }

    private static AnimationClip LoadFirstClip(string fbxPath)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();
        if (clips.Length == 0)
        {
            Debug.LogError($"[PlayerAnimatorBuilder] AnimationClip が見つかりません: {fbxPath}");
            return null;
        }
        return clips[0];
    }

    private static void CleanSubAssets(AnimatorController controller, AnimatorStateMachine keepRootSm)
    {
        var subs = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
        foreach (var sub in subs)
        {
            if (sub == null) continue;
            if (sub == controller) continue;
            if (sub == keepRootSm) continue;
            if (sub is AnimatorState || sub is BlendTree || sub is AnimatorTransitionBase || sub is AnimatorStateMachine)
            {
                AssetDatabase.RemoveObjectFromAsset(sub);
                Object.DestroyImmediate(sub, true);
            }
        }
    }
}
