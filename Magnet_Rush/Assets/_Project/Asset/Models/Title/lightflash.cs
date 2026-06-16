using UnityEngine;

public class Flashing : MonoBehaviour
{
    [SerializeField] private float maxIntensity = 10f;
    [SerializeField] private float speed = 10f;

    private Light pointLight;
    private float lightStrength = 0f;
    private bool increasing = true;

    void Start()
    {
        pointLight = GetComponent<Light>();

        if (pointLight == null)
        {
            Debug.LogError("Lightコンポーネントが見つかりません");
            enabled = false;
            return;
        }

        pointLight.type = LightType.Point; // ポイントライトに設定
        pointLight.intensity = 0f;
    }

    void Update()
    {
        if (increasing)
        {
            lightStrength += speed * Time.deltaTime;

            if (lightStrength >= maxIntensity)
            {
                lightStrength = maxIntensity;
                increasing = false;
            }
        }
        else
        {
            lightStrength -= speed * Time.deltaTime;

            if (lightStrength <= 0f)
            {
                lightStrength = 0f;
                increasing = true;
            }
        }

        pointLight.intensity = lightStrength;
    }
}
