param([string]$WorkDirectory=(Join-Path $env:TEMP ('Litfix-DiscordWG-'+[Guid]::NewGuid().ToString('N'))),[string]$CorePath)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'PKA-Proxy.exe') $WorkDirectory
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe=Join-Path $WorkDirectory 'DiscordWireGuardTests.exe'
& $compiler /nologo /platform:x64 /target:exe ('/out:'+$exe) ('/reference:'+(Join-Path $WorkDirectory 'PKA-Proxy.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'tests\DiscordWireGuardTests.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar testes.'}
& $exe $WorkDirectory
if($LASTEXITCODE -ne 0){throw 'Testes Discord WireGuard falharam.'}
if($CorePath){foreach($name in @('discord-wireguard.json','combined-wireguard-global.json','combined-wireguard-apps.json')){& $CorePath check -c (Join-Path $WorkDirectory $name);if($LASTEXITCODE -ne 0){throw "Motor rejeitou $name"}}}
Write-Output $WorkDirectory
