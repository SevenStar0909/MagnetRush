using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 残弾数UIの表示・更新。スプライト切替方式（極性＋残弾数統合画像）。
/// BulletManagerとPolarityControllerのイベントを購読する。
/// </summary>
public class AmmoUI : MonoBehaviour
{
    [SerializeField] private Image ammoImage;

    [Header("S極 残弾スプライト (0〜4)")]
    [SerializeField] private Sprite[] spritesS;

    [Header("N極 残弾スプライト (0〜4)")]
    [SerializeField] private Sprite[] spritesN;

    private PolarityController polarityController;
    private MagneticPole currentPole = MagneticPole.S;
    private int currentRemaining = 4;

    void Start()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null)
        {
            polarityController = player.GetComponent<PolarityController>();
            if (polarityController != null)
            {
                polarityController.OnPolarityChanged += OnPolarityChanged;
                currentPole = polarityController.CurrentPole;
            }
        }

        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.OnBulletCountChanged += OnBulletCountChanged;
            OnBulletCountChanged(0);
        }

        UpdateSprite();
    }

    void OnDestroy()
    {
        if (polarityController != null)
            polarityController.OnPolarityChanged -= OnPolarityChanged;

        if (BulletManager.Instance != null)
            BulletManager.Instance.OnBulletCountChanged -= OnBulletCountChanged;
    }

    private void OnPolarityChanged(MagneticPole pole)
    {
        currentPole = pole;
        UpdateSprite();
    }

    private void OnBulletCountChanged(int usedCount)
    {
        int max = BulletManager.Instance != null ? BulletManager.Instance.MaxBullets : 4;
        currentRemaining = Mathf.Clamp(max - usedCount, 0, max);
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (ammoImage == null) return;

        Sprite[] sprites = currentPole == MagneticPole.S ? spritesS : spritesN;
        if (sprites != null && currentRemaining >= 0 && currentRemaining < sprites.Length)
            ammoImage.sprite = sprites[currentRemaining];
    }
}
