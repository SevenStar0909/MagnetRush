using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IMGUIベースのデバッグチートメニュー。
/// F2キーで表示/非表示。カテゴリ分けされたボタンでチートを実行する。
/// Editor + Development Buildのみ動作。
/// </summary>
public class DebugActionMenu : MonoBehaviour
{
#if !DEBUG && !UNITY_EDITOR
    void Awake() { Destroy(this); }
#else
    private bool m_isVisible;
    private Vector2 m_scrollPos;
    private Rect m_windowRect = new Rect(10, 10, 300, 400);

    static readonly List<DebugCategory> s_categories = new List<DebugCategory>();

    class DebugCategory
    {
        public string name;
        public bool foldout = true;
        public List<DebugAction> actions = new List<DebugAction>();
    }

    class DebugAction
    {
        public string label;
        public Action onExecute;
    }

    void Awake()
    {
        RegisterDefaultActions();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            m_isVisible = !m_isVisible;
    }

    void OnGUI()
    {
        if (!m_isVisible) { ChannelLogger.LogGuardReturn("UI", "メニュー非表示中"); return; }
        m_windowRect = GUILayout.Window(999, m_windowRect, DrawWindow, "デバッグメニュー [F2]");
    }

    void DrawWindow(int id)
    {
        m_scrollPos = GUILayout.BeginScrollView(m_scrollPos);

        foreach (var category in s_categories)
        {
            category.foldout = GUILayout.Toggle(category.foldout, category.name, "foldout");
            if (!category.foldout) continue;

            GUILayout.BeginVertical("box");
            foreach (var action in category.actions)
            {
                if (GUILayout.Button(action.label))
                {
                    try
                    {
                        action.onExecute?.Invoke();
                        Debug.Log($"[DebugAction] {action.label} を実行");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[DebugAction] {action.label} でエラー: {e.Message}");
                    }
                }
            }
            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();

        // タイムスケール
        GUILayout.Space(4);
        GUILayout.Label($"TimeScale: {Time.timeScale:F1}");
        Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0f, 3f);

        GUILayout.Space(4);
        if (GUILayout.Button("閉じる"))
            m_isVisible = false;

        GUI.DragWindow();
    }

    /// <summary>
    /// デバッグアクションを登録する。外部スクリプトから呼び出し可能。
    /// </summary>
    public static void Register(string category, string label, Action action)
    {
        var cat = s_categories.Find(c => c.name == category);
        if (cat == null)
        {
            cat = new DebugCategory { name = category };
            s_categories.Add(cat);
        }
        cat.actions.Add(new DebugAction { label = label, onExecute = action });
    }

    /// <summary>
    /// 全登録アクションをクリアする。
    /// </summary>
    public static void ClearAll()
    {
        s_categories.Clear();
    }

    void RegisterDefaultActions()
    {
        s_categories.Clear();

        // プレイヤー
        Register("プレイヤー", "HP全回復", () =>
        {
            var player = FindAnyObjectByType<Player>();
            if (player != null && player.m_health != null)
                player.m_health.ResetHealth();
        });

        // 弾
        Register("弾", "全弾クリア", () =>
        {
            if (BulletManager.Instance != null)
                BulletManager.Instance.ClearAll();
        });

        // 敵
        Register("敵", "全敵撃破", () =>
        {
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.m_health != null)
                    enemy.m_health.Damage(9999);
            }
        });

        // システム
        Register("システム", "TimeScale 0（一時停止）", () => Time.timeScale = 0f);
        Register("システム", "TimeScale 1（通常）", () => Time.timeScale = 1f);
        Register("システム", "TimeScale 0.5（スロー）", () => Time.timeScale = 0.5f);
    }
#endif
}
