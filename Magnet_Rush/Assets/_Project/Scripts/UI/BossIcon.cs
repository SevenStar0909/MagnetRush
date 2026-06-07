using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class BossIcon : MonoBehaviour
{
    [Header("砕け散るアニメーション画像 (0番から順に全種類入れる)")]
    [SerializeField] private Sprite[] m_explosionSprites;

    [Header("アニメーションの1フレームの速度（秒）")]
    [SerializeField] private float m_frameRate = 0.03f;

    private Image m_image;

    void Awake()
    {
        m_image = GetComponent<Image>();
    }

    /// <summary>
    /// 砕け散る演出を再生し、終わったら自身を削除する
    /// </summary>
    public void PlayExplosionAndDestroy()
    {
        StartCoroutine(ExplosionCoroutine());
    }

    private IEnumerator ExplosionCoroutine()
    {
        if (m_explosionSprites == null || m_explosionSprites.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        // スプライトシートを0番から順番に切り替えてアニメーションさせる
        for (int i = 0; i < m_explosionSprites.Length; i++)
        {
            m_image.sprite = m_explosionSprites[i];
            yield return new WaitForSeconds(m_frameRate);
        }

        // アニメーションが終わったらオブジェクト自体を消滅させる
        Destroy(gameObject);
    }
}