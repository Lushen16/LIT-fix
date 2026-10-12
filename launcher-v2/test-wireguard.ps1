param([string]$WorkDirectory=(Join-Path $env:TEMP ('Litfix-WireGuard-tests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'PKA-Proxy.exe') $WorkDirectory
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe=Join-Path $WorkDirectory 'WireGuardTests.exe'
& $compiler /nologo /platform:x64 /target:exe ('/out:'+$exe) ('/reference:'+(Join-Path $WorkDirectory 'PKA-Proxy.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tests\WireGuardTests.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar testes WireGuard.'}
& $exe $WorkDirectory
if($LASTEXITCODE -ne 0){throw 'Testes WireGuard falharam.'}
Write-Output $WorkDirectory
