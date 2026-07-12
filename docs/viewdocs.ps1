# Powershell script - view documents
#
# Amendments
# 09-Jul-2026   Initial version (Copilot)
param(
    [string]$Port = "8080"
)

# Common DocFX output folders
$Candidates = @(
    "_site",
    "site",
    "publish/_site",
    "publish/ref",
    "publish",
    "docs/_site",
    "docs/site"
)

# Scan for DocFX output
$SiteDir = $null

foreach ($c in $Candidates) {
    if (Test-Path $c) {
        $SiteDir = $c
        break
    }
}

if (-not $SiteDir) {
    Write-Host "ERROR: No DocFX output folder found."
    Write-Host "Checked:"
    $Candidates | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "Serving documentation from '$SiteDir' on port $Port..."

# Build the command safely
$cmd = @(
    "-NoExit",
    "-Command",
    "http-server '$SiteDir' -p $Port"
)

Start-Process powershell -ArgumentList $cmd

# Open browser
$url = "http://localhost:$Port"
Start-Process $url
