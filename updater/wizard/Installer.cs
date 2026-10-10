using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
namespace Setup;
public record Plan(string Project,string Id,string Prefix,bool Connected,bool Existing,bool Wpf=false);
public static class Installer {
 public static bool SupportedTarget(string? target){
  var match=System.Text.RegularExpressions.Regex.Match(target??"", @"^net(\d+)\.0-windows(?:\d+(?:\.\d+)*)?$");
  return match.Success && int.TryParse(match.Groups[1].Value,out var major) && major>=8;
 }

 public static Plan Inspect(string path,string? profile){
  path=Path.GetFullPath(path);var xml=XDocument.Load(path);
  if(xml.Root?.Attribute("Sdk") is null||xml.Descendants("TargetFrameworks").Any())throw new Exception("请选择 SDK 风格的单目标项目。");
  if(!SupportedTarget(xml.Descendants("TargetFramework").SingleOrDefault()?.Value))throw new Exception("目标框架须为 net8.0-windows 或更高版本；通过外部 props 设置框架的项目请手动接入。");
  bool wpf=xml.Descendants("UseWPF").Any(x=>string.Equals(x.Value.Trim(),"true",StringComparison.OrdinalIgnoreCase));
  if(!wpf&&!xml.Descendants("UseWindowsForms").Any(x=>string.Equals(x.Value.Trim(),"true",StringComparison.OrdinalIgnoreCase)))throw new Exception("目前支持 WPF 和 WinForms 桌面项目。");
  if(xml.Descendants("Compile").Any(x=>x.Attribute("Remove")!=null)||xml.Descendants("EnableDefaultCompileItems").Any(x=>x.Value=="false"))throw new Exception("自定义编译项目请使用手动接入。");
  for(var d=new DirectoryInfo(Path.GetDirectoryName(path)!);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"Directory.Packages.props")))throw new Exception("中央包管理项目请手动接入。");
  var dir=Path.Combine(Path.GetDirectoryName(path)!,"TeamUpdater.Generated");
  if(Directory.Exists(dir)){
   var file=Path.Combine(dir,"updater-settings.json");if(!File.Exists(file))throw new Exception("生成目录已存在，请先检查原有文件。");
   if(!xml.Descendants("PackageReference").Any(x=>(string?)x.Attribute("Include")=="Team.DesktopUpdater.1159792767")||!xml.Descendants("None").Any(x=>(string?)x.Attribute("TargetPath")=="updater-settings.json"))throw new Exception("检测到未完成的接入：生成目录存在但项目引用或配置复制项缺失。请恢复安装前备份后重新接入。");
   using var j=JsonDocument.Parse(File.ReadAllText(file));return new(path,j.RootElement.GetProperty("appId").GetString()!,j.RootElement.GetProperty("downloadPrefix").GetString()!,!j.RootElement.GetProperty("manifestUrl").GetString()!.Contains("updates.invalid"),true,wpf);
  }
  if(xml.Descendants("PackageReference").Any(x=>(string?)x.Attribute("Include")=="Team.DesktopUpdater.1159792767"))throw new Exception("项目已引用更新组件，请使用现有配置。");
  string id="app-"+Guid.NewGuid().ToString("N"),baseUrl="https://updates.invalid/";
  if(profile!=null){using var j=JsonDocument.Parse(File.ReadAllText(profile));baseUrl=j.RootElement.GetProperty("baseUrl").GetString()!;if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var u)||u.Scheme!="https"||u.UserInfo.Length>0||!baseUrl.EndsWith('/'))throw new Exception("团队地址须为 HTTPS 且以 / 结尾。");}
  return new(path,id,baseUrl+id+"/",profile!=null,false,wpf);
 }
 public static void Resource(string leaf,string target){var a=Assembly.GetExecutingAssembly();using var stream=a.GetManifestResourceStream(a.GetManifestResourceNames().Single(x=>x.EndsWith(".payload."+leaf)))!;using var output=File.Create(target);stream.CopyTo(output);}
 public static void Install(Plan p){
  if(p.Existing)return;
  var dir=Path.Combine(Path.GetDirectoryName(p.Project)!,"TeamUpdater.Generated");if(Directory.Exists(dir))throw new Exception("项目状态已改变，请重新选择。");
  var original=File.ReadAllBytes(p.Project);var xml=XDocument.Load(p.Project);
  try{
   Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"project.original"),original);
   Resource("package.nupkg",Path.Combine(dir,"Team.DesktopUpdater.1159792767.0.2.0.nupkg"));Resource(p.Wpf?"bootstrap-wpf.txt":"bootstrap.txt",Path.Combine(dir,"TeamUpdaterBootstrap.cs"));
   if(p.Wpf){
    xml.Root!.Add(new XElement("PropertyGroup",new XAttribute("Label","TeamUpdaterWizard"),new XElement("UseWindowsForms","true")));
    xml.Root.Add(new XElement("ItemGroup",new XAttribute("Label","TeamUpdaterWizard"),new XElement("Using",new XAttribute("Remove","System.Windows.Forms")),new XElement("Using",new XAttribute("Remove","System.Drawing"))));
   }
   File.WriteAllText(Path.Combine(dir,"updater-settings.json"),JsonSerializer.Serialize(new{appId=p.Id,manifestUrl=p.Prefix+"update.json",downloadPrefix=p.Prefix},new JsonSerializerOptions{WriteIndented=true}));
   xml.Root!.Add(new XElement("ItemGroup",new XAttribute("Label","TeamUpdaterWizard"),new XElement("PackageReference",new XAttribute("Include","Team.DesktopUpdater.1159792767"),new XAttribute("Version","0.2.0")),new XElement("None",new XAttribute("Update","TeamUpdater.Generated/updater-settings.json"),new XAttribute("CopyToOutputDirectory","PreserveNewest"),new XAttribute("CopyToPublishDirectory","PreserveNewest"),new XAttribute("TargetPath","updater-settings.json"))));
   xml.Root.Add(new XElement("PropertyGroup",new XAttribute("Label","TeamUpdaterWizard"),new XElement("RestoreAdditionalProjectSources","$(RestoreAdditionalProjectSources);$(MSBuildProjectDirectory)/TeamUpdater.Generated")));
   xml.Save(p.Project);
   File.WriteAllText(Path.Combine(dir,"接入说明.txt"),"在主窗口 InitializeComponent(); 后添加 TeamUpdaterBootstrap.Attach(this);\n更新地址："+p.Prefix+"update.json\n"+(p.Connected?"已预设服务器，需发布验证。":"服务器未配置。updates.invalid 不可用于真实更新。管理员需替换 JSON 配置。")+"\n撤销：移除 csproj 中两个 Label=TeamUpdaterWizard 的 XML 组和 Attach 行，再移除生成目录。project.original 为安装前备份，请勿覆盖后续编辑。\n");
  }catch{File.WriteAllBytes(p.Project,original);if(Directory.Exists(dir))Directory.Delete(dir,true);throw;}
 }
}
