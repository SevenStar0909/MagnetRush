using System;
using UnityEngine;

/// <summary>
/// 指定型のサブクラス一覧を Inspector でドロップダウン選択させる属性。
/// 対象フィールドは string 型で、選択されたクラスの AssemblyQualifiedName が保存される。
/// </summary>
public class ClassTypeName : PropertyAttribute
{
    public Type type;

    public ClassTypeName(Type type)
    {
        this.type = type;
    }
}
