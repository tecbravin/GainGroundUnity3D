param([ValidateSet('Prepare','Create','Tests','Build','Sync')][string]$Step = 'Prepare')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$workspaceRoot = Split-Path $projectRoot -Parent
$validationRoot = Join-Path $workspaceRoot '.validation/UnityProject'
$unityExe = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe'
$resultsRoot = Join-Path $projectRoot 'Validation'
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null
if ($Step -eq 'Prepare') {
    New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
    foreach ($folder in @('Assets','Packages','ProjectSettings')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $folder) -Destination $validationRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $validationRoot 'Analysis') | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Analysis/OriginalROM.bin') -Destination (Join-Path $validationRoot 'Analysis/OriginalROM.bin') -Force
    Write-Output 'Validation copy prepared; the open project is not launched a second time.'
    exit
}
if ($Step -eq 'Sync') {
    Get-ChildItem -LiteralPath (Join-Path $validationRoot 'Assets') -Filter '*.cs.meta' -Recurse | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($validationRoot, $_.FullName)
        $target = Join-Path $projectRoot $relative
        if (Test-Path -LiteralPath $target) {
            $sourceGuid = (Select-String -LiteralPath $_.FullName -Pattern '^guid:').Line
            $targetGuid = (Select-String -LiteralPath $target -Pattern '^guid:').Line
            if ($sourceGuid -ne $targetGuid) { throw ('Script GUID mismatch: ' + $relative) }
        }
    }
    $source = Join-Path $validationRoot 'Assets/GainGround/Editable'
    $destination = Join-Path $projectRoot 'Assets/GainGround/Editable'
    if (Test-Path -LiteralPath $destination) { throw 'Editable assets already exist; refusing to overwrite user edits.' }
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
    Get-ChildItem -LiteralPath (Join-Path $validationRoot 'Assets') -Filter '*.meta' -Recurse | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($validationRoot, $_.FullName)
        $target = Join-Path $projectRoot $relative
        if (!(Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $_.FullName -Destination $target }
    }
    Copy-Item -LiteralPath (Join-Path $validationRoot 'ProjectSettings/EditorBuildSettings.asset') -Destination (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Force
    if (Test-Path -LiteralPath (Join-Path $validationRoot 'Builds')) { Copy-Item -LiteralPath (Join-Path $validationRoot 'Builds') -Destination $projectRoot -Recurse -Force }
    Copy-Item -LiteralPath (Join-Path $validationRoot 'Validation/ArenaPreview.png') -Destination $resultsRoot -Force
    Write-Output 'Generated editable assets and Windows build copied to main project.'
    exit
}
$log = Join-Path $resultsRoot ($Step + '.log')
$arguments = @('-batchmode', '-projectPath', ('"' + $validationRoot + '"'), '-logFile', ('"' + $log + '"'))
if ($Step -eq 'Create') { $arguments += @('-executeMethod','PlayableProjectBuilder.Create','-quit') }
if ($Step -eq 'Build') { $arguments += @('-executeMethod','PlayableProjectBuilder.BuildWindows','-quit') }
if ($Step -eq 'Tests') { $arguments += @('-runTests','-testPlatform','EditMode','-testResults',('"' + (Join-Path $resultsRoot 'EditMode.xml') + '"')) }
$process = Start-Process -FilePath $unityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output ('Unity ' + $Step + ' exit code: ' + $process.ExitCode)
Select-String -LiteralPath $log -Pattern 'error CS|Exception:|Build completed|Build succeeded|BuildFailed|Test run|Playable recreation created' | Select-Object -Last 12
if ($process.ExitCode -ne 0) { exit 1 }
