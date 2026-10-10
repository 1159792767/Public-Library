"""Publish a version once; bump csproj Version for each library change."""
import json, re, subprocess, xml.etree.ElementTree as ET, zipfile
from pathlib import Path

def run(*args): subprocess.run(args,check=True)
root=ET.parse('Demo.Common.csproj').getroot()
name=root.find('.//PackageId').text.lower(); version=root.find('.//Version').text
if not re.fullmatch(r'\d+\.\d+\.\d+',version): raise ValueError('Stable version required')
index=Path('feed/flat')/name/'index.json'
versions=json.loads(index.read_text())['versions'] if index.exists() else []
if version in versions:
    print('Already published; increment Version to publish changes.'); raise SystemExit(0)
run('dotnet','pack','Demo.Common.csproj','-c','Release','-o','_packages')
package=next(Path('_packages').glob('*.nupkg')); target=index.parent/version; target.mkdir(parents=True,exist_ok=True)
(target/f'{name}.{version}.nupkg').write_bytes(package.read_bytes())
with zipfile.ZipFile(package) as z:
    nuspec=next(n for n in z.namelist() if n.endswith('.nuspec'))
    (target/f'{name}.nuspec').write_bytes(z.read(nuspec))
versions.append(version); versions.sort(key=lambda x:tuple(map(int,x.split('.'))))
index.write_text(json.dumps({'versions':versions}),encoding='utf-8')
run('git','config','user.name','team-updater-bot')
run('git','config','user.email','team-updater-bot@users.noreply.github.com')
run('git','add','feed')
run('git','commit','-m','Publish '+name+' '+version)
run('git','push','origin','HEAD:main')
