using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine拡張: 磁力場の方向にカメラのReferenceUpをSlerp追従させる。
/// CinemachineCameraにAddComponentして使用。
/// </summary>
public class CinemachineMagnetUp : CinemachineExtension
{
    [SerializeField] private float alignSpeed = 360f;

    private Quaternion currentAlignment = Quaternion.identity;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;

        // Followターゲットからtransform.upを取得
        var target = vcam.Follow;
        if (target == null) return;

        Vector3 targetUp = target.up;

        // 目標アライメント
        var targetAlignment = Quaternion.FromToRotation(Vector3.up, targetUp);

        // Slerp遷移
        float maxAngle = alignSpeed * deltaTime;
        float angle = Quaternion.Angle(currentAlignment, targetAlignment);

        if (angle > 0.01f)
        {
            float t = Mathf.Clamp01(maxAngle / angle);
            currentAlignment = Quaternion.Slerp(currentAlignment, targetAlignment, t);
        }
        else
        {
            currentAlignment = targetAlignment;
        }

        // ReferenceUpを書き換え
        state.ReferenceUp = currentAlignment * Vector3.up;
    }
}