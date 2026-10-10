using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
namespace Team.DesktopUpdater;
public sealed record UpdateOptions(string AppId,string CurrentVersion,string EntryExe,Uri ManifestUri,string AllowedDownloadPrefix,string? HealthCheckArgument=null);
public sealed record UpdateManifest(string Version,string Url,string Sha256,string? Notes);
public sealed class UpdateClient {
 readonly HttpClient http;
 public UpdateOptions Options{get;}
 public VersionStore History{get;}
 public string Root{get;}
 public UpdateClient(UpdateOptions options,HttpClient? httpClient=null){
  if(string.IsNullOrWhiteSpace(options.AppId)||options.AppId.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='.'&&c!='-'&&c!='_'))throw new ArgumentException("AppId 仅支持字母数字及 .-_ 字符");
  PackageSafety.Parse(options.CurrentVersion);PackageSafety.ValidateEntry(options.EntryExe);
  if(options.ManifestUri.Scheme!="https"||!Uri.TryCreate(options.AllowedDownloadPrefix,UriKind.Absolute,out var prefix)||prefix.Scheme!="https"||!options.AllowedDownloadPrefix.EndsWith('/'))throw new ArgumentException("清单与下载前缀必须为 HTTPS，前缀以 / 结尾");
  Options=options;http=httpClient??new HttpClient{Timeout=TimeSpan.FromMinutes(10)};
  http.DefaultRequestHeaders.UserAgent.ParseAdd("TeamDesktopUpdater/0.1");
  Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TeamDesktopUpdater",options.AppId);
  History=new VersionStore(Root);
 }
 public void RegisterCurrent()=>History.Register(Options.CurrentVersion,Path.Combine(AppContext.BaseDirectory,Options.EntryExe));
 public async Task<UpdateManifest?> CheckAsync(CancellationToken ct=default){
  var text=await http.GetStringAsync(Options.ManifestUri,ct);
  var m=JsonSerializer.Deserialize<UpdateManifest>(text,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("空更新清单");
  ValidateManifest(m);
  return PackageSafety.IsNewer(m.Version,Options.CurrentVersion)?m:null;
 }
 void ValidateManifest(UpdateManifest m){
  PackageSafety.Parse(m.Version);
  if(m.Url is null||!m.Url.StartsWith(Options.AllowedDownloadPrefix,StringComparison.Ordinal)||!Uri.TryCreate(m.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new InvalidDataException("更新地址不在允许的发布路径中");
  if(m.Sha256 is null||m.Sha256.Length!=64||!m.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("SHA256 格式错误");
 }
 public async Task<string> StageAsync(UpdateManifest manifest,IProgress<string>? progress=null,CancellationToken ct=default){
  ValidateManifest(manifest);if(!PackageSafety.IsNewer(manifest.Version,Options.CurrentVersion))throw new InvalidDataException("更新清单不得降级");
  var root=Path.Combine(Root,"versions",manifest.Version+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var zip=Path.Combine(root,"update.zip");
  using(var response=await http.GetAsync(manifest.Url,HttpCompletionOption.ResponseHeadersRead,ct)){
   response.EnsureSuccessStatusCode();await using var input=await response.Content.ReadAsStreamAsync(ct);await using var output=File.Create(zip);var buffer=new byte[81920];long total=0;int n;
   while((n=await input.ReadAsync(buffer,ct))>0){total+=n;if(total>1024L*1024*1024)throw new InvalidDataException("下载超过 1 GB 限制");await output.WriteAsync(buffer.AsMemory(0,n),ct);progress?.Report($"正在下载 {total/1048576.0:F1} MB");}
  }
  progress?.Report("正在校验和解压…");var app=Path.Combine(root,"app");
  await Task.Run(()=>{PackageSafety.Verify(zip,manifest.Sha256);PackageSafety.Extract(zip,app);},ct);
  var exe=Path.Combine(app,Options.EntryExe);if(!File.Exists(exe))throw new InvalidDataException("未找到程序入口");
  if(Options.HealthCheckArgument is not null){
   using var p=Process.Start(new ProcessStartInfo(exe,Options.HealthCheckArgument){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=app})??throw new IOException("无法启动自检");
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(30));
   try{await p.WaitForExitAsync(timeout.Token);}catch{if(!p.HasExited)p.Kill(true);throw;}
   if(p.ExitCode!=0)throw new InvalidDataException("新版自检失败，当前版本保持不变");
  }
  return exe;
 }
 public void Launch(string exe){_ = Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)!})??throw new IOException("无法启动程序");}
 public void Rollback(InstalledVersion target){History.Validate(target);Launch(target.Executable);}
}

