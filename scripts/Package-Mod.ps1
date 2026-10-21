# Builds a distributable zip of Before the Storm Access.
# Layout matches "extract into game folder": BepInEx\plugins\<dll>, plus the
# loader (winhttp/doorstop/BepInEx core) and Tolk DLLs at the root.
# Usage: .\scripts\Package-Mod.ps1  (from the project root or anywhere)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path $PSScriptRoot -Parent
$gameDir = "D:\Root\Life is Strange - Before the Storm"
$staging = Join-Path $env:TEMP "BtSAccess-package"
$versionLine = Select-String -Path (Join-Path $projectDir "Main.cs") -Pattern 'Version = "([^"]+)"'
$version = $versionLine.Matches[0].Groups[1].Value
$zipPath = Join-Path $projectDir ("dist\BeforeTheStormAccess-v" + $version + ".zip")

Write-Host "Packaging Before the Storm Access v$version"

# Fresh build without deploying to the game folder.
dotnet build (Join-Path $projectDir "BeforeTheStormAccess.csproj") -c Release -p:SkipCopyToPlugins=true
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force (Join-Path $staging "BepInEx\plugins") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $staging "BepInEx\core") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $projectDir "dist") | Out-Null

# Mod DLL
Copy-Item (Join-Path $projectDir "bin\Release\net35\BeforeTheStormAccess.dll") (Join-Path $staging "BepInEx\plugins\")

# Translations
$translationsDir = Join-Path $projectDir "Translations"
if (Test-Path $translationsDir) {
    Copy-Item (Join-Path $translationsDir "*.txt") (Join-Path $staging "BepInEx\plugins\")
}

# Loader: BepInEx core + doorstop from the working install
Copy-Item (Join-Path $gameDir "BepInEx\core\*") (Join-Path $staging "BepInEx\core\")
Copy-Item (Join-Path $gameDir "winhttp.dll") $staging
Copy-Item (Join-Path $gameDir "doorstop_config.ini") $staging
Copy-Item (Join-Path $gameDir ".doorstop_version") $staging -ErrorAction SilentlyContinue

# Speech: Tolk + NVDA client
Copy-Item (Join-Path $gameDir "Tolk.dll") $staging
Copy-Item (Join-Path $gameDir "nvdaControllerClient64.dll") $staging

# Docs
Copy-Item (Join-Path $projectDir "README.md") (Join-Path $staging "BeforeTheStormAccess-README.txt")
Copy-Item (Join-Path $projectDir "THIRD_PARTY_NOTICES.txt") $staging

if (Test-Path $zipPath) { Remove-Item $zipPath -Force -Confirm:$false }
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath

Write-Host "Package written to $zipPath"
