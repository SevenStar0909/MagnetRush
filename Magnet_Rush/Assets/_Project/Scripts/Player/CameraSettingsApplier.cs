using UnityEngine;
using UnityEngine.Serialization;
using Unity.Cinemachine;

public class CameraSettingsApplier : MonoBehaviour
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;
    [FormerlySerializedAs("cinemachineCamera")]
    [SerializeField] private CinemachineCamera m_cinemachineCamera;

    private CinemachineOrbitalFollow m_orbitalFollow;
    private float m_defaultFOV;
    private Cinemachine3OrbitRig.Settings m_defaultOrbits;

    void Start()
    {
        if (m_cinemachineCamera != null)
        {
            m_orbitalFollow = m_cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            m_defaultFOV = m_cinemachineCamera.Lens.FieldOfView;
            if (m_orbitalFollow != null)
                m_defaultOrbits = m_orbitalFollow.Orbits;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// エイムモード切替。カメラ距離とFOVを変更する。
    /// </summary>
    public void SetAimMode(bool aiming)
    {
        if (m_orbitalFollow == null || m_settings == null) return;

        if (aiming)
        {
            // エイム時: Orbitsを縮小してカメラを寄せる
            float scale = m_settings.aimCameraDistance / m_settings.cameraDistance;
            var orbits = m_defaultOrbits;
            orbits.Top = new Cinemachine3OrbitRig.Orbit
                { Height = m_defaultOrbits.Top.Height, Radius = m_defaultOrbits.Top.Radius * scale };
            orbits.Center = new Cinemachine3OrbitRig.Orbit
                { Height = m_defaultOrbits.Center.Height, Radius = m_defaultOrbits.Center.Radius * scale };
            orbits.Bottom = new Cinemachine3OrbitRig.Orbit
                { Height = m_defaultOrbits.Bottom.Height, Radius = m_defaultOrbits.Bottom.Radius * scale };
            m_orbitalFollow.Orbits = orbits;
        }
        else
        {
            m_orbitalFollow.Orbits = m_defaultOrbits;
        }

        if (m_cinemachineCamera != null)
        {
            var lens = m_cinemachineCamera.Lens;
            lens.FieldOfView = aiming ? m_settings.aimFOV : m_defaultFOV;
            m_cinemachineCamera.Lens = lens;
        }
    }
}
