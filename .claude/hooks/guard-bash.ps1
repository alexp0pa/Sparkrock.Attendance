# PreToolUse hook (Bash/PowerShell): block destructive commands. Exit code 2 rejects the call
# and shows the reason to Claude.
$hookInput = [Console]::In.ReadToEnd() | ConvertFrom-Json
$command = [string]$hookInput.tool_input.command

$blocked = @(
    @{ Pattern = 'git\s+push\s+.*(--force|-f\b)'; Reason = 'force-push rewrites shared history' },
    @{ Pattern = 'git\s+reset\s+--hard';            Reason = 'hard reset discards uncommitted work' },
    @{ Pattern = 'git\s+clean\s+-\w*f';             Reason = 'git clean deletes untracked files' },
    @{ Pattern = 'ef\s+database\s+drop';            Reason = 'dropping the database destroys data' }
)

foreach ($rule in $blocked) {
    if ($command -match $rule.Pattern) {
        [Console]::Error.WriteLine("Blocked by .claude/hooks/guard-bash.ps1: $($rule.Reason). Ask the user to run it themselves if it is really intended.")
        exit 2
    }
}

exit 0
