param([string]$WorkDirectory=(Join-Path $env:TEMP ('PKA-V2-tests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$root=Join-Path $PSScriptRoot 'src'
$sources=@('RoutingConfiguration.cs','TunConfiguration.cs','SessionStore.cs','ProxyDiagnostics.cs','NetworkGuard.cs','DiscordProxy.cs','AppDiscovery.cs','DiscordWireGuard.cs') | ForEach-Object {Join-Path $root $_}
$sources+=Join-Path $PSScriptRoot 'tests\ProtocolTests.cs'
$sources+=Join-Path $PSScriptRoot 'tests\DiscordTests.cs'
$sources+=Join-Path $PSScriptRoot 'tests\CombinedTests.cs'
$exe=Join-Path $WorkDirectory 'tests.exe'
& $compiler /nologo /platform:x64 /target:exe ('/out:'+$exe) /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tests\V2Tests.cs') $sources
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar testes.'}
& $exe $WorkDirectory
if($LASTEXITCODE -ne 0){throw 'Testes falharam.'}
Write-Output ('Resultados em: '+$WorkDirectory)
