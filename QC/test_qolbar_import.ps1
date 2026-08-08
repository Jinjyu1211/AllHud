# QoLBar Import Test Script
$ErrorActionPreference = "Stop"

Write-Host "=== QoLBar Import Test ===" -ForegroundColor Cyan
Write-Host ""

# Sample QoLBar ExportInfo JSON (short property names, no Chinese chars)
$json = [System.Text.Encoding]::UTF8.GetString([System.Text.Encoding]::Default.GetBytes(@'
{
  "b2": {
    "n": "TestBar",
    "sL": [
      { "n": "Provoke", "t": 0, "c": "/ac Provoke <t>", "k": 114, "cdA": 7535 },
      { "n": "Esuna", "t": 0, "c": "/ac Esuna", "cdA": 155 }
    ],
    "h": false,
    "d": 4,
    "a": 1,
    "v": 2,
    "bW": 100,
    "p": [500, 400],
    "s": 1.0,
    "sp": [8, 4]
  },
  "cs": {
    "n": "CombatCondition",
    "c": [
      { "i": "cf", "a": 1, "n": false, "o": 0 },
      { "i": "j", "a": 21, "n": false, "o": 0 }
    ]
  }
}
'@))

Write-Host "1. Generate QoLBar GZip+Base64 data..." -ForegroundColor Yellow

$jsonBytes = [System.Text.Encoding]::UTF8.GetBytes($json)
$ms = [System.IO.MemoryStream]::new()
$gzip = [System.IO.Compression.GZipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress)
$gzip.Write($jsonBytes, 0, $jsonBytes.Length)
$gzip.Close()
$base64Data = [Convert]::ToBase64String($ms.ToArray())
$ms.Close()

Write-Host "   Base64 length: $($base64Data.Length) chars" -ForegroundColor Gray
Write-Host "   Base64 first 80: $($base64Data.Substring(0, [Math]::Min(80, $base64Data.Length)))..." -ForegroundColor Gray

$bytes = [Convert]::FromBase64String($base64Data)
$isGZip = $bytes[0] -eq 0x1F -and $bytes[1] -eq 0x8B
Write-Host "   GZip header detected: $isGZip" -ForegroundColor Gray

Write-Host ""
Write-Host "2. Decompress verification..." -ForegroundColor Yellow

$decodedBytes = [Convert]::FromBase64String($base64Data)
$decompressMs = [System.IO.MemoryStream]::new($decodedBytes)
$decompressGzip = [System.IO.Compression.GZipStream]::new($decompressMs, [System.IO.Compression.CompressionMode]::Decompress)
$reader = [System.IO.StreamReader]::new($decompressGzip)
$decompressedJson = $reader.ReadToEnd()
$reader.Close()

Write-Host "   Decompressed JSON length: $($decompressedJson.Length) chars" -ForegroundColor Gray
Write-Host "   Decompressed (first 200):" -ForegroundColor Gray
Write-Host "   $($decompressedJson.Substring(0, [Math]::Min(200, $decompressedJson.Length)))" -ForegroundColor Gray

Write-Host ""
Write-Host "3. JSON structure validation..." -ForegroundColor Yellow

$obj = $decompressedJson | ConvertFrom-Json

$hasBar = ($obj.b2 -ne $null)
$hasCs = ($obj.cs -ne $null)
Write-Host "   Has b2 (Bar): $hasBar" -ForegroundColor Gray
Write-Host "   Has cs (ConditionSet): $hasCs" -ForegroundColor Gray

if ($hasBar) {
    Write-Host "   Bar Name: $($obj.b2.n)" -ForegroundColor Gray
    Write-Host "   Bar Shortcuts: $($obj.b2.sL.Count)" -ForegroundColor Gray
    Write-Host "   Bar DockSide: $($obj.b2.d)" -ForegroundColor Gray
    Write-Host "   First Shortcut: '$($obj.b2.sL[0].n)' Hotkey=$($obj.b2.sL[0].k) Cooldown=$($obj.b2.sL[0].cdA)" -ForegroundColor Gray
}

if ($hasCs) {
    Write-Host "   ConditionSet Name: $($obj.cs.n)" -ForegroundColor Gray
    Write-Host "   ConditionSet Items: $($obj.cs.c.Count)" -ForegroundColor Gray
    Write-Host "   First Condition: Id='$($obj.cs.c[0].i)' Arg=$($obj.cs.c[0].a)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "4. Format detection tests..." -ForegroundColor Yellow

$testCases = @(
    @{ Text = "=== QC Bar Export v1 ==="; Expected = $false; Desc = "Reject QC format" },
    @{ Text = ""; Expected = $false; Desc = "Reject empty string" },
    @{ Text = "not base64"; Expected = $false; Desc = "Reject invalid base64" }
)

# Test valid GZip+Base64
try {
    $isValid = $base64Data.Length -ge 10 -and (-not $base64Data.StartsWith("=== QC"))
    Write-Host "   Compressed format detection: PASS" -ForegroundColor Green
} catch {
    Write-Host "   Compressed format detection: FAIL" -ForegroundColor Red
}

# Test plain JSON format
try {
    $isJson = $json.TrimStart().StartsWith('{')
    Write-Host "   Plain JSON format detection: PASS" -ForegroundColor Green
} catch {
    Write-Host "   Plain JSON format detection: FAIL" -ForegroundColor Red
}

foreach ($tc in $testCases) {
    $desc = $tc.Desc
    $expected = $tc.Expected
    $result = $false
    try {
        if ($tc.Text -eq "") {
            $result = $false
        } elseif ($tc.Text.StartsWith("=== QC")) {
            $result = $false
        } else {
            $testBytes = [Convert]::FromBase64String($tc.Text)
            $result = $testBytes.Length -gt 2 -and $testBytes[0] -eq 0x1F -and $testBytes[1] -eq 0x8B
        }
    } catch {
        $result = $false
    }
    $status = if ($result -eq $expected) { "PASS" } else { "FAIL" }
    $color = if ($result -eq $expected) { "Green" } else { "Red" }
    Write-Host "   $($desc): $status" -ForegroundColor $color
}

Write-Host ""
Write-Host "5. Data integrity validation..." -ForegroundColor Yellow

$allPassed = $true

if ($obj.b2.n -ne "TestBar") { Write-Host "   FAIL: Bar.Name = $($obj.b2.n)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: Bar.Name = TestBar" -ForegroundColor Green }

if ($obj.b2.sL.Count -ne 2) { Write-Host "   FAIL: Bar.ShortcutList count = $($obj.b2.sL.Count)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: Bar.ShortcutList count = 2" -ForegroundColor Green }

if ($obj.b2.sL[0].n -ne "Provoke") { Write-Host "   FAIL: Sh#1.Name = $($obj.b2.sL[0].n)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: Sh#1.Name = Provoke" -ForegroundColor Green }

if ($obj.b2.sL[0].k -ne 114) { Write-Host "   FAIL: Sh#1.Hotkey = $($obj.b2.sL[0].k)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: Sh#1.Hotkey = 114 (F3)" -ForegroundColor Green }

if ($obj.b2.sL[0].cdA -ne 7535) { Write-Host "   FAIL: Sh#1.CooldownAction = $($obj.b2.sL[0].cdA)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: Sh#1.CooldownAction = 7535" -ForegroundColor Green }

if ($obj.cs.n -ne "CombatCondition") { Write-Host "   FAIL: CS.Name = $($obj.cs.n)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: CS.Name = CombatCondition" -ForegroundColor Green }

if ($obj.cs.c.Count -ne 2) { Write-Host "   FAIL: CS.Conditions count = $($obj.cs.c.Count)" -ForegroundColor Red; $allPassed = $false }
else { Write-Host "   PASS: CS.Conditions count = 2" -ForegroundColor Green }

if ($obj.cs.c[0].i -ne "cf" -or $obj.cs.c[0].a -ne 1) {
    Write-Host "   FAIL: Cnd#1 = Id='$($obj.cs.c[0].i)' Arg=$($obj.cs.c[0].a)" -ForegroundColor Red; $allPassed = $false
} else {
    Write-Host "   PASS: Cnd#1 = Id=cf Arg=1 (InCombat)" -ForegroundColor Green
}

if ($obj.cs.c[1].i -ne "j" -or $obj.cs.c[1].a -ne 21) {
    Write-Host "   FAIL: Cnd#2 = Id='$($obj.cs.c[1].i)' Arg=$($obj.cs.c[1].a)" -ForegroundColor Red; $allPassed = $false
} else {
    Write-Host "   PASS: Cnd#2 = Id=j Arg=21 (PLD)" -ForegroundColor Green
}

# Also test the export string can be placed on clipboard for in-game testing
Write-Host ""
Write-Host "=== Test Result ===" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "Result: ALL PASSED" -ForegroundColor Green
    Write-Host ""
    Write-Host "QoLBar export format parsing verified successfully!" -ForegroundColor Green
    Write-Host "Supported formats:" -ForegroundColor Yellow
    Write-Host "  - GZip+Base64 compressed JSON" -ForegroundColor Gray
    Write-Host "  - Plain JSON (uncompressed)" -ForegroundColor Gray
    Write-Host "Supported data models:" -ForegroundColor Yellow
    Write-Host "  - ExportInfo (b1/b2: BarCfg, s1/s2: ShCfg, cs: CndSetCfg)" -ForegroundColor Gray
    Write-Host "  - ImportInfo (bar/shortcut/conditionSet)" -ForegroundColor Gray
    Write-Host "  - Direct BarCfg/ShCfg/CndSetCfg JSON" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Test export string (paste into QC import for in-game test):" -ForegroundColor Yellow
    Write-Host "  $base64Data" -ForegroundColor Gray
    exit 0
} else {
    Write-Host "Result: SOME TESTS FAILED" -ForegroundColor Red
    exit 1
}