using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Tayx.Graphy;

/// <summary>
/// Graphyの表示を3段階でトグルし、RAMモジュールのラベルを日本語化する。
/// 0回目: 全表示 / 1回目: ADVANCED(左下)非表示 / 2回目: 全部非表示
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
        m_graphy = GetComponent<GraphyManager>();
    }

    void Start()
    {
        var ram = transform.Find("SafeArea/RAM - Module");
        if (ram == null) return;

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
        if (Keyboard.current == null) return;
        if (m_graphy == null) return;
        if (!Keyboard.current[m_toggleKey].wasPressedThisFrame) return;

        m_stage = (m_stage + 1) % 3;

        if (m_stage == 0)
        {
            m_graphy.FpsModuleState = GraphyManager.ModuleState.FULL;
            m_graphy.RamModuleState = GraphyManager.ModuleState.FULL;
            m_graphy.AudioModuleState = GraphyManager.ModuleState.FULL;
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
            m_graphy.AudioModuleState = GraphyManager.ModuleState.OFF;
            m_graphy.AdvancedModuleState = GraphyManager.ModuleState.OFF;
        }
    }
}
