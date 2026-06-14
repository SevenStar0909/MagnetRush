using UnityEngine;

public class PingPongRotation : MonoBehaviour
{
    [Header("基準回転")]
    public Vector3 defaultRotation;

    [Header("最小回転")]
    public Vector3 minRotation;

    [Header("最大回転")]
    public Vector3 maxRotation;

    [Header("軸ごとの速度")]
    public Vector3 speed = new Vector3(1f, 1f, 1f);

    void Update()
    {
        float x = defaultRotation.x +
                  Mathf.Lerp(minRotation.x, maxRotation.x,
                  Mathf.PingPong(Time.time * speed.x, 1f));

        float y = defaultRotation.y +
                  Mathf.Lerp(minRotation.y, maxRotation.y,
                  Mathf.PingPong(Time.time * speed.y, 1f));

        float z = defaultRotation.z +
                  Mathf.Lerp(minRotation.z, maxRotation.z,
                  Mathf.PingPong(Time.time * speed.z, 1f));

        transform.localRotation = Quaternion.Euler(x, y, z);
    }
}