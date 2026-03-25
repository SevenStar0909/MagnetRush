using UnityEngine;
using Unity.Cinemachine;

public class CameraSettingsApplier : MonoBehaviour
{
    [SerializeField] private PlayerSettings settings;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private CinemachineOrbitalFollow orbitalFollow;
    private float defaultFOV;
    private Cinemachine3OrbitRig.Settings defaultOrbits;

    void Start()
    {
        if (cinemachineCamera != null)
        {
            orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            defaultFOV = cinemachineCamera.Lens.FieldOfView;
            if (orbitalFollow != null)
                defaultOrbits = orbitalFollow.Orbits;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// エイムモード切替。カメラ距離とFOVを変更する。
    /// </summary>
    public void SetAimMode(bool aiming)
    {
        if (orbitalFollow == null || settings == null) return;

        if (aiming)
        {
            // エイム時: Orbitsを縮小してカメラを寄せる
            float scale = settings.aimCameraDistance / settings.cameraDistance;
            var orbits = defaultOrbits;
            orbits.Top = new Cinemachine3OrbitRig.Orbit
                { Height = defaultOrbits.Top.Height, Radius = defaultOrbits.Top.Radius * scale };
            orbits.Center = new Cinemachine3OrbitRig.Orbit
                { Height = defaultOrbits.Center.Height, Radius = defaultOrbits.Center.Radius * scale };
            orbits.Bottom = new Cinemachine3OrbitRig.Orbit
                { Height = defaultOrbits.Bottom.Height, Radius = defaultOrbits.Bottom.Radius * scale };
            orbitalFollow.Orbits = orbits;
        }
        else
        {
            orbitalFollow.Orbits = defaultOrbits;
        }

        if (cinemachineCamera != null)
        {
            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = aiming ? settings.aimFOV : defaultFOV;
            cinemachineCamera.Lens = lens;
        }
    }
}
