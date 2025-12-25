# Reorganize WitDatabase solution structure
# This script moves projects into a cleaner folder structure

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot

Write-Host "Starting solution reorganization..." -ForegroundColor Cyan
Write-Host "Root directory: $rootDir" -ForegroundColor Gray

# Step 1: Create new folder structure
Write-Host "`nStep 1: Creating folder structure..." -ForegroundColor Yellow

$folders = @(
    "src/core",
    "src/engine",
    "src/providers",
    "samples",
    "benchmarks",
    "docs"
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

# Step 2: Move projects
Write-Host "`nStep 2: Moving projects..." -ForegroundColor Yellow

$moves = @{
    # Core projects
    "OutWit.Database.Core" = "src/core"
    "OutWit.Database.Core.Tests" = "src/core"
    "OutWit.Database.Core.BouncyCastle" = "src/core"
    "OutWit.Database.Core.IndexedDb" = "src/core"
    "OutWit.Database.Core.IndexedDb.Tests" = "src/core"
    
    # Engine projects
    "OutWit.Database.Parser" = "src/engine"
    "OutWit.Database.Parser.Tests" = "src/engine"
    "OutWit.Database" = "src/engine"
    "OutWit.Database.Tests" = "src/engine"
    
    # Samples
    "OutWit.Database.Samples.BlazorWasm" = "samples"
    
    # Benchmarks
    "OutWit.Database.Core.Tests.Benchmarks" = "benchmarks"
}

foreach ($project in $moves.Keys) {
    $source = Join-Path $rootDir $project
    $destFolder = Join-Path $rootDir $moves[$project]
    $dest = Join-Path $destFolder $project
    
    if (Test-Path $source) {
        if (Test-Path $dest) {
            Write-Host "  Skip (already moved): $project" -ForegroundColor Gray
        } else {
            Write-Host "  Moving: $project -> $($moves[$project])" -ForegroundColor Cyan
            Move-Item -Path $source -Destination $dest -Force
            Write-Host "  Moved: $project" -ForegroundColor Green
        }
    } else {
        Write-Host "  Not found: $project" -ForegroundColor Yellow
    }
}

# Step 3: Move documentation
Write-Host "`nStep 3: Moving documentation..." -ForegroundColor Yellow

$docFiles = @(
    "CODE_STYLE_GUIDE.md",
    "COMPLETION_PLAN.md",
    "PROJECT_STATUS.md",
    "WitSQL.md",
    "Roadmap.*.md"
)

$docsSource = Join-Path $rootDir "Docs"
$docsDest = Join-Path $rootDir "docs"

if (Test-Path $docsSource) {
    # Move markdown files from Docs to docs
    foreach ($pattern in $docFiles) {
        $files = Get-ChildItem -Path $docsSource -Filter $pattern -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            $destFile = Join-Path $docsDest $file.Name
            if (-not (Test-Path $destFile)) {
                Move-Item -Path $file.FullName -Destination $destFile -Force
                Write-Host "  Moved: $($file.Name)" -ForegroundColor Green
            }
        }
    }
    
    # Also move AUDIT file
    $auditFile = Join-Path $docsSource "AUDIT-WitDatabase-Core.md"
    if (Test-Path $auditFile) {
        Move-Item -Path $auditFile -Destination (Join-Path $docsDest "AUDIT-WitDatabase-Core.md") -Force
        Write-Host "  Moved: AUDIT-WitDatabase-Core.md" -ForegroundColor Green
    }
    
    # Remove old Docs folder if empty
    $remaining = Get-ChildItem -Path $docsSource -ErrorAction SilentlyContinue
    if ($remaining.Count -eq 0) {
        Remove-Item -Path $docsSource -Force
        Write-Host "  Removed empty Docs folder" -ForegroundColor Gray
    }
}

# Step 4: Update .slnx file
Write-Host "`nStep 4: Updating solution file..." -ForegroundColor Yellow

$slnxPath = Join-Path $rootDir "OutWit.slnx"
if (Test-Path $slnxPath) {
    $slnxContent = Get-Content $slnxPath -Raw
    
    # Update project paths
    $slnxContent = $slnxContent -replace 'Path="OutWit\.Database\.Core/', 'Path="src/core/OutWit.Database.Core/'
    $slnxContent = $slnxContent -replace 'Path="OutWit\.Database\.Parser/', 'Path="src/engine/OutWit.Database.Parser/'
    $slnxContent = $slnxContent -replace 'Path="OutWit\.Database\.Tests/', 'Path="src/engine/OutWit.Database.Tests/'
    $slnxContent = $slnxContent -replace 'Path="OutWit\.Database/', 'Path="src/engine/OutWit.Database/'
    $slnxContent = $slnxContent -replace 'Path="OutWit\.Database\.Samples\.BlazorWasm/', 'Path="samples/OutWit.Database.Samples.BlazorWasm/'
    
    # Update folder structure
    $newSlnx = @"
<Solution>
  <Folder Name="/@docs/">
    <File Path="Directory.Build.props" />
    <File Path="docs/CODE_STYLE_GUIDE.md" />
    <File Path="docs/COMPLETION_PLAN.md" />
    <File Path="docs/PROJECT_STATUS.md" />
    <File Path="docs/AUDIT-WitDatabase-Core.md" />
    <File Path="README.md" />
    <File Path="docs/WitSQL.md" />
  </Folder>
  <Folder Name="/src/core/">
    <Project Path="src/core/OutWit.Database.Core/OutWit.Database.Core.csproj" Id="91042611-a01d-4293-9594-25ee9d3a0a67" />
    <Project Path="src/core/OutWit.Database.Core.Tests/OutWit.Database.Core.Tests.csproj" Id="91a232f7-c5e4-4083-8127-20070e0d8b03" />
    <Project Path="src/core/OutWit.Database.Core.BouncyCastle/OutWit.Database.Core.BouncyCastle.csproj" Id="e244e45c-4d29-4c06-9bbb-68f33d803880" />
    <Project Path="src/core/OutWit.Database.Core.IndexedDb/OutWit.Database.Core.IndexedDb.csproj" Id="0B08187A-589F-4B18-9840-F4F3940F9DA8" />
    <Project Path="src/core/OutWit.Database.Core.IndexedDb.Tests/OutWit.Database.Core.IndexedDb.Tests.csproj" Id="6CB5FC26-DE05-488F-90AC-A6B7AA4955BC" />
  </Folder>
  <Folder Name="/src/engine/">
    <Project Path="src/engine/OutWit.Database.Parser/OutWit.Database.Parser.csproj" Id="5d480879-e75e-4d6a-bd07-09be725177a5" />
    <Project Path="src/engine/OutWit.Database.Parser.Tests/OutWit.Database.Parser.Tests.csproj" Id="198cc914-dec7-4660-b8f2-e8069cd68a17" />
    <Project Path="src/engine/OutWit.Database/OutWit.Database.csproj" Id="e9257641-e6c2-4a4b-b263-1b72246a5a72" />
    <Project Path="src/engine/OutWit.Database.Tests/OutWit.Database.Tests.csproj" Id="300c3af8-a89b-4507-b444-167d9b0d947c" />
  </Folder>
  <Folder Name="/samples/">
    <Project Path="samples/OutWit.Database.Samples.BlazorWasm/OutWit.Database.Samples.BlazorWasm.csproj" />
  </Folder>
  <Folder Name="/benchmarks/">
    <Project Path="benchmarks/OutWit.Database.Core.Tests.Benchmarks/OutWit.Database.Core.Tests.Benchmarks.csproj" Id="218fe20c-91ee-4980-98ce-332906c1db1a" />
  </Folder>
</Solution>
"@
    
    Set-Content -Path $slnxPath -Value $newSlnx -Encoding UTF8
    Write-Host "  Updated: OutWit.slnx" -ForegroundColor Green
}

# Step 5: Update project references
Write-Host "`nStep 5: Checking project references..." -ForegroundColor Yellow

$csprojFiles = Get-ChildItem -Path $rootDir -Filter "*.csproj" -Recurse

foreach ($csproj in $csprojFiles) {
    $content = Get-Content $csproj.FullName -Raw
    $updated = $false
    
    # Update ProjectReference paths
    if ($content -match '<ProjectReference Include="\.\.\\') {
        # Core references
        $content = $content -replace '<ProjectReference Include="\.\.\\OutWit\.Database\.Core\\', '<ProjectReference Include="..\..\OutWit.Database.Core\'
        $content = $content -replace '<ProjectReference Include="\.\.\\OutWit\.Database\.Core\.BouncyCastle\\', '<ProjectReference Include="..\OutWit.Database.Core.BouncyCastle\'
        $content = $content -replace '<ProjectReference Include="\.\.\\OutWit\.Database\.Core\.IndexedDb\\', '<ProjectReference Include="..\OutWit.Database.Core.IndexedDb\'
        
        # Parser references
        $content = $content -replace '<ProjectReference Include="\.\.\\OutWit\.Database\.Parser\\', '<ProjectReference Include="..\OutWit.Database.Parser\'
        
        $updated = $true
    }
    
    if ($updated) {
        Set-Content -Path $csproj.FullName -Value $content -Encoding UTF8
        Write-Host "  Updated: $($csproj.Name)" -ForegroundColor Green
    }
}

Write-Host "`nDone! Solution reorganized successfully." -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Close and reopen Visual Studio" -ForegroundColor White
Write-Host "  2. Verify all projects load correctly" -ForegroundColor White
Write-Host "  3. Run 'dotnet build' to verify compilation" -ForegroundColor White
Write-Host "  4. Review changes with 'git status'" -ForegroundColor White
