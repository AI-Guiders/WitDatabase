# Reorganize WitDatabase solution structure using Git commands
# This script uses 'git mv' to preserve file history

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot

Write-Host "Starting solution reorganization with Git..." -ForegroundColor Cyan
Write-Host "Root directory: $rootDir" -ForegroundColor Gray

# Check if we're in a git repository
$gitStatus = git status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Not in a git repository!" -ForegroundColor Red
    exit 1
}

# Step 1: Create new folder structure
Write-Host "`nStep 1: Creating folder structure..." -ForegroundColor Yellow

$folders = @(
    "Sources/Core",
    "Sources/Engine",
    "Sources/Providers",
    "Samples",
    "Benchmarks",
    "Docs"
)

foreach ($folder in $folders) {
    $fullPath = Join-Path $rootDir $folder
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "  Created: $folder" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $folder" -ForegroundColor Gray
    }
}

# Step 2: Move projects using git mv
Write-Host "`nStep 2: Moving projects with git mv..." -ForegroundColor Yellow

$moves = @{
    # Core projects
    "OutWit.Database.Core" = "Sources/Core"
    "OutWit.Database.Core.Tests" = "Sources/Core"
    "OutWit.Database.Core.BouncyCastle" = "Sources/Core"
    "OutWit.Database.Core.IndexedDb" = "Sources/Core"
    "OutWit.Database.Core.IndexedDb.Tests" = "Sources/Core"
    
    # Engine projects
    "OutWit.Database.Parser" = "Sources/Engine"
    "OutWit.Database.Parser.Tests" = "Sources/Engine"
    "OutWit.Database" = "Sources/Engine"
    "OutWit.Database.Tests" = "Sources/Engine"
    
    # Samples
    "OutWit.Database.Samples.BlazorWasm" = "Samples"
    
    # Benchmarks
    "OutWit.Database.Core.Tests.Benchmarks" = "Benchmarks"
}

foreach ($project in $moves.Keys) {
    $source = $project
    $destFolder = $moves[$project]
    $dest = "$destFolder/$project"
    
    if (Test-Path $source) {
        if (Test-Path $dest) {
            Write-Host "  Skip (already moved): $project" -ForegroundColor Gray
        } else {
            Write-Host "  Moving: $project -> $destFolder" -ForegroundColor Cyan
            git mv $source $dest 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Moved: $project" -ForegroundColor Green
            } else {
                Write-Host "  WARNING: Failed to move $project" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  Not found: $project" -ForegroundColor Yellow
    }
}

# Step 3: Move documentation using git mv
Write-Host "`nStep 3: Moving documentation..." -ForegroundColor Yellow

$docFiles = @(
    "CODE_STYLE_GUIDE.md",
    "COMPLETION_PLAN.md", 
    "PROJECT_STATUS.md",
    "WitSQL.md",
    "AUDIT-WitDatabase-Core.md"
)

$DocsSource = "Docs"

if (Test-Path $DocsSource) {
    # Get actual files (handle wildcards)
    $actualFiles = Get-ChildItem -Path $DocsSource -Filter "*.md"
    
    foreach ($file in $actualFiles) {
        $destFile = "Docs/$($file.Name)"
        if (-not (Test-Path $destFile)) {
            Write-Host "  Moving: $($file.Name)" -ForegroundColor Cyan
            git mv "$DocsSource/$($file.Name)" $destFile 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Moved: $($file.Name)" -ForegroundColor Green
            }
        }
    }
    
    # Check if Docs folder is empty
    $remaining = Get-ChildItem -Path $DocsSource -ErrorAction SilentlyContinue
    if ($remaining.Count -eq 0) {
        Remove-Item -Path $DocsSource -Force
        Write-Host "  Removed empty Docs folder" -ForegroundColor Gray
    }
}

# Step 4: Update .slnx file
Write-Host "`nStep 4: Updating solution file..." -ForegroundColor Yellow

$slnxPath = "OutWit.slnx"
if (Test-Path $slnxPath) {
    $newSlnx = @"
<Solution>
  <Folder Name="/@Docs/">
    <File Path="Directory.Build.props" />
    <File Path="Docs/CODE_STYLE_GUIDE.md" />
    <File Path="Docs/COMPLETION_PLAN.md" />
    <File Path="Docs/PROJECT_STATUS.md" />
    <File Path="Docs/AUDIT-WitDatabase-Core.md" />
    <File Path="Docs/WitSQL.md" />
    <File Path="README.md" />
  </Folder>
  <Folder Name="/Sources/Core/">
    <Project Path="Sources/Core/OutWit.Database.Core/OutWit.Database.Core.csproj" Id="91042611-a01d-4293-9594-25ee9d3a0a67" />
    <Project Path="Sources/Core/OutWit.Database.Core.Tests/OutWit.Database.Core.Tests.csproj" Id="91a232f7-c5e4-4083-8127-20070e0d8b03" />
    <Project Path="Sources/Core/OutWit.Database.Core.BouncyCastle/OutWit.Database.Core.BouncyCastle.csproj" Id="e244e45c-4d29-4c06-9bbb-68f33d803880" />
    <Project Path="Sources/Core/OutWit.Database.Core.IndexedDb/OutWit.Database.Core.IndexedDb.csproj" Id="0B08187A-589F-4B18-9840-F4F3940F9DA8" />
    <Project Path="Sources/Core/OutWit.Database.Core.IndexedDb.Tests/OutWit.Database.Core.IndexedDb.Tests.csproj" Id="6CB5FC26-DE05-488F-90AC-A6B7AA4955BC" />
  </Folder>
  <Folder Name="/Sources/Engine/">
    <Project Path="Sources/Engine/OutWit.Database.Parser/OutWit.Database.Parser.csproj" Id="5d480879-e75e-4d6a-bd07-09be725177a5" />
    <Project Path="Sources/Engine/OutWit.Database.Parser.Tests/OutWit.Database.Parser.Tests.csproj" Id="198cc914-dec7-4660-b8f2-e8069cd68a17" />
    <Project Path="Sources/Engine/OutWit.Database/OutWit.Database.csproj" Id="e9257641-e6c2-4a4b-b263-1b72246a5a72" />
    <Project Path="Sources/Engine/OutWit.Database.Tests/OutWit.Database.Tests.csproj" Id="300c3af8-a89b-4507-b444-167d9b0d947c" />
  </Folder>
  <Folder Name="/Samples/">
    <Project Path="Samples/OutWit.Database.Samples.BlazorWasm/OutWit.Database.Samples.BlazorWasm.csproj" />
  </Folder>
  <Folder Name="/Benchmarks/">
    <Project Path="Benchmarks/OutWit.Database.Core.Tests.Benchmarks/OutWit.Database.Core.Tests.Benchmarks.csproj" Id="218fe20c-91ee-4980-98ce-332906c1db1a" />
  </Folder>
</Solution>
"@
    
    Set-Content -Path $slnxPath -Value $newSlnx -Encoding UTF8
    git add $slnxPath
    Write-Host "  Updated: OutWit.slnx" -ForegroundColor Green
}

# Step 5: Update project references
Write-Host "`nStep 5: Updating project references..." -ForegroundColor Yellow

$csprojFiles = Get-ChildItem -Path $rootDir -Filter "*.csproj" -Recurse

foreach ($csproj in $csprojFiles) {
    $content = Get-Content $csproj.FullName -Raw
    $originalContent = $content
    
    # Fix references from Core projects to other Core projects (same folder)
    if ($csproj.FullName -like "*\Sources\Core\*") {
        $content = $content -replace '<ProjectReference Include="\.\.\\(OutWit\.Database\.Core[^\\]*?)\\', '<ProjectReference Include="..\$1\'
    }
    
    # Fix references from Engine projects to Core projects (up two levels)
    if ($csproj.FullName -like "*\Sources\Engine\*") {
        $content = $content -replace '<ProjectReference Include="\.\.\\(OutWit\.Database\.Core[^\\]*?)\\', '<ProjectReference Include="..\..\Core\$1\'
    }
    
    # Fix references from Engine projects to other Engine projects (same folder)
    if ($csproj.FullName -like "*\Sources\Engine\*") {
        $content = $content -replace '<ProjectReference Include="\.\.\\(OutWit\.Database(?!\.Core)[^\\]*?)\\', '<ProjectReference Include="..\$1\'
    }
    
    # Fix references from Samples to Engine (up one, then into Sources)
    if ($csproj.FullName -like "*\Samples\*") {
        $content = $content -replace '<ProjectReference Include="\.\.\\(OutWit\.Database[^\\]*?)\\', '<ProjectReference Include="..\Sources\Engine\$1\'
        $content = $content -replace '<ProjectReference Include="\.\.\\(OutWit\.Database\.Core[^\\]*?)\\', '<ProjectReference Include="..\Sources\Core\$1\'
    }
    
    # Fix references from Benchmarks
    if ($csproj.FullName -like "*\Benchmarks\*") {
        $content = $content -replace '<ProjectReference Include="\.\.\\(OutWit\.Database\.Core[^\\]*?)\\', '<ProjectReference Include="..\Sources\Core\$1\'
    }
    
    if ($content -ne $originalContent) {
        Set-Content -Path $csproj.FullName -Value $content -Encoding UTF8
        git add $csproj.FullName
        Write-Host "  Updated: $($csproj.Name)" -ForegroundColor Green
    }
}

Write-Host "`nDone! Solution reorganized successfully." -ForegroundColor Green
Write-Host "`nGit status:" -ForegroundColor Cyan
git status --short

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Review changes: git status" -ForegroundColor White
Write-Host "  2. Close and reopen Visual Studio" -ForegroundColor White  
Write-Host "  3. Run 'dotnet build' to verify compilation" -ForegroundColor White
Write-Host "  4. Commit changes: git commit -m 'Reorganize solution structure'" -ForegroundColor White
