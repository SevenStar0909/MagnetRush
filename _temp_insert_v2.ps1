$ErrorActionPreference = "Stop"

$path = "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Enemy/EnemyBossA/EnemyBossBaseA_Animator.cs"
$enc = [System.Text.Encoding]::Default
$content = [System.IO.File]::ReadAllText($path, $enc)

if ($content.Length -lt 1000) {
    throw "file too small, abort: length=$($content.Length)"
}

# anchor must be unique substring
$anchor = [string]"        SetIsStunned(false);`n    }"

if (-not $content.Contains($anchor)) {
    throw "anchor not found"
}

# anchor occurs twice in file (SetIsStunnedTrue + SetIsStunnedFalse), use the second one
# instead, anchor on the unique tail "    public void SetIsStunnedFalse()`n    {`n        SetIsStunned(false);`n    }"
$uniqueAnchor = [string]"    public void SetIsStunnedFalse()`n    {`n        SetIsStunned(false);`n    }"

if (-not $content.Contains($uniqueAnchor)) {
    throw "uniqueAnchor not found"
}

$count = ($content.Split([string[]]@($uniqueAnchor), [System.StringSplitOptions]::None)).Length - 1
if ($count -ne 1) {
    throw "uniqueAnchor matched $count times, expected 1"
}

$insertion = [string]"`n`n    /// <summary>AttackStance または AttackMotion 中なら true。AI が攻撃中判定に使う。</summary>`n    public bool IsAttacking`n    {`n        get`n        {`n            if (m_animator == null) return false;`n            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;`n            return hash == s_hAttackStanceState || hash == s_hAttackMotionState;`n        }`n    }`n`n    /// <summary>AttackMotion 中（振りかぶり?振り抜き）なら true。AI が腕Hitbox期待中の判定に使う。</summary>`n    public bool IsInAttackMotion`n    {`n        get`n        {`n            if (m_animator == null) return false;`n            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackMotionState;`n        }`n    }`n`n    /// <summary>AttackStun 中なら true。Bool ではなく現在 State を見ることで AnimEvent 配線漏れに対しても堅牢。</summary>`n    public bool IsStunned`n    {`n        get`n        {`n            if (m_animator == null) return false;`n            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackStunState;`n        }`n    }`n`n    // Animator State 名のハッシュ（State 名は EnemyBossA_Animator.controller の Layer0 上の State 名と一致する必要がある）`n    private static readonly int s_hAttackStanceState = Animator.StringToHash(""AttackStance"");`n    private static readonly int s_hAttackMotionState = Animator.StringToHash(""AttackMotion"");`n    private static readonly int s_hAttackStunState   = Animator.StringToHash(""AttackStun"");"

$replacement = [string]($uniqueAnchor + $insertion)
$newContent = $content.Replace($uniqueAnchor, $replacement)

if ($newContent.Length -le $content.Length) {
    throw "newContent not larger than original (orig=$($content.Length), new=$($newContent.Length))"
}

[System.IO.File]::WriteAllText($path, $newContent, $enc)
Write-Host "OK orig=$($content.Length) new=$($newContent.Length) delta=$($newContent.Length - $content.Length)"
