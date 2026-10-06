param(
  [string]$ProjectRoot = 'D:/Projetos Unity/Fabula/Games/Paw-Order'
)

$ErrorActionPreference = 'Stop'
$Suspects = @('Greta','Vera','Milo','Nina','Otto','Boris')
$OutcomesDir = Join-Path $ProjectRoot 'Assets/Project/Scriptables/Investigations/PawOrder/Outcomes'
$QuestionsJsonPath = Join-Path $ProjectRoot 'Assets/Project/Scriptables/Investigations/PawOrder/paworder_import_source.json'
$CharactersDir = Join-Path $ProjectRoot 'Assets/Project/Scriptables/Investigations/PawOrder/Characters'
$PreviewPath = Join-Path $OutcomesDir 'PawOrder_OutcomeEvidencePreview.csv'

function Get-CharacterGuids {
  $map = @{}
  Get-ChildItem -File $CharactersDir -Filter '*.asset' | ForEach-Object {
    $meta = "$($_.FullName).meta"
    if (Test-Path $meta) {
      $guidLine = Get-Content $meta | Where-Object { $_ -match '^guid:\s*' } | Select-Object -First 1
      if ($guidLine) {
        $guid = ($guidLine -replace '^guid:\s*','').Trim()
        $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        $map[$name] = $guid
      }
    }
  }
  return $map
}

function HasAny([string]$text, [string[]]$terms) {
  foreach ($t in $terms) { if ($text.Contains($t)) { return $true } }
  return $false
}

function ClampRound([double]$v) {
  $allowed = @(0,10,20,25,30,40,50,60,75,80,90,100)
  $nearest = $allowed | Sort-Object { [Math]::Abs($_ - $v) } | Select-Object -First 1
  return [int]$nearest
}

function Score-Suspect([string]$responseLower, [string]$suspect, [string]$target, [string]$question) {
  $m = 0; $me = 0; $o = 0

  $motWeak = @('motivo','perder','conflito','segredo','cobrir','encobrir','historia','história','ciume','ciúme','rival','pressao','pressão')
  $motStrong = @('matar','assassinato','silenciar','vinganca','vingança','ameaca','ameaça','chantagem')
  $meansWeak = @('acesso','entrar','camarim','rotina','procedimento','batom','lenco','lenço','frasco','ajuste','alterado')
  $meansStrong = @('veneno','manipular','sabotar','raspada')
  $oppWeak = @('hora','antes','depois','sozinho','circulou','frequentou','voltaria','visivel','visível')
  $oppStrong = @('na hora critica','na hora crítica','segunda entrada','duas vezes','acesso limpo')

  $mentionsSuspect = $responseLower.Contains($suspect.ToLower())
  if ($mentionsSuspect) { $m += 20; $me += 20; $o += 20 }

  if ($suspect -eq $target) {
    $o += 10
  }

  if (HasAny $responseLower $motWeak) { $m += 10 }
  if (HasAny $responseLower $motStrong) { $m += 20 }
  if (HasAny $responseLower $meansWeak) { $me += 10 }
  if (HasAny $responseLower $meansStrong) { $me += 20 }
  if (HasAny $responseLower $oppWeak) { $o += 10 }
  if (HasAny $responseLower $oppStrong) { $o += 20 }

  $q = $question.ToLower()
  if ($q.Contains('motivo') -or $q.Contains('perder')) { $m += 10 }
  if ($q.Contains('acesso') -or $q.Contains('entrar') -or $q.Contains('procedimento')) { $me += 10; $o += 10 }
  if ($q.Contains('hora') -or $q.Contains('sozinho') -or $q.Contains('circulou')) { $o += 10 }

  if (-not $mentionsSuspect) {
    $m = [Math]::Min($m, 30)
    $me = [Math]::Min($me, 30)
    $o = [Math]::Min($o, 30)
  }

  $m = ClampRound([Math]::Max(0,[Math]::Min(100,$m)))
  $me = ClampRound([Math]::Max(0,[Math]::Min(100,$me)))
  $o = ClampRound([Math]::Max(0,[Math]::Min(100,$o)))
  $score = [int][Math]::Round((($m + $me + $o) / 3.0),0,[MidpointRounding]::AwayFromZero)

  $rationale = if ($mentionsSuspect) { 'citado diretamente na resposta; indício moderado conservador' } else { 'sem citação direta; apenas indício contextual baixo' }

  return [PSCustomObject]@{ motivationScore=$m; meansScore=$me; opportunityScore=$o; score=$score; rationale=$rationale }
}

$questionsDoc = Get-Content -Raw $QuestionsJsonPath | ConvertFrom-Json
$qById = @{}
foreach ($q in $questionsDoc.questions) { $qById[$q.id] = $q.question }

$characterGuids = Get-CharacterGuids
foreach ($s in $Suspects) { if (-not $characterGuids.ContainsKey($s)) { throw "GUID não encontrado para suspeito: $s" } }

$previewRows = New-Object System.Collections.Generic.List[object]
$outcomeFiles = Get-ChildItem -Recurse -File $OutcomesDir -Filter 'Outcome_*.asset'
$updatedCount = 0
$alreadyHadValues = 0

foreach ($file in $outcomeFiles) {
  $text = Get-Content -Raw $file.FullName
  $outcomeId = ([regex]::Match($text, '(?m)^\s*outcomeId:\s*(.+)$')).Groups[1].Value.Trim()
  $response = ([regex]::Match($text, '(?m)^\s*responseText:\s*(.+)$')).Groups[1].Value.Trim()
  $m = [regex]::Match($outcomeId, '^Outcome_(Q\d+)_([A-Za-z]+)$')
  if (-not $m.Success) { continue }
  $qid = $m.Groups[1].Value
  $target = $m.Groups[2].Value
  $question = if ($qById.ContainsKey($qid)) { [string]$qById[$qid] } else { $qid }

  $hasExisting = -not ($text -match '(?m)^\s*suspectEvidenceValues:\s*\[\]\s*$')
  if ($hasExisting) { $alreadyHadValues++ }

  $entries = New-Object System.Collections.Generic.List[string]
  foreach ($suspect in $Suspects) {
    $sc = Score-Suspect -responseLower $response.ToLower() -suspect $suspect -target $target -question $question
    $previewRows.Add([PSCustomObject]@{ outcomeId=$outcomeId; question=$question; targetCharacter=$target; suspect=$suspect; motivationScore=$sc.motivationScore; meansScore=$sc.meansScore; opportunityScore=$sc.opportunityScore; score=$sc.score; rationale=$sc.rationale })
    $guid = $characterGuids[$suspect]
    $entries.Add("  - suspect: {fileID: 11400000, guid: $guid, type: 2}`n    score: $($sc.score)`n    motivationScore: $($sc.motivationScore)`n    meansScore: $($sc.meansScore)`n    opportunityScore: $($sc.opportunityScore)")
  }

  $block = "suspectEvidenceValues:`n" + ($entries -join "`n")
  if ($text -match '(?ms)^\s*suspectEvidenceValues:\s*\[\]\s*$') {
    $newText = [regex]::Replace($text, '(?ms)^\s*suspectEvidenceValues:\s*\[\]\s*$', $block)
  } else {
    $newText = [regex]::Replace($text, '(?ms)^\s*suspectEvidenceValues:\s*.*$', $block)
  }

  if ($newText -ne $text) {
    Set-Content -Path $file.FullName -Value $newText -NoNewline -Encoding UTF8
    $updatedCount++
  }
}

$previewRows | Export-Csv -Path $PreviewPath -NoTypeInformation -Encoding UTF8
Write-Output "OUTCOMES_TOTAL=$($outcomeFiles.Count)"
Write-Output "OUTCOMES_UPDATED=$updatedCount"
Write-Output "OUTCOMES_WITH_EXISTING_VALUES=$alreadyHadValues"
Write-Output "PREVIEW_PATH=$PreviewPath"
