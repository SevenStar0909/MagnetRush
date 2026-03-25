using UnityEngine;
using TMPro;

namespace MagnetRush.UI
{
    /// <summary>
    /// 残弾数UIの表示・更新。BulletManagerのイベントを購読する。
    /// </summary>
    public class AmmoUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI ammoText;

        void Start()
        {
            if (BulletManager.Instance != null)
            {
                BulletManager.Instance.OnBulletCountChanged += UpdateDisplay;
                UpdateDisplay(0);
            }
        }

        void OnDestroy()
        {
            if (BulletManager.Instance != null)
            {
                BulletManager.Instance.OnBulletCountChanged -= UpdateDisplay;
            }
        }

        private void UpdateDisplay(int currentCount)
        {
            if (ammoText == null) return;
            int max = BulletManager.Instance != null ? BulletManager.Instance.MaxBullets : 4;
            int remaining = max - currentCount;
            ammoText.text = $"残弾: {remaining}/{max}";
        }
    }
}
