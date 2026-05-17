$ErrorActionPreference = "Stop"
$path = "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Enemy/EnemyBossA/EnemyBossBaseA_Animator.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::Default)
$content = $content.Replace('StringToHash("AttackStance")', 'StringToHash("AttackStanceAnim")')
$content = $content.Replace('StringToHash("AttackMotion")', 'StringToHash("AttackMotionAnim")')
$content = $content.Replace('StringToHash("AttackStun")', 'StringToHash("AttackStunAnim")')
[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::Default)
Write-Host "Done"
