using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Tayx.Graphy;
using Tayx.Graphy.Audio;

/// <summary>
/// Graphyの表示を3段階でトグルし、RAMモジュールのラベルを日本語化する。
/// 起動時: 全表示 / F2を1回押下: ADVANCED(左下)非表示 / 2回押下: 全部非表示 / 3回押下: 全表示に戻る
/// Audio モジュールは Unity 標準オーディオ無効化（CRI ADX2 採用）のため常時 OFF。
/// </summary>
[RequireComponent(typeof(GraphyManager))]
public class GraphyStagedToggle : MonoBehaviour
{
    [SerializeField] private Key m_toggleKey = Key.F2;
    [SerializeField] private Font m_japaneseFont;

    private GraphyManager m_graphy;
    private int m_stage;

    void Awake()
    {
        // Release ビルドでは GraphyManager 自体を破棄して FPS/メモリ UI を完全無効化する
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            Destroy(gameObject);
            return;
        }

        m_graphy = GetComponent<GraphyManager>();

        // CRI ADX2 採用で Unity 標準オーディオを切ってある（AudioManager.asset: m_DisableAudio=1）。
        // Graphy の G_AudioMonitor.Update が AudioListener.GetOutputData を毎フレ呼んでエラーを出すため、
        // コンポーネント自体を Awake で無効化して Update を止める（SetActive 経路だと初期1〜2フレ取りこぼす）
        var audioMonitors = GetComponentsInChildren<G_AudioMonitor>(includeInactive: true);
        for (int i = 0; i < audioMonitors.Length; i++)
        {
            audioMonitors[i].enabled = false;
        }
    }

    void Start()
    {
        // GraphyManager の内部モジュール初期化が Start 完了後に終わるため、
        // ModuleState setter を直接呼ぶと NRE になる。1フレ遅延してから設定する
        StartCoroutine(InitializeModulesNextFrame());

        var ram = transform.Find("SafeArea/RAM - Module");
        if (ram == null) { ChannelLogger.LogGuardReturn("UI", "RAM - Moduleが見つからない"); return; }

        var reserved = ram.Find("reserved_ram_text");
        if (reserved != null)
        {
            var t = reserved.GetComponent<Text>();
            if (t != null)
            {
                t.text = "予約";
                if (m_japaneseFont != null) t.font = m_japaneseFont;
            }
        }

        var allocated = ram.Find("allocated_ram_text");
        if (allocated != null)
        {
            var t = allocated.GetComponent<Text>();
            if (t != null)
            {
                t.text = "確保";
                if (m_japaneseFont != null) t.font = m_japaneseFont;
            }
        }

        var mono = ram.Find("mono_ram_text");
        if (mono != null)
        {
            var t = mono.GetComponent<Text>();
            if (t != null)
            {
                t.text = "Mono";
                if (m_japaneseFont != null) t.font = m_japaneseFont;
            }
        }
    }

    void Update()
    {
        if (Keyboard.current == null) { ChannelLogger.LogGuardReturn("UI", "Keyboardデバイスなし"); return; }
        if (m_graphy == null) { ChannelLogger.LogGuardReturn("UI", "GraphyManager未設定"); return; }
        if (!Keyboard.current[m_toggleKey].wasPressedThisFrame) { ChannelLogger.LogGuardReturn("UI", "トグルキー未押下"); return; }

        m_stage = (m_stage + 1) % 3;

        if (m_stage == 0)
        {
            m_graphy.FpsModuleState = GraphyManager.ModuleState.FULL;
            m_graphy.RamModuleState = GraphyManager.ModuleState.FULL;
            m_graphy.AdvancedModuleState = GraphyManager.ModuleState.FULL;
        }
        else if (m_stage == 1)
        {
            m_graphy.AdvancedModuleState = GraphyManager.ModuleState.OFF;
        }
        else
        {
            m_graphy.FpsModuleState = GraphyManager.ModuleState.OFF;
            m_graphy.RamModuleState = GraphyManager.ModuleState.OFF;
            m_graphy.AdvancedModuleState = GraphyManager.ModuleState.OFF;
        }
    }

    private System.Collections.IEnumerator InitializeModulesNextFrame()
    {
        yield return null; // GraphyManager の Awake/Start で内部モジュール生成を待つ
        if (m_graphy == null) yield break;
        m_graphy.FpsModuleState = GraphyManager.ModuleState.FULL;
        m_graphy.RamModuleState = GraphyManager.ModuleState.FULL;
        m_graphy.AudioModuleState = GraphyManager.ModuleState.OFF;
        m_graphy.AdvancedModuleState = GraphyManager.ModuleState.FULL;
        m_stage = 0; // 次の F2 押下で stage 1 (Advanced 非表示) に進む
    }
}
