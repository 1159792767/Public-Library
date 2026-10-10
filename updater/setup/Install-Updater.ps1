[CmdletBinding(SupportsShouldProcess=$true)]
param([Parameter(Mandatory=$true)][string]$Project,[string]$AppId,[string]$ManifestUrl,[string]$DownloadPrefix,[switch]$Uninstall)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath $Project).Path
if([IO.Path]::GetExtension($projectPath) -ne '.csproj'){throw 'Project must be a .csproj file.'}
$root=Split-Path -Parent $projectPath
$generated=Join-Path $root 'TeamUpdater.Generated'
$state=Join-Path $generated 'install-state.json'
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function WriteUtf8([string]$Path,[string]$Text){[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
if($Uninstall){
 if(!(Test-Path -LiteralPath $state)){throw 'No installation record found.'}
 $record=Get-Content -Raw -LiteralPath $state | ConvertFrom-Json
 if($record.project -ne $projectPath){throw 'Record belongs to another project.'}
 foreach($item in $record.files){
  $resolved=[IO.Path]::GetFullPath((Join-Path $generated $item.name))
  if(!$resolved.StartsWith($generated+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid tracked path.'}
  if(!(Test-Path -LiteralPath $resolved) -or (Hash $resolved) -ne $item.hash){throw 'Generated files changed; refusing automatic uninstall.'}
 }
 if((Hash $projectPath) -ne $record.projectHash){throw 'Project changed since installation; remove the two TeamUpdaterAutoSetup groups manually to preserve edits.'}
 if($PSCmdlet.ShouldProcess($projectPath,'Restore original project and remove generated files')){
  Copy-Item -LiteralPath (Join-Path $generated 'project.original') -Destination $projectPath -Force
  foreach($item in $record.files){Remove-Item -LiteralPath (Join-Path $generated $item.name)}
  Remove-Item -LiteralPath $state
  Remove-Item -LiteralPath $generated
  Write-Output 'Uninstalled. Remove the TeamUpdaterBootstrap.Attach(this) line if you added it.'
 };return
}
if($AppId -notmatch '^[A-Za-z0-9_.-]+$'){throw 'AppId is required; use letters, digits, dot, underscore or hyphen.'}
foreach($address in @($ManifestUrl,$DownloadPrefix)){
 $uri=$null
 if(![Uri]::TryCreate($address,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo){throw 'Use HTTPS URLs without embedded credentials.'}
}
if(!$DownloadPrefix.EndsWith('/')){throw 'DownloadPrefix must end with /.'}
[xml]$xml=Get-Content -Raw -LiteralPath $projectPath
if($xml.Project.Sdk -notlike 'Microsoft.NET.Sdk*'){throw 'Only SDK-style projects are supported.'}
$tfms=@($xml.SelectNodes('//TargetFramework') | ForEach-Object {$_.InnerText})
if($tfms.Count -ne 1 -or $tfms[0] -notmatch '^net10\.0-windows([0-9.]+)?$' -or $xml.SelectSingleNode('//TargetFrameworks')){throw 'Requires a single net10.0-windows target; no automatic framework migration.'}
if(!($xml.SelectNodes('//UseWindowsForms') | Where-Object {$_.InnerText -eq 'true'})){throw 'Requires UseWindowsForms=true.'}
if($xml.SelectSingleNode('//ManagePackageVersionsCentrally[text()="true"]')){throw 'Central package management requires manual integration.'}
$cursor=$root
while($cursor){if(Test-Path -LiteralPath (Join-Path $cursor 'Directory.Packages.props')){throw 'Directory.Packages.props detected; use manual integration.'};$next=Split-Path -Parent $cursor;if($next -eq $cursor){break};$cursor=$next}
if($xml.SelectSingleNode('//EnableDefaultCompileItems[text()="false"]') -or $xml.SelectSingleNode('//Compile[@Remove]')){throw 'Custom compile items require manual integration.'}
if(Test-Path -LiteralPath $state){
 $record=Get-Content -Raw -LiteralPath $state | ConvertFrom-Json
 if($record.project -ne $projectPath -or $record.appId -ne $AppId -or $record.manifestUrl -ne $ManifestUrl -or $record.downloadPrefix -ne $DownloadPrefix){throw 'Already installed with different settings.'}
 foreach($item in $record.files){if(!(Test-Path -LiteralPath (Join-Path $generated $item.name)) -or (Hash (Join-Path $generated $item.name)) -ne $item.hash){throw 'Generated files changed. Review manually.'}}
 if(!$xml.SelectSingleNode('//ItemGroup[@Label="TeamUpdaterAutoSetup"]')){throw 'Integration reference missing.'}
 Write-Output 'Already installed. No changes made.';return
}
if(Test-Path -LiteralPath $generated){throw 'Generated directory already exists without a valid record.'}
if($xml.SelectSingleNode('//PackageReference[@Include="Team.DesktopUpdater.1159792767"]')){throw 'Updater already referenced; use manual integration.'}
$package=Join-Path $PSScriptRoot 'packages/Team.DesktopUpdater.1159792767.0.1.0.nupkg'
if(!(Test-Path -LiteralPath $package)){throw 'Bundled package missing.'}
if(!$PSCmdlet.ShouldProcess($projectPath,'Install package reference, local source, settings and bootstrap')){return}
New-Item -ItemType Directory -Path $generated | Out-Null
try {
 Copy-Item -LiteralPath $projectPath -Destination (Join-Path $generated 'project.original')
 Copy-Item -LiteralPath $package -Destination (Join-Path $generated 'Team.DesktopUpdater.1159792767.0.1.0.nupkg')
 WriteUtf8 (Join-Path $generated 'updater-settings.json') (@{appId=$AppId;manifestUrl=$ManifestUrl;downloadPrefix=$DownloadPrefix}|ConvertTo-Json)
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TeamUpdaterBootstrap.cs.template') -Destination (Join-Path $generated 'TeamUpdaterBootstrap.cs')
 $group=$xml.CreateElement('ItemGroup');$group.SetAttribute('Label','TeamUpdaterAutoSetup')
 $ref=$xml.CreateElement('PackageReference');$ref.SetAttribute('Include','Team.DesktopUpdater.1159792767');$ref.SetAttribute('Version','0.1.0');[void]$group.AppendChild($ref)
 $content=$xml.CreateElement('None');$content.SetAttribute('Update','TeamUpdater.Generated/updater-settings.json');$content.SetAttribute('CopyToOutputDirectory','PreserveNewest');$content.SetAttribute('CopyToPublishDirectory','PreserveNewest');$content.SetAttribute('TargetPath','updater-settings.json');[void]$group.AppendChild($content)
 [void]$xml.Project.AppendChild($group)
 $props=$xml.CreateElement('PropertyGroup');$props.SetAttribute('Label','TeamUpdaterAutoSetup')
 $source=$xml.CreateElement('RestoreAdditionalProjectSources');$source.InnerText='$(RestoreAdditionalProjectSources);$(MSBuildProjectDirectory)/TeamUpdater.Generated';[void]$props.AppendChild($source);[void]$xml.Project.AppendChild($props)
 $xml.Save($projectPath)
 $files=@('project.original','Team.DesktopUpdater.1159792767.0.1.0.nupkg','updater-settings.json','TeamUpdaterBootstrap.cs') | ForEach-Object {@{name=$_;hash=(Hash (Join-Path $generated $_))}}
 $record=@{project=$projectPath;projectHash=(Hash $projectPath);appId=$AppId;manifestUrl=$ManifestUrl;downloadPrefix=$DownloadPrefix;files=@($files)}
 WriteUtf8 $state ($record|ConvertTo-Json -Depth 5)
} catch {
 if(Test-Path -LiteralPath (Join-Path $generated 'project.original')){Copy-Item -LiteralPath (Join-Path $generated 'project.original') -Destination $projectPath -Force}
 throw 'Installation failed; original project restored where backup exists. Inspect TeamUpdater.Generated before retrying.'
}
Write-Output 'Installed. Add TeamUpdaterBootstrap.Attach(this); after InitializeComponent() in the main Form constructor.'
Write-Output 'Then run dotnet build and verify the update button placement.'
