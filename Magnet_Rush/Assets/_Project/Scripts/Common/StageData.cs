using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "MagnetRush/StageData")]
public class StageData : ScriptableObject
{
    [System.Serializable]
    public class Stage
    {
        public string mainSceneName;
        public string mapSceneName;
        public GameObject previewPrefab;
    }

    public Stage[] stages = new Stage[3];
}