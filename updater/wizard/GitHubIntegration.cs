using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace Setup;

public record GitHubPlan(Plan Install, string Root, string RelativeProject, string PublicRepo, string ProjectRepo, string DefaultBranch, string PublicBranch, string Manifest);
public static class GitHubIntegration
{
    public static string Repository(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.IsDefaultPort)
            throw new Exception("请填写 https://github.com/所有者/仓库，可带 .git 后缀。");
        var path = uri.AbsolutePath.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        if (!Regex.IsMatch(path, @"^[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+$") || path.Split('/')[1] is "." or "..") throw new Exception("仓库地址应指向仓库首页，不能包含 tree、blob 或 releases 路径。");
        return path;
    }
    public static string ProductId(string repo, string project) => "app-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(repo.ToLowerInvariant()+"/"+project.Replace('\\','/'))))[..20].ToLowerInvariant();
    static async Task<string> Branch(string repo)
    {
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};http.DefaultRequestHeaders.UserAgent.ParseAdd("TeamUpdaterSetup/0.4");
        using var response=await http.GetAsync("https://api.github.com/repos/"+repo);
        if(!response.IsSuccessStatusCode) throw new Exception($"无法读取 {repo}（HTTP {(int)response.StatusCode}）。本预览仅支持公开仓库，请检查网络与仓库地址。");
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if(json.RootElement.GetProperty("private").GetBoolean()) throw new Exception("当前预览不支持私有仓库。");
        return json.RootElement.GetProperty("default_branch").GetString()!;
    }
    public static async Task<GitHubPlan> Inspect(string project,string publicUrl,string projectUrl)
    {
        var pub=Repository(publicUrl);var repo=Repository(projectUrl);
        if(pub.Equals(repo,StringComparison.OrdinalIgnoreCase)) throw new Exception("请选择两个不同的仓库。");
        var p=Installer.Inspect(project,null); var root=Path.GetDirectoryName(p.Project)!;
        for(var d=new DirectoryInfo(root);d!=null;d=d.Parent) if(Directory.Exists(Path.Combine(d.FullName,".git"))||File.Exists(Path.Combine(d.FullName,".git"))){root=d.FullName;break;}
        var rel=Path.GetRelativePath(root,p.Project).Replace('\\','/');
        var configuration=Path.Combine(root,".team-updater.json");
        if(File.Exists(configuration)){
            using var old=JsonDocument.Parse(File.ReadAllText(configuration));
            if(old.RootElement.GetProperty("project").GetString()!=rel||old.RootElement.GetProperty("projectRepo").GetString()!=repo) throw new Exception("本预览每个项目仓库接入一个应用，当前仓库已有另一项配置。");
        }
        var branches=await Task.WhenAll(Branch(pub),Branch(repo));
        using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(30)}){
            var uri=$"https://raw.githubusercontent.com/{pub}/{branches[0]}/feed/index.json";
            using var response=await http.GetAsync(uri);
            if(!response.IsSuccessStatusCode)throw new Exception("公共库尚未初始化包源。请管理员先按配套说明配置 feed/index.json 和包发布流水线。");
        }
        var id=p.Existing?p.Id:ProductId(repo,rel);
        return new(p with{Id=id,Prefix=$"https://github.com/{repo}/releases/download/",Connected=true},root,rel,pub,repo,branches[1],branches[0],$"https://raw.githubusercontent.com/{repo}/updater-channel/{id}.json");
    }
    public static void Install(GitHubPlan plan)
    {
        var p=plan.Install;var dir=Path.Combine(Path.GetDirectoryName(p.Project)!,"TeamUpdater.Generated");
        var workflow=Path.Combine(plan.Root,".github","workflows","team-updater.yml");
        var script=Path.Combine(plan.Root,"scripts","auto_release.py");
        if((File.Exists(workflow)||File.Exists(script))&&!File.Exists(Path.Combine(plan.Root,".team-updater.json"))) throw new Exception("目标流水线或脚本已存在，请先检查，向导不会覆盖未知文件。");
        Installer.Install(p);
        foreach(var file in new[]{"updater-settings.json","TeamUpdaterBootstrap.cs"}) if(File.Exists(Path.Combine(dir,file))) File.Copy(Path.Combine(dir,file),Path.Combine(dir,file+".before-github"),true);
        Installer.Resource(p.Wpf?"bootstrap-wpf.txt":"bootstrap.txt",Path.Combine(dir,"TeamUpdaterBootstrap.cs"));
        Installer.Resource(p.Wpf?"autostart-wpf.txt":"autostart.txt",Path.Combine(dir,"TeamUpdaterAutoStart.cs"));
        File.WriteAllText(Path.Combine(dir,"updater-settings.json"),JsonSerializer.Serialize(new{appId=p.Id,manifestUrl=plan.Manifest,downloadPrefix=p.Prefix},new JsonSerializerOptions{WriteIndented=true}));
        var xml=XDocument.Load(p.Project);
        var feed=$"https://raw.githubusercontent.com/{plan.PublicRepo}/{plan.PublicBranch}/feed/index.json";
        if(!xml.Descendants("RestoreAdditionalProjectSources").Any(x=>x.Value.Contains(feed))) xml.Root!.Add(new XElement("PropertyGroup",new XAttribute("Label","TeamUpdaterWizard"),new XElement("RestoreAdditionalProjectSources","$(RestoreAdditionalProjectSources);"+feed)));
        xml.Save(p.Project);
        var tfm=xml.Descendants("TargetFramework").Single().Value;var sdk=Regex.Match(tfm,@"^net(\d+)\.").Groups[1].Value;var runtime=xml.Descendants("RuntimeIdentifier").LastOrDefault()?.Value;
        if(runtime is not "win-x86" and not "win-x64") runtime=xml.Descendants("PlatformTarget").LastOrDefault()?.Value=="x86"?"win-x86":"win-x64";
        var version=xml.Descendants("Version").LastOrDefault()?.Value??"1.0.0";
        var major=Version.TryParse(version,out var v)?Math.Max(2,v.Major+1):2;
        var tests=Directory.EnumerateFiles(plan.Root,"*.csproj",SearchOption.AllDirectories).Where(x=>!x.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)&&XDocument.Load(x).Descendants("IsTestProject").Any(e=>e.Value=="true")).Select(x=>Path.GetRelativePath(plan.Root,x).Replace('\\','/')).ToArray();
        File.WriteAllText(Path.Combine(plan.Root,".team-updater.json"),JsonSerializer.Serialize(new{project=plan.RelativeProject,publicRepo=plan.PublicRepo,projectRepo=plan.ProjectRepo,appId=p.Id,runtime,entryExe=(xml.Descendants("AssemblyName").LastOrDefault()?.Value??Path.GetFileNameWithoutExtension(p.Project))+".exe",releaseMajor=major,testProjects=tests},new JsonSerializerOptions{WriteIndented=true}));
        Directory.CreateDirectory(Path.GetDirectoryName(workflow)!);Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        Installer.Resource("auto_release.txt",script);Installer.Resource("workflow.txt",workflow);
        File.WriteAllText(workflow,File.ReadAllText(workflow).Replace("branches: [main]","branches: ["+JsonSerializer.Serialize(plan.DefaultBranch)+"]").Replace("'10.0.x'","'"+sdk+".0.x'"));
        File.WriteAllText(Path.Combine(dir,"GitHub接入说明.txt"),"已自动接入，无需添加 Attach 行。启动后及每 15 分钟检查更新，确认后安装。\n请将本次生成和修改的项目文件、TeamUpdater.Generated 内 cs/nupkg/json、.team-updater.json、scripts/auto_release.py、.github/workflows/team-updater.yml 提交推送至 "+plan.ProjectRepo+" 的 "+plan.DefaultBranch+" 分支。无需填写密钥。不要提交 bin、obj 或 before-github/project.original 备份。\nActions 每小时检查公共包（GitHub 可能延迟），也可手动 Run workflow。公共库新版本须先发布进 feed。构建通过才移动更新频道。\n项目应已引用公共库的包；没有引用时不会猜测安装哪一个包。仅公开仓库、单应用、显式稳定 PackageReference。本预览不支持中央包管理或自定义离线驱动环境。\n若没有测试项目，则自动验证仅覆盖编译，不代表业务兼容性已验证。\n更新清单："+plan.Manifest);
    }
}
