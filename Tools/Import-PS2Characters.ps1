param([ValidateSet('Athra','All','Enemies')][string]$Characters='Athra')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$workspaceRoot=Split-Path $projectRoot -Parent
$project=Join-Path $workspaceRoot '.validation/UnityProject'
$log=Join-Path $projectRoot 'Validation/PS2Import.log'
$method=if($Characters -eq 'Enemies'){'PS2EnemyInstaller.InstallAll'}elseif($Characters -eq 'All'){'PS2CharacterInstaller.InstallAll'}else{'PS2CharacterInstaller.InstallAthra'}
$process=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$process.WaitForExit()
Select-String -Path $log -Pattern 'error CS|Exception:|Error:' | Select-Object -Last 8
Write-Output ('Unity PS2 model import exit code: '+$process.ExitCode)
if($process.ExitCode -ne 0){exit 1}
