param([Parameter(Mandatory=$true)][string]$DllDirectory,[Parameter(Mandatory=$true)][string]$Product,[Parameter(Mandatory=$true)][string]$Version,[ValidateSet('managed','native')][string]$Kind,[ValidateSet('x86','x64')][string]$Architecture,[Parameter(Mandatory=$true)][string]$OutputZip)
$ErrorActionPreference='Stop'
if(!$Kind -or !$Architecture){throw 'Kind and Architecture are required'}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Version must be x.y.z'}
if(Test-Path -LiteralPath $OutputZip){throw 'Output already exists; do not overwrite published packages'}
$files=@(Get-ChildItem -LiteralPath $DllDirectory -File -Filter *.dll)
if(!$files.Count){throw 'No DLL files'}
$stage=Join-Path $env:TEMP ('DllPackage-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $stage | Out-Null
$items=@($files|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $stage;@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
@{product=$Product;version=$Version;kind=$Kind;architecture=$Architecture;files=$items}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $stage 'package.json') -Encoding utf8
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $OutputZip
Get-FileHash -LiteralPath $OutputZip -Algorithm SHA256
