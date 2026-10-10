using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace Team.DesktopUpdater;
public record InstalledVersion(string Version,string Executable,string Sha256) {
 public override string ToString()=>Version+"  —  "+Path.GetDirectoryName(Executable);
}
public sealed class VersionStore {
 readonly string path;
 public VersionStore(string root){Directory.CreateDirectory(root);path=Path.Combine(root,"history.json");}
 List<InstalledVersion> Read()=>File.Exists(path)?JsonSerializer.Deserialize<List<InstalledVersion>>(File.ReadAllText(path))??[]:[];
 public void Register(string version,string executable){
  PackageSafety.Parse(version);executable=Path.GetFullPath(executable);
  var item=new InstalledVersion(version,executable,PackageSafety.Hash(executable));var all=Read();
  all.RemoveAll(x=>x.Executable.Equals(executable,StringComparison.OrdinalIgnoreCase)||x.Version==version);all.Add(item);
  var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(all));File.Move(temp,path,true);
 }
 public List<InstalledVersion> Candidates(string current)=>Read().Where(x=>PackageSafety.Parse(x.Version)<PackageSafety.Parse(current)&&File.Exists(x.Executable)).OrderByDescending(x=>PackageSafety.Parse(x.Version)).ToList();
 public void Validate(InstalledVersion target){
  if(!Read().Contains(target))throw new InvalidDataException("该版本不在本机历史记录中");
  if(!File.Exists(target.Executable))throw new InvalidDataException("旧版本文件已被移动或删除");
  PackageSafety.Verify(target.Executable,target.Sha256);
 }
}
public static class PackageSafety {
 public static Version Parse(string value){if(!Version.TryParse(value,out var v)||v.Build<0||value.Any(c=>!char.IsAsciiDigit(c)&&c!='.'))throw new InvalidDataException("版本号必须为数字分段格式");return v;}
 public static bool IsNewer(string candidate,string current)=>Parse(candidate)>Parse(current);
 public static string Hash(string file){using var s=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(s));}
 public static void Verify(string file,string expected){if(!string.Equals(Hash(file),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("文件校验失败，未切换版本");}
 public static void ValidateEntry(string entry){if(string.IsNullOrWhiteSpace(entry)||Path.GetFileName(entry)!=entry||!entry.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("入口必须是更新包根目录中的 EXE 文件名");}
 public static void Extract(string zip,string destination){
  var root=Path.GetFullPath(destination)+Path.DirectorySeparatorChar;using var archive=ZipFile.OpenRead(zip);
  long total=0;
  foreach(var entry in archive.Entries){
   var full=Path.GetFullPath(Path.Combine(root,entry.FullName));total+=entry.Length;
   if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||entry.FullName.Contains(':')||total>2L*1024*1024*1024)throw new InvalidDataException("非法路径或更新包解压大小超限");
  }
  Directory.CreateDirectory(destination);archive.ExtractToDirectory(destination,false);
 }
}
