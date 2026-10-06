$ErrorActionPreference = 'Stop'
$root = 'Assets/Project/Scriptables/Investigations/PawOrder'
$targets = Get-ChildItem -Recurse -File $root -Filter '*.asset'

# Campos textuais YAML mais comuns nesses ScriptableObjects
$fieldPattern = '^(\s*)(responseText|proofSummary|questionText|displayName|description|name):\s*(.*)$'

$changedFiles = 0
$changedLines = 0

function Fix-Mojibake([string]$s) {
  if ([string]::IsNullOrEmpty($s)) { return $s }
  $fixed = [Text.Encoding]::UTF8.GetString([Text.Encoding]::GetEncoding(1252).GetBytes($s))
  return $fixed
}

foreach ($f in $targets) {
  $lines = Get-Content $f.FullName
  $fileChanged = $false

  for ($i=0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $m = [regex]::Match($line, $fieldPattern)
    if (-not $m.Success) { continue }

    $indent = $m.Groups[1].Value
    $field = $m.Groups[2].Value
    $value = $m.Groups[3].Value

    # Só tenta corrigir quando há sinais claros de mojibake
    if ($value -match 'Ã|Â|â\x80|â\x81|â\x82|â\x83|â\x84|â\x85|â\x86|â\x87|â\x88|â\x89|â\x8A|â\x8B|â\x8C|â\x8D|â\x8E|â\x8F|â\x90|â\x91|â\x92|â\x93|â\x94|â\x95|â\x96|â\x97|â\x98|â\x99|â\x9A|â\x9B|â\x9C|â\x9D|â\x9E|â\x9F') {
      $fixed = Fix-Mojibake $value
      if ($fixed -ne $value) {
        $lines[$i] = "${indent}${field}: $fixed"
        $fileChanged = $true
        $changedLines++
      }
    }
  }

  if ($fileChanged) {
    Set-Content -Path $f.FullName -Value $lines -Encoding UTF8
    $changedFiles++
  }
}

Write-Output "FILES_CHANGED=$changedFiles"
Write-Output "LINES_CHANGED=$changedLines"

