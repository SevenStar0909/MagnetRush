using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 残弾数UIの表示・更新。スプライト切替方式（極性＋残弾数統合画像）。
/// BulletManagerとPoleAbilityのイベントを購読する。
/// </summary>
public class AmmoUI : MonoBehaviour
{
    [SerializeField] private Image m_ammoImage;

    [Header("S極 残弾スプライト (0〜4)")]
    [SerializeField] private Sprite[] m_spritesS;

    [Header("N極 残弾スプライト (0〜4)")]
    [SerializeField] private Sprite[] m_spritesN;

    private PoleAbility m_pole;
    private MagneticPole m_currentPole = MagneticPole.S;
    private int m_currentRemaining = 4;

    void Start()
    {
        var playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_pole = playerObj.GetComponent<PoleAbility>();
            if (m_pole != null)
            {
                m_pole.OnPoleChanged += OnPoleChanged;
                m_currentPole = m_pole.CurrentPole;
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
        if (m_pole != null)
            m_pole.OnPoleChanged -= OnPoleChanged;

        if (BulletManager.Instance != null)
            BulletManager.Instance.OnBulletCountChanged -= OnBulletCountChanged;
    }

    private void OnPoleChanged(MagneticPole pole)
    {
        m_currentPole = pole;
        UpdateSprite();
    }

    private void OnBulletCountChanged(int usedCount)
    {
        int max = BulletManager.Instance != null ? BulletManager.Instance.MaxBullets : 4;
        m_currentRemaining = Mathf.Clamp(max - usedCount, 0, max);
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (m_ammoImage == null) { ChannelLogger.LogGuardReturn("UI", "残弾Image未設定"); return; }

        Sprite[] sprites = m_currentPole == MagneticPole.S ? m_spritesS : m_spritesN;
        if (sprites != null && m_currentRemaining >= 0 && m_currentRemaining < sprites.Length)
            m_ammoImage.sprite = sprites[m_currentRemaining];
    }
}
