using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// メニュー画面でコントローラー操作の初期フォーカスを保証する。
/// マウスクリック等で選択が外れた場合も既定ボタンへ復帰させる。
/// 依存: EventSystem
/// </summary>
public class MenuFocusController : MonoBehaviour
{
    [SerializeField, Tooltip("シーン開始時に選択状態にするボタン")]
    private Selectable m_firstSelected;

    private void Start()
    {
        if (m_firstSelected == null)
        {
            ChannelLogger.LogGuardReturn("UI", "MenuFocusController: m_firstSelected が未設定");
            return;
        }
        Select(m_firstSelected.gameObject);
    }

    private void Update()
    {
        // 選択がnullのままだとパッドのナビゲーションが効かなくなるので既定ボタンへ戻す
        if (m_firstSelected != null
            && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == null)
        {
            Select(m_firstSelected.gameObject);
        }
    }

    private void Select(GameObject target)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(target);
    }
}
