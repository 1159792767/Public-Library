using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace DllUpdates;
public record FileItem(string Name,string Sha256);
public record PackageInfo(string Product,string Version,string Kind,string Architecture,FileItem[] Files);
public record History(string Backup,string[] Added,string Version);
public static class Engine {
 public static string Hash(string file){using var f=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(f));}
 public static PackageInfo Inspect(string stage,string product,string kind,string architecture){
  var manifest=JsonSerializer.Deserialize<PackageInfo>(File.ReadAllText(Path.Combine(stage,"package.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new Exception("包清单为空");
  if(manifest.Product!=product||manifest.Kind!=kind||manifest.Architecture!=architecture)throw new Exception("包的产品、调用类型或位数与当前配置不一致");
  if(!Version.TryParse(manifest.Version,out _)||manifest.Files is null||manifest.Files.Length==0)throw new Exception("版本或文件清单无效");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var f in manifest.Files){
   if(Path.GetFileName(f.Name)!=f.Name||f.Name.Contains(':')||!f.Name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||!names.Add(f.Name))throw new Exception("本版只允许不重复的根目录 DLL 文件");
   if(!string.Equals(Hash(Path.Combine(stage,f.Name)),f.Sha256,StringComparison.OrdinalIgnoreCase))throw new Exception("DLL 校验失败："+f.Name);
  }
  return manifest;
 }
 public static string Extract(string zip){
  var root=Path.Combine(Path.GetTempPath(),"DllUpdate-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  using var a=ZipFile.OpenRead(zip);long size=0;
  foreach(var e in a.Entries){if(e.FullName!=Path.GetFileName(e.FullName)||e.FullName.Contains(':')||(size+=e.Length)>512L*1024*1024)throw new Exception("包只允许根目录文件且不得超过 512 MB");}
  a.ExtractToDirectory(root);return root;
 }
 static string State(string target)=>Path.Combine(target,".dll-updater");
 static void UnlockCheck(IEnumerable<string> paths){foreach(var p in paths)if(File.Exists(p)){using var stream=new FileStream(p,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}}
 public static void Install(string stage,string target,string product,string kind,string architecture){
  target=Path.GetFullPath(target);if(!Directory.Exists(target))throw new Exception("请先选择现有 DLL 目录");
  var package=Inspect(stage,product,kind,architecture);var state=State(target);Directory.CreateDirectory(state);
  using var gate=new FileStream(Path.Combine(state,"lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  var historyFile=Path.Combine(state,"pending.json");if(File.Exists(historyFile))throw new Exception("已有未回退的备份。本版先回退或由管理员归档备份，再应用其他包。");
  UnlockCheck(package.Files.Select(f=>Path.Combine(target,f.Name)));
  var backup=Path.Combine(state,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);var added=new List<string>();
  foreach(var f in package.Files){var path=Path.Combine(target,f.Name);if(File.Exists(path))File.Copy(path,Path.Combine(backup,f.Name));else added.Add(f.Name);}
  var history=new History(backup,added.ToArray(),package.Version);File.WriteAllText(historyFile,JsonSerializer.Serialize(history));
  try{foreach(var f in package.Files)File.Copy(Path.Combine(stage,f.Name),Path.Combine(target,f.Name),true);}
  catch{Restore(target,history);File.Delete(historyFile);throw;}
 }
 static void Restore(string target,History h){
  var state=Path.GetFullPath(State(target))+Path.DirectorySeparatorChar;
  if(!Path.GetFullPath(h.Backup).StartsWith(state,StringComparison.OrdinalIgnoreCase))throw new Exception("备份路径无效");
  var files=Directory.GetFiles(h.Backup,"*.dll");
  foreach(var name in h.Added)if(Path.GetFileName(name)!=name||name.Contains(':')||!name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))throw new Exception("历史记录无效");
  UnlockCheck(files.Select(x=>Path.Combine(target,Path.GetFileName(x))).Concat(h.Added.Select(x=>Path.Combine(target,x))));
  foreach(var file in files)File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
  foreach(var name in h.Added){var file=Path.Combine(target,name);if(File.Exists(file))File.Delete(file);}
 }
 public static void Rollback(string target){
  target=Path.GetFullPath(target);var state=State(target);if(!Directory.Exists(state))throw new Exception("没有备份记录");
  using var gate=new FileStream(Path.Combine(state,"lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  var file=Path.Combine(state,"pending.json");var history=JsonSerializer.Deserialize<History>(File.ReadAllText(file))??throw new Exception("备份记录无效");
  Restore(target,history);File.Delete(file);
 }
}
