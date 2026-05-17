$path = "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Enemy/EnemyBossA/EnemyBossBaseA_Animator.cs"
$enc = [System.Text.Encoding]::Default
$content = [System.IO.File]::ReadAllText($path, $enc)

# SetIsStunnedFalse() メソッドを丸ごと検出する。直後にプロパティ群を挿入する。
$anchor = @"
    public void SetIsStunnedFalse()
    {
        SetIsStunned(false);
    }
"@.Replace("`r`n", "`n")

# 既存ファイルの改行コードを判定（CRLF/LF）
$useCrLf = $content.Contains("`r`n")
$nl = if ($useCrLf) { "`r`n" } else { "`n" }

# CRLFファイルの場合、anchorをCRLFに変換
$anchorMatch = if ($useCrLf) { $anchor.Replace("`n", "`r`n") } else { $anchor }

if (-not $content.Contains($anchorMatch)) {
    Write-Error "Anchor not found"
    exit 1
}

# 挿入する新規プロパティ群（先頭に空行を入れて既存の `}` と分離）
$insertion = @"

    /// <summary>AttackStance または AttackMotion 中なら true。AI が攻撃中判定に使う。</summary>
    public bool IsAttacking
    {
        get
        {
            if (m_animator == null) return false;
            int hash = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return hash == s_hAttackStanceState || hash == s_hAttackMotionState;
        }
    }

    /// <summary>AttackMotion 中（振りかぶり?振り抜き）なら true。AI が腕Hitbox期待中の判定に使う。</summary>
    public bool IsInAttackMotion
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackMotionState;
        }
    }

    /// <summary>AttackStun 中なら true。Bool ではなく現在 State を見ることで AnimEvent 配線漏れに対しても堅牢。</summary>
    public bool IsStunned
    {
        get
        {
            if (m_animator == null) return false;
            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == s_hAttackStunState;
        }
    }

    // Animator State 名のハッシュ（State 名は EnemyBossA_Animator.controller の Layer0 上の State 名と一致する必要がある）
    private static readonly int s_hAttackStanceState = Animator.StringToHash("AttackStance");
    private static readonly int s_hAttackMotionState = Animator.StringToHash("AttackMotion");
    private static readonly int s_hAttackStunState   = Animator.StringToHash("AttackStun");
"@.Replace("`r`n", "`n")

if ($useCrLf) {
    $insertion = $insertion.Replace("`n", "`r`n")
}

$replacement = $anchorMatch + $insertion
$newContent = $content.Replace($anchorMatch, $replacement)

[System.IO.File]::WriteAllText($path, $newContent, $enc)
Write-Host "OK: inserted (useCrLf=$useCrLf)"
