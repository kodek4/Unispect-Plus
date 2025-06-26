param(
    [Parameter(Mandatory=$true)]
    [string]$ProcessName
)

# Unispect CLI Workflow Test Script
# This script tests all features in a realistic workflow

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$logDir = "TestResults/logs/$timestamp"
$logFile = "$logDir/workflow-test.log"

# Create log directory
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

function Write-Log {
    param($Message, $Color = "White")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage -ForegroundColor $Color
    Add-Content $logFile $logMessage
}

# Helper to invoke CLI and abort on non-zero exit codes
function Invoke-CLI {
    # Uses automatic $Args array to collect all passed tokens
    & $cliPath @Args | Where-Object { $_ -notlike 'DEBUG:*' }
    if ($LASTEXITCODE -ne 0) {
        throw "CLI command failed: $($Args -join ' ') (exit $LASTEXITCODE)"
    }
}

Write-Log "🔍 Starting Unispect CLI Workflow Test" -Color Cyan
Write-Log "======================================" -Color Cyan
Write-Log "Testing with process: $ProcessName" -Color Yellow

# Find the CLI executable in standard locations
$cliSearchPaths = @(
    "../Unispect.CLI/bin/Debug/net9.0-windows/unispect-cli.exe",
    "../Unispect.CLI/bin/Release/net9.0-windows/unispect-cli.exe",
    "unispect-cli.exe" # Check in current working directory
)

$cliPath = $null
foreach ($path in $cliSearchPaths) {
    if (Test-Path $path) {
        $cliPath = $path
        break
    }
}

# Verify CLI executable exists and provide helpful error
if (-not $cliPath) {
    Write-Log "❌ CLI executable not found in any of the standard locations:" -Color Red
    $cliSearchPaths | ForEach-Object { Write-Log "  - $_" -Color Red }
    Write-Log "Please build the project first with: dotnet build" -Color Yellow
    exit 1
}

Write-Log "✅ Found CLI executable at: $cliPath" -Color Green

# Verify process exists
$process = Get-Process $ProcessName -ErrorAction SilentlyContinue
if (-not $process) {
    Write-Log "❌ Process '$ProcessName' not found!" -Color Red
    exit 1
}

try {
    # 0. Ensure a clean slate by removing any existing cache
    Write-Log "`n0. Clearing existing cache (if any)..." -Color Yellow
    Invoke-CLI cache clear --process $ProcessName

    # 1. Initial dump of a process
    Write-Log "`n1. Dumping process memory..." -Color Yellow
    Invoke-CLI dump --process $ProcessName --output "$logDir/dump1.txt" --format text --refresh

    # 2. Query specific types (case-insensitive wildcard)
    Write-Log "`n2. Querying specific types..." -Color Yellow
    Write-Log "[COMMAND] query *" -Color Cyan
    Invoke-CLI query --process $ProcessName --query '*'

    # Offset/type-only formats require a specific type; pick the first returned type from wildcard search via CLI json output
    # Silent helper search to fetch any type name without polluting console output
    $rawLines = & $cliPath search --process $ProcessName --pattern '*' --type types --limit 1 2>&1
    $rawFirst = ($rawLines | Where-Object { $_ -match '🏷️' } | Select-Object -First 1)
    $firstType = ($rawFirst -replace '^\s*🏷️\s*','') -replace '\s*\(.*',''
    if ($firstType -and $firstType.Length -gt 1) {
        Write-Log "[COMMAND] query ""$($firstType).*"" --format offset-only" -Color Cyan
        Invoke-CLI query --process $ProcessName --query "$firstType.*" --format offset-only

        Write-Log "[COMMAND] query ""$($firstType).*"" --format type-only" -Color Cyan
        Invoke-CLI query --process $ProcessName --query "$firstType.*" --format type-only
    } else {
        Write-Log "⚠️  Could not resolve first type name for offset/type-only query" -Color Yellow
    }

    # 3. Search for patterns
    Write-Log "`n3. Searching for specific patterns..." -Color Yellow
    Write-Log '[COMMAND] search *Manager --type fields --limit 25' -Color Cyan
    Invoke-CLI search --process $ProcessName --pattern '*Manager' --type fields --limit 25

    Write-Log '[COMMAND] search "^.{6,12}$" --regex --limit 5' -Color Cyan
    Invoke-CLI search --process $ProcessName --pattern '^.{6,12}$' --regex --limit 5

    Write-Log '[COMMAND] search * --offset-range 0x20-0x40 --limit 10' -Color Cyan
    Invoke-CLI search --process $ProcessName --pattern '*' --offset-range '0x20-0x40' --limit 10

    # 4. Generate statistics
    Write-Log "`n4. Generating statistics..." -Color Yellow
    Invoke-CLI stats --process $ProcessName --output "$logDir/stats.json" --format json --detailed
    # quick sanity check on TotalTypes
    $statsObj = Get-Content "$logDir/stats.json" | ConvertFrom-Json
    $tt = [int]$statsObj.Overview.TotalTypes
    if ($tt -lt 100) { Write-Log "⚠️  Very low type count ($tt)" -Color Yellow } else { Write-Log "    • TotalTypes (expanded) = $tt" -Color Cyan }
    # also run text format (stdout only)
    Invoke-CLI stats --process $ProcessName --format text

    # 5. Export in different formats
    Write-Log "`n5. Testing different export formats..." -Color Yellow
    Invoke-CLI dump --process $ProcessName --output "$logDir/dump.json" --format json
    Invoke-CLI dump --process $ProcessName --output "$logDir/dump.utd" --format utd
    Invoke-CLI dump --process $ProcessName --output "$logDir/dump.cs" --format csharp-intptr
    Invoke-CLI dump --process $ProcessName --output "$logDir/dump_ulong.cs" --format csharp-ulong

    # 6. Cache operations
    Write-Log "`n6. Testing cache operations..." -Color Yellow
    Invoke-CLI cache list
    Invoke-CLI cache info --process $ProcessName

    # 7. Comparison test (clone cache under a synthetic process name)
    Write-Log "`n7. Testing comparison functionality..." -Color Yellow

    $cacheDir = Join-Path $env:LOCALAPPDATA "Unispect/Cache"
    $origCache = Join-Path $cacheDir ("$($ProcessName.ToLower())_assembly-csharp.utd")
    $syntheticProcess = "${ProcessName}_modified"
    $modifiedCache = Join-Path $cacheDir ("$($syntheticProcess.ToLower())_assembly-csharp.utd")

    if (Test-Path $origCache) {
        Copy-Item $origCache $modifiedCache -Force
        Invoke-CLI compare --process1 $ProcessName --process2 $syntheticProcess --format json --output "$logDir/diff.json"
        $diffObj = Get-Content "$logDir/diff.json" | ConvertFrom-Json
        if ($diffObj.ModifiedTypes.Count -gt 0 -or $diffObj.OnlyInFirst.Count -gt 0 -or $diffObj.OnlyInSecond.Count -gt 0) {
            Write-Log "⚠️  Unexpected differences detected in identical cache compare" -Color Yellow
        }
    } else {
        Write-Log "⚠️ Cache file not found at $origCache, skipping comparison test" -Color Yellow
    }

    # 8. Validation
    Write-Log "`n8. Validating cache integrity..." -Color Yellow
    Invoke-CLI validate --process $ProcessName
    # run fixer path (should no-op)
    Invoke-CLI validate --process $ProcessName --fix

    # Check for output files
    Write-Log "`n9. Verifying outputs..." -Color Yellow
    $expectedFiles = @(
        "$logDir/dump1.txt",
        "$logDir/stats.json",
        "$logDir/dump.json",
        "$logDir/dump.utd",
        "$logDir/dump.cs",
        "$logDir/dump_ulong.cs",
        "$logDir/diff.json"
    )

    foreach ($file in $expectedFiles) {
        if (Test-Path $file) {
            Write-Log "✅ Found $file" -Color Green
        } else {
            Write-Log "❌ Missing $file" -Color Red
        }
    }

    Write-Log "`n✨ Workflow test complete!" -Color Cyan
    Write-Log "Log file: $logFile" -Color Cyan
} catch {
    Write-Log "❌ Test failed: $_" -Color Red
    exit 1
} 