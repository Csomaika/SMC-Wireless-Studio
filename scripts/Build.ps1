param([string]$Version=(Get-Content "$PSScriptRoot/../version.txt" -Raw).Trim(),[string]$Output="artifacts",[switch]$SkipPublish)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Version must be major.minor.build'}
$publish=Join-Path $root 'artifacts/publish'
$destination=[IO.Path]::GetFullPath((Join-Path $root $Output))
New-Item -ItemType Directory -Force $destination | Out-Null
if(-not $SkipPublish){
 if(Test-Path $publish){Remove-Item $publish -Recurse -Force}
 dotnet publish src/Studio.Desktop/Studio.Desktop.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o $publish
 if($LASTEXITCODE -ne 0){throw 'Desktop publish failed'}
 Copy-Item LICENSE "$publish/LICENSE.txt"
 Copy-Item src/Studio.Desktop/Assets/NOTICE.md "$publish/PRODUCT-IMAGES.txt"
}
if(-not(Test-Path "$root/.tools/wix.exe")){
 dotnet tool install wix --version 5.0.2 --tool-path "$root/.tools"
 if($LASTEXITCODE -ne 0){throw 'WiX installation failed'}
}
$msi=Join-Path $destination "SMC-Wireless-Studio-$Version-x64.msi"
& "$root/.tools/wix.exe" build installer/Package.wxs -arch x64 -d "Version=$Version" -d "PublishDir=$publish" -d "Assets=$root/src/Studio.Desktop/Assets" -o $msi
if($LASTEXITCODE -ne 0){throw 'MSI build failed'}
$sha=(Get-FileHash $msi -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$msi.sha256","$sha  $([IO.Path]::GetFileName($msi))`n")
Write-Host "Built $msi"
