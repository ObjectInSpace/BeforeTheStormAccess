# Downloads the pinned Prism release (https://github.com/ethindp/prism, MPL-2.0)
# and extracts the speech library for each platform into native\:
#   native\win-x64\prism.dll, native\macos\libprism.dylib, native\linux-x64\libprism.so
# plus Prism's NOTICE and license. Each download is checked against a pinned
# SHA-256 so a changed release can't slip in. Re-run after bumping $version.
$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $PSScriptRoot
$version = "v0.18.3"
$assets = @(
    @{ Zip = "prism-windows-x64.zip"; Sha = "13D3C9D1D524B0737CDE261C6468845753E36B6C5C2637E7D26BFB8BF4D8783F";
       Entry = "dynamic/release/bin/prism.dll"; Dest = "win-x64\prism.dll" },
    @{ Zip = "prism-macos-universal.zip"; Sha = "1F698ECB305D29CDB8D4329B072F7B6BCD06729A162575F8451ED3D80DB60903";
       Entry = "dynamic/release/lib/libprism.dylib"; Dest = "macos\libprism.dylib" },
    @{ Zip = "prism-linux-x64.zip"; Sha = "2B3050C89EAA4FB6C452377EC155EC7B71F444B424B547E04DA238575D98A31C";
       Entry = "dynamic/release/lib/libprism.so"; Dest = "linux-x64\libprism.so" }
)

$nativeDir = Join-Path $projectDir "native"
$downloadDir = Join-Path ([IO.Path]::GetTempPath()) ("prism-" + $version)
New-Item -ItemType Directory -Force $downloadDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($asset in $assets) {
    $zipPath = Join-Path $downloadDir $asset.Zip
    if (-not (Test-Path $zipPath)) {
        $url = "https://github.com/ethindp/prism/releases/download/$version/$($asset.Zip)"
        Write-Host "Downloading $url"
        Invoke-WebRequest -Uri $url -OutFile $zipPath
    }

    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
    if ($hash -ne $asset.Sha) {
        Remove-Item $zipPath
        throw "$($asset.Zip): SHA-256 $hash does not match the pinned $($asset.Sha)."
    }

    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $dest = Join-Path $nativeDir $asset.Dest
        New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($asset.Entry), $dest, $true)
        if ($asset.Zip -eq "prism-windows-x64.zip") {
            # NOTICE points at LICENSES\ for the bundled components' license texts.
            [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry("NOTICE"), (Join-Path $nativeDir "PRISM_NOTICE.txt"), $true)
            foreach ($entry in $zip.Entries) {
                if ($entry.FullName -notlike "LICENSES/*" -or $entry.Length -eq 0) { continue }
                $licenseDest = Join-Path $nativeDir ("PRISM_" + $entry.FullName)
                New-Item -ItemType Directory -Force (Split-Path $licenseDest) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $licenseDest, $true)
            }
        }
    } finally {
        $zip.Dispose()
    }
    Write-Host "Extracted $($asset.Dest)"
}

Set-Content (Join-Path $nativeDir "PRISM_VERSION.txt") $version
Write-Host "Prism $version ready in $nativeDir"
