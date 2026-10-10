namespace Setup;
public sealed class GitHubWizard:Form {
 readonly TextBox project=new(){Width=520,ReadOnly=true};
 readonly TextBox publicRepo=new(){Width=620,PlaceholderText="https://github.com/所有者/Public-Library"};
 readonly TextBox projectRepo=new(){Width=620,PlaceholderText="https://github.com/所有者/Project-Library"};
 readonly Button install=new(){Text="检查并接入",Width=180,Height=38};
 readonly TextBox status=new(){Multiline=true,ReadOnly=true,Width=730,Height=180,ScrollBars=ScrollBars.Vertical};
 public GitHubWizard(){
  Text="团队更新器 0.4.0 — GitHub 自动接入预览";ClientSize=new(800,660);MinimumSize=new(820,700);StartPosition=FormStartPosition.CenterScreen;Font=new("Microsoft YaHei UI",10);
  var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};Controls.Add(flow);
  flow.Controls.Add(new Label{Text="GitHub 公共包自动更新",Font=new(Font.FontFamily,20,FontStyle.Bold),AutoSize=true});
  flow.Controls.Add(new Label{Text="支持 .NET 8+ WPF / WinForms · 公开仓库 · 每仓库一个应用",AutoSize=true,Margin=new Padding(3,12,3,15)});
  flow.Controls.Add(new Label{Text="1. 本地项目（应已引用公共库包）",AutoSize=true});
  var row=new FlowLayoutPanel{Width=730,Height=40};row.Controls.Add(project);var browse=new Button{Text="选择…",Width=90};row.Controls.Add(browse);flow.Controls.Add(row);
  browse.Click+=(_,_)=>{using var d=new OpenFileDialog{Filter="C# 项目|*.csproj"};if(d.ShowDialog()==DialogResult.OK)project.Text=d.FileName;};
  flow.Controls.Add(new Label{Text="2. 公共库 GitHub 地址",AutoSize=true});flow.Controls.Add(publicRepo);
  flow.Controls.Add(new Label{Text="3. 项目库 GitHub 地址",AutoSize=true});flow.Controls.Add(projectRepo);
  flow.Controls.Add(new Label{Text="首次接入后，将生成文件提交推送到项目库，Actions 即开始工作。\n公共库需已初始化包源；私有库与 GitLab 将在后续版本接入。",Width=730,Height=50,Margin=new Padding(3,12,3,8)});
  flow.Controls.Add(install);flow.Controls.Add(status);
  install.Click+=async(_,_)=>{
   install.Enabled=false;browse.Enabled=false;publicRepo.Enabled=projectRepo.Enabled=false;
   try{
    if(string.IsNullOrWhiteSpace(project.Text))throw new Exception("请先选择本地项目。");
    status.Text="正在检查仓库、包源和项目配置…";
    var plan=await GitHubIntegration.Inspect(project.Text,publicRepo.Text,projectRepo.Text);
    GitHubIntegration.Install(plan);
    status.Text="本地接入完成。无需填写 AppId、JSON 或添加启动代码。\r\n\r\n下一步：在 Git 工具中提交并推送生成的项目文件、TeamUpdater.Generated（cs/nupkg/json）、.team-updater.json、scripts/auto_release.py 和 .github/workflows/team-updater.yml。\r\n\r\n首次 Actions 成功后，从 Releases 下载程序。公共包发布后每小时检测；程序启动及每 15 分钟检查，确认后安装。\r\n\r\n详细说明位于 TeamUpdater.Generated/GitHub接入说明.txt。";
   }catch(Exception e){status.Text="接入未完成："+e.GetBaseException().Message;}
   finally{install.Enabled=true;browse.Enabled=true;publicRepo.Enabled=projectRepo.Enabled=true;}
  };
 }
}
