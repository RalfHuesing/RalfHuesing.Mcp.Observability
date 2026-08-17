<#
.SYNOPSIS
    Automates the release workflow for RalfHuesing.Mcp.Observability.

.DESCRIPTION
    Validates git state, builds and runs all tests, updates the .csproj version,
    commits the bump, creates a git tag (e.g. v1.0.1), and pushes to origin.
    This triggers the GitHub Actions workflow to build, test, pack, and publish to NuGet.org.

.PARAMETER Version
    The target release version (e.g., '1.0.1' or 'v1.0.1').

.PARAMETER Message
    Optional release message or description.

.PARAMETER DryRun
    If specified, performs all builds and tests but skips pushing git commits and tags.

.EXAMPLE
    ./scripts/create-release.ps1 -Version 1.0.1
    ./scripts/create-release.ps1 -Version 1.0.1 -DryRun
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, Position = 0, HelpMessage = "The release version (e.g., 1.0.1)")]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Message,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Write-Step ([string]$text) {
    Write-Host "`n=== $text ===" -ForegroundColor Cyan
}

function Write-Success ([string]$text) {
    Write-Host "  [OK] $text" -ForegroundColor Green
}

function Write-Warn ([string]$text) {
    Write-Host "  [WARN] $text" -ForegroundColor Yellow
}

function Write-Err ([string]$text) {
    Write-Host "  [ERROR] $text" -ForegroundColor Red
}

# 1. Normalize and Validate Version
$cleanVersion = $Version.TrimStart('v', 'V').Trim()
if ($cleanVersion -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') {
    Write-Err "Invalid semantic version: '$Version'. Expected format like '1.0.1' or '1.1.0-preview.1'."
    exit 1
}
$tagName = "v$cleanVersion"

Write-Step "Starting Release Flow for $tagName"
if ($DryRun) {
    Write-Warn "DRY RUN MODE: No git push will be executed."
}

# 2. Check Repository Root & Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
Set-Location $repoRoot

$csprojPath = Join-Path $repoRoot "src/RalfHuesing.Mcp.Observability/RalfHuesing.Mcp.Observability.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Err "Could not find csproj at $csprojPath"
    exit 1
}

# 3. Check Git Status
Write-Step "Checking Git status"
$currentBranch = ((git --no-pager branch --show-current) | Out-String).Trim()
if ($currentBranch -ne "main") {
    Write-Warn "You are currently on branch '$currentBranch' (recommended: 'main')."
}

$uncommitted = ((git --no-pager status --porcelain) | Out-String).Trim()
if ($uncommitted) {
    if ($DryRun) {
        Write-Warn "Working directory has uncommitted changes (ignored in DryRun):`n$uncommitted"
    } else {
        Write-Err "Working directory has uncommitted changes. Please commit or stash before releasing.`n$uncommitted"
        exit 1
    }
}

# Check if tag already exists
$existingTag = ((git --no-pager tag -l $tagName) | Out-String).Trim()
if ($existingTag) {
    if ($DryRun) {
        Write-Warn "Git tag '$tagName' already exists locally (ignored in DryRun)."
    } else {
        Write-Err "Git tag '$tagName' already exists locally."
        exit 1
    }
}

Write-Success "Git state is clean"

# 4. Build Solution
Write-Step "Building solution (Release mode)"
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Err "dotnet build failed. Aborting release."
    exit 1
}
Write-Success "Build succeeded with 0 errors"

# 5. Run Tests
Write-Step "Running test suite"
dotnet test --configuration Release --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Err "dotnet test failed. Aborting release."
    exit 1
}
Write-Success "All tests passed"

# 6. Verify Packaging
Write-Step "Testing NuGet package creation"
dotnet pack $csprojPath --configuration Release -p:PackageVersion=$cleanVersion -o (Join-Path $repoRoot "artifacts-dryrun")
if ($LASTEXITCODE -ne 0) {
    Write-Err "dotnet pack failed. Aborting release."
    exit 1
}
Remove-Item -Recurse -Force (Join-Path $repoRoot "artifacts-dryrun") -ErrorAction SilentlyContinue
Write-Success "Package creation verified"

# 7. Check for DryRun Exit
if ($DryRun) {
    Write-Step "DryRun completed successfully"
    Write-Host "`nDryRun summary:"
    Write-Host "  - Target Tag: $tagName"
    Write-Host "  - Version:    $cleanVersion"
    Write-Host "  - Tests:      Passed (Release mode)"
    Write-Host "  - Build:      Passed (Release mode)"
    Write-Host "  - Packaging:  Passed ($cleanVersion.nupkg / .snupkg)"
    Write-Host "`nTo execute the actual release, re-run without -DryRun:"
    Write-Host "  ./scripts/create-release.ps1 -Version $cleanVersion" -ForegroundColor Yellow
    exit 0
}

# 8. Update .csproj Version if necessary
Write-Step "Updating .csproj version to $cleanVersion"
[xml]$csprojXml = Get-Content $csprojPath -Raw
$versionNode = $csprojXml.SelectSingleNode("//PropertyGroup/Version")
if ($versionNode -ne $null) {
    $versionNode.InnerText = $cleanVersion
    $csprojXml.Save($csprojPath)
    Write-Success "Updated <Version> in $csprojPath"
} else {
    Write-Warn "No <Version> node found in csproj; using project defaults."
}

# 9. Git Commit & Tag
Write-Step "Committing version bump and creating tag $tagName"
$hasChanges = ((git --no-pager status --porcelain) | Out-String).Trim()
if ($hasChanges) {
    git add $csprojPath
    git commit -m "chore(release): bump version to $cleanVersion"
    Write-Success "Committed version bump"
}

$releaseNote = if ($Message) { $Message } else { "Release $tagName" }
git tag -a $tagName -m "$releaseNote"
Write-Success "Created git tag $tagName"

# 9. Push to GitHub
Write-Step "Pushing commit and tag to origin"
git push origin $currentBranch
git push origin $tagName
Write-Success "Pushed to origin ($tagName)"

# 10. Summary & Links
Write-Step "Release initiated successfully!"
Write-Host @"

  Tag:          $tagName
  Version:      $cleanVersion
  GitHub Run:   https://github.com/RalfHuesing/RalfHuesing.Mcp.Observability/actions
  Releases:     https://github.com/RalfHuesing/RalfHuesing.Mcp.Observability/releases
  NuGet.org:    https://www.nuget.org/packages/RalfHuesing.Mcp.Observability/$cleanVersion

The GitHub Actions workflow 'Build & Publish' is now running to deploy your package to NuGet.org.
"@ -ForegroundColor Green
