/// <summary>
/// サウンドデータの認識用のラベル
/// </summary>
public static class SoundData
{
    /// <summary>
    /// acb認識用のラベル
    /// </summary>
    public static class CueSheet
    {
        public const string BGM = "BGM";
        public const string SE = "SE";
        // 適時追加
    }

    /// <summary>
    /// カテゴリー認識用のラベル
    /// </summary>
    public static class Category
    {
        // 適時追加
    }

    // 以下はキュー認識用のラベル

    public static class BGM
    {
        public const string Test = "test";
        // 適時追加
    }

    public static class SE
    {
        public const string PlayerShot = "PlayerShot";
        public const string SelfShot = "SelfShot";
        public const string MagField = "MagField";
        public const string HitObject = "HitObject";
        public const string TurretShot = "TurretShot";
        // 適時追加
    }

    public static class Voice
    {
        // 適時追加
    }
}