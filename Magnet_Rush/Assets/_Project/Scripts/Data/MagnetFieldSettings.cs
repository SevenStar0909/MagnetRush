using UnityEngine;

public enum FieldShape { Sphere, Box, Cylinder }

[CreateAssetMenu(fileName = "MagnetFieldSettings", menuName = "MagnetRush/MagnetFieldSettings")]
public class MagnetFieldSettings : ScriptableObject
{
    [Header("形状")]
    public FieldShape shape = FieldShape.Sphere;

    [Header("範囲")]
    [Tooltip("full strength の半径")]
    public float innerRadius = 3f;
    [Tooltip("falloff→0 の半径")]
    public float outerRadius = 8f;

    [Header("強度")]
    public float strength = 20f;

    [Header("ライフタイム")]
    [Tooltip("0=永続")]
    public float lifetime = 12f;

    [Header("Box")]
    public Vector3 size = new Vector3(2f, 2f, 2f);

    [Header("Cylinder")]
    public float cylinderHeight = 4f;
    public float cylinderRadius = 1f;

    [Header("ダメージ蓄積")]
    public bool accumulateDamage = true;
    public float maxStoredDamage = 200f;
}