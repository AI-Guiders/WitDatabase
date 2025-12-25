# Parameters passed from the MSBuild target
Param(
    [string]$TargetDir
)

# Fix for MSBuild trailing slash issue: 
# Sometimes the path passed from MSBuild includes an escaped quote at the end.
# This ensures we have a clean path string.
$TargetDir = $TargetDir.Trim('"')

# Validate that the directory exists before proceeding
if (-not (Test-Path $TargetDir)) {
    Write-Host "Skipping: Directory not found -> $TargetDir"
    exit 0
}

Write-Host "Searching for ANTLR generated files in: $TargetDir"

# Find generated C# files in the target directory (specifically in the 'obj' folder).
# We filter by name pattern (Lexer, Parser, Listener, Visitor) to strictly avoid modifying any user code.
$files = Get-ChildItem -Path $TargetDir -Recurse -Filter "*.cs" | 
         Where-Object { $_.Name -match '(Lexer|Parser|Listener|Visitor)\.cs$' }

foreach ($file in $files) {
    # Read the entire file content as a single raw string.
    # Using -Raw is crucial to prevent PowerShell from altering line endings or duplicating content.
    $content = Get-Content $file.FullName -Raw

    # Check if the file contains 'public' modifiers before attempting any replacement to save I/O operations.
    if ($content -match 'public partial class' -or $content -match 'public interface') {
        
        # Replace 'public' with 'internal' for both partial classes and interfaces.
        $newContent = $content -replace 'public partial class', 'internal partial class' `
                               -replace 'public interface', 'internal interface'
        
        # Write the modified content back to the file.
        # -NoNewline prevents adding an extra empty line at the end of the file on every build.
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        
        Write-Host " -> Made internal: $($file.Name)"
    }
}