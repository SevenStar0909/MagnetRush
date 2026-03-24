// TMPのEssentials Importダイアログを自動的に抑制する
#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public static class SuppressTMPDialog
{
    static SuppressTMPDialog()
    {
        // TMP Essentialsが既にインポート済みならダイアログを表示しない
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
        {
            EditorPrefs.SetBool("TMPro.Preferences.PackageImported", true);
        }
    }
}
#endif
