$path = "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Enemy/EnemyBossA/EnemyBossBaseA_Animator.cs"
$enc = [System.Text.Encoding]::Default
$content = [System.IO.File]::ReadAllText($path, $enc)

# `SetIsStunned(false);\n    }\n    /// <summary>AttackStance` を
# `SetIsStunned(false);\n    }\n\n    /// <summary>AttackStance` に置換（空行を1行追加）
$old = "        SetIsStunned(false);`n    }`n    /// <summary>AttackStance"
$new = "        SetIsStunned(false);`n    }`n`n    /// <summary>AttackStance"

if (-not $content.Contains($old)) {
    Write-Error "old pattern not found"
    exit 1
}

$newContent = $content.Replace([string]$old, [string]$new)
[System.IO.File]::WriteAllText($path, $newContent, $enc)
Write-Host "OK: blank line inserted"
