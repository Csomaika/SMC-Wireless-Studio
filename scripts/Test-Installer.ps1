param([Parameter(Mandatory)][string]$OldMsi,[Parameter(Mandatory)][string]$NewMsi,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$log=Join-Path $root 'artifacts/installer-tests'
New-Item -ItemType Directory -Force $log | Out-Null
function Invoke-Msi([string]$verb,[string]$file,[string]$name){
 $p=Start-Process msiexec.exe -ArgumentList "$verb `"$file`" /qn /norestart /L*v `"$log/$name.log`"" -Wait -PassThru
 if($p.ExitCode -notin @(0,3010)){throw "MSI $name failed: $($p.ExitCode)"}
}
function Query-Msi([string]$path,[string]$query){
 $installer=New-Object -ComObject WindowsInstaller.Installer
 $db=$installer.OpenDatabase((Resolve-Path $path).Path,0)
 $view=$db.OpenView($query);[void]$view.Execute();$record=$view.Fetch()
 if($null -eq $record){throw "MSI query found no row: $query"}
 return [string]$record.StringData(1)
}
$label=Query-Msi $NewMsi 'SELECT `Text` FROM `Control` WHERE `Dialog_` = ''StudioWelcome'' AND `Control` = ''Update'''
if($label -ne 'Update'){throw 'Installer Update button missing'}
$oldUpgrade=Query-Msi $OldMsi 'SELECT `Value` FROM `Property` WHERE `Property` = ''UpgradeCode'''
$newUpgrade=Query-Msi $NewMsi 'SELECT `Value` FROM `Property` WHERE `Property` = ''UpgradeCode'''
Write-Host "Upgrade identities: old=[$oldUpgrade] new=[$newUpgrade]"
if($oldUpgrade -ne $newUpgrade){throw 'Upgrade identity changed'}
$data=Join-Path $env:LOCALAPPDATA 'SMC Wireless Studio/Projects'
New-Item -ItemType Directory -Force $data | Out-Null
$sentinel=Join-Path $data 'upgrade-preservation.json'
[IO.File]::WriteAllText($sentinel,'{"name":"Megfogó – őrzött projekt","cycleMs":20}')
$hash=(Get-FileHash $sentinel).Hash
Invoke-Msi '/i' $OldMsi 'install-old'
$exe=Join-Path $env:ProgramFiles 'SMC Wireless Studio/SMCWirelessStudio.exe'
if(-not(Test-Path $exe)){throw 'Installed EXE missing'}
Invoke-Msi '/i' $NewMsi 'upgrade-new'
if((Get-FileHash $sentinel).Hash -ne $hash){throw 'Update changed user data'}
$fileVersion=(Get-Item $exe).VersionInfo.FileVersion
if(-not $fileVersion.StartsWith("$Version.")){throw "Wrong installed EXE version: $fileVersion"}
$env:SMC_SMOKE_DIR=Join-Path $root 'artifacts/installed-smoke'
$run=Start-Process $exe -ArgumentList '--smoke-test' -PassThru
if(-not $run.WaitForExit(60000)){$run.Kill();throw 'Installed app smoke test timed out'}
if($run.ExitCode -ne 0){throw "Installed app smoke test failed: $($run.ExitCode)"}
$downgrade=Start-Process msiexec.exe -ArgumentList "/i `"$OldMsi`" /qn /norestart /L*v `"$log/downgrade.log`"" -Wait -PassThru
if($downgrade.ExitCode -eq 0){throw 'Downgrade was not blocked'}
Invoke-Msi '/x' $NewMsi 'uninstall'
if((Get-FileHash $sentinel).Hash -ne $hash){throw 'Uninstall removed user data'}
if(Test-Path $exe){throw 'Uninstall did not remove the EXE'}
[IO.File]::WriteAllText((Join-Path $log 'result.txt'),'PASS: Update button present; stable upgrade identity; old install; upgrade; data preserved; installed app smoke; downgrade blocked; uninstall preserves data.')
Write-Host 'All MSI lifecycle tests passed.'
