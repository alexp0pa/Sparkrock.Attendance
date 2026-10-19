# Stop hook: before Claude hands control back, the solution must build and the fast test suites
# (unit + architecture) must pass. Integration tests are left to explicit `dotnet test` runs.
# Exit code 2 feeds the failure back to Claude so it fixes the problem first.
$hookInput = [Console]::In.ReadToEnd() | ConvertFrom-Json

# Hooks run from the project root; walk up to the solution in case the session cd'ed deeper.
$root = (Get-Location).Path
while ($root -and -not (Get-ChildItem -Path $root -Filter *.sln -ErrorAction SilentlyContinue)) {
    $root = Split-Path $root -Parent
}
if (-not $root) { exit 0 }
Set-Location $root

# Skip when no C#/project files changed since the last green run (keeps analysis-only turns fast).
$stateFile = Join-Path $root '.claude/.quality-gate-state'
$fingerprint = (Get-ChildItem -Path $root -Recurse -File -Include *.cs, *.csproj, *.props, *.sln |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Sort-Object FullName |
    ForEach-Object { "$($_.FullName)|$($_.LastWriteTimeUtc.Ticks)|$($_.Length)" }) -join "`n"
$hash = [BitConverter]::ToString(
    [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($fingerprint)))

if ((Test-Path $stateFile) -and (Get-Content $stateFile -Raw).Trim() -eq $hash) { exit 0 }

# Avoid an endless loop: if Claude is already continuing because of this hook, report but don't block again.
$blockExitCode = if ($hookInput.stop_hook_active) { 0 } else { 2 }

$build = dotnet build --nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) {
    $errors = $build | Select-String -Pattern ': error ' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique | Select-Object -First 30
    [Console]::Error.WriteLine("Quality gate: build failed. Fix these before finishing:`n" + ($errors -join "`n"))
    exit $blockExitCode
}

$testProjects = Get-ChildItem -Path (Join-Path $root 'tests') -Filter *.csproj -Recurse |
    Where-Object { $_.BaseName -notmatch 'Integration' }

foreach ($project in $testProjects) {
    $test = dotnet test $project.FullName --no-build --nologo -v q --logger 'console;verbosity=normal' 2>&1
    if ($LASTEXITCODE -ne 0) {
        # Forward the test runner's failure summary (test name, assertion message, user-code stack frames).
        $lines = @($test | ForEach-Object { "$_".TrimEnd() })
        $start = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^\s*Failed \S+' })
        if ($start -lt 0) { $start = 0 }
        $failures = $lines[$start..($lines.Count - 1)] |
            Where-Object { $_ -and $_ -notmatch '^\s+at System\.|^\s*(Total tests|Passed|Failed|Total time):|^Test Run Failed' } |
            Select-Object -First 60
        [Console]::Error.WriteLine("Quality gate: $($project.BaseName) failed. Fix before finishing:`n" + ($failures -join "`n"))
        exit $blockExitCode
    }
}

Set-Content -Path $stateFile -Value $hash
exit 0
