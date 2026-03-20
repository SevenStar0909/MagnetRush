using UnityEngine;
using System;

public class Magnetizable : MonoBehaviour
{
    [SerializeField] private MagneticPole pole = MagneticPole.None;
    [SerializeField] private bool isActive;

    public MagneticPole Pole => pole;
    public bool IsActive => isActive;

    public event Action<MagneticPole> OnPoleChanged;

    public void SetPole(MagneticPole newPole)
    {
        pole = newPole;
        isActive = newPole != MagneticPole.None;
        OnPoleChanged?.Invoke(pole);
    }

    public MagneticPole GetPole()
    {
        return pole;
    }

    public void Deactivate()
    {
        pole = MagneticPole.None;
        isActive = false;
        OnPoleChanged?.Invoke(pole);
    }
}
