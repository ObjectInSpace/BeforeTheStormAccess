# Builds distributable zips of Before the Storm Access, one per platform:
#   dist\BeforeTheStormAccess-v{version}-windows.zip  (tested)
#   dist\BeforeTheStormAccess-v{version}-macos.zip    (untested: no Mac to test on)
#   dist\BeforeTheStormAccess-v{version}-linux.zip    (untested)
# Each extracts into the game folder: BepInEx\plugins holds the mod DLL, the
# translations and the platform's Prism speech library. Windows takes the
# loader (winhttp/doorstop/BepInEx core) from the working install; macOS and
# Linux start from the official BepInEx release for that platform, pinned by
# SHA-256. Run scripts\Get-Prism.ps1 first.
# Usage: .\scripts\Package-Mod.ps1  (from the project root or anywhere)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path $PSScriptRoot -Parent
$gameDir = "D:\Root\Life is Strange - Before the Storm"
$versionLine = Select-String -Path (Join-Path $projectDir "Main.cs") -Pattern 'Version = "([^"]+)"'
$version = $versionLine.Matches[0].Groups[1].Value
$nativeDir = Join-Path $projectDir "native"
$bepinexVersion = "5.4.23.5"
$bepinexZips = @{
    macos = @{ Name = "BepInEx_macos_universal_$bepinexVersion.zip"; Sha = "01C2AE782EB016DFD6C345A18DBD2DCAFFFB3D9D318449D6486689F426B4A323" }
    linux = @{ Name = "BepInEx_linux_x64_$bepinexVersion.zip"; Sha = "E538560BE65739F562519AB518A75F9C65B3F57F87457403AE7CDE683C12DAB7" }
}
$prismLibs = @{
    windows = "win-x64\prism.dll"
    macos = "macos\libprism.dylib"
    linux = "linux-x64\libprism.so"
}

Write-Host "Packaging Before the Storm Access v$version"

foreach ($lib in $prismLibs.Values) {
    if (-not (Test-Path (Join-Path $nativeDir $lib))) { throw "native\$lib missing: run scripts\Get-Prism.ps1 first." }
}

& (Join-Path $PSScriptRoot "Check-Translations.ps1")
if ($LASTEXITCODE -ne 0) { throw "Translation check failed" }

# Fresh build without deploying to the game folder.
dotnet build (Join-Path $projectDir "BeforeTheStormAccess.csproj") -c Release -p:SkipCopyToPlugins=true
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

New-Item -ItemType Directory -Force (Join-Path $projectDir "dist") | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-BepInExRelease($platform) {
    $info = $bepinexZips[$platform]
    $cacheDir = Join-Path ([IO.Path]::GetTempPath()) "bepinex-$bepinexVersion"
    New-Item -ItemType Directory -Force $cacheDir | Out-Null
    $zipPath = Join-Path $cacheDir $info.Name
    if (-not (Test-Path $zipPath)) {
        $url = "https://github.com/BepInEx/BepInEx/releases/download/v$bepinexVersion/$($info.Name)"
        Write-Host "Downloading $url"
        Invoke-WebRequest -Uri $url -OutFile $zipPath
    }
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
    if ($hash -ne $info.Sha) {
        Remove-Item $zipPath
        throw "$($info.Name): SHA-256 $hash does not match the pinned $($info.Sha)."
    }
    return $zipPath
}

foreach ($platform in @("windows", "macos", "linux")) {
    $staging = Join-Path $env:TEMP "BtSAccess-package-$platform"
    $zipPath = Join-Path $projectDir ("dist\BeforeTheStormAccess-v" + $version + "-" + $platform + ".zip")
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -Confirm:$false }
    $plugins = Join-Path $staging "BepInEx\plugins"
    New-Item -ItemType Directory -Force $plugins | Out-Null

    # Loader
    if ($platform -eq "windows") {
        New-Item -ItemType Directory -Force (Join-Path $staging "BepInEx\core") | Out-Null
        Copy-Item (Join-Path $gameDir "BepInEx\core\*") (Join-Path $staging "BepInEx\core\")
        Copy-Item (Join-Path $gameDir "winhttp.dll") $staging
        Copy-Item (Join-Path $gameDir "doorstop_config.ini") $staging
        Copy-Item (Join-Path $gameDir ".doorstop_version") $staging -ErrorAction SilentlyContinue
    } else {
        [IO.Compression.ZipFile]::ExtractToDirectory((Get-BepInExRelease $platform), $staging)
        Remove-Item (Join-Path $staging "changelog.txt") -ErrorAction SilentlyContinue
    }

    # Mod, translations, speech library
    Copy-Item (Join-Path $projectDir "bin\Release\net35\BeforeTheStormAccess.dll") $plugins
    Copy-Item (Join-Path $projectDir "Translations\*.txt") $plugins
    Copy-Item (Join-Path $nativeDir $prismLibs[$platform]) $plugins

    # Docs and licenses
    Copy-Item (Join-Path $projectDir "README.md") (Join-Path $staging "BeforeTheStormAccess-README.txt")
    Copy-Item (Join-Path $projectDir "THIRD_PARTY_NOTICES.txt") $staging
    Copy-Item (Join-Path $nativeDir "PRISM_NOTICE.txt") $staging
    Copy-Item (Join-Path $nativeDir "PRISM_LICENSES") $staging -Recurse

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force -Confirm:$false }
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath
    Write-Host "Package written to $zipPath"
}
