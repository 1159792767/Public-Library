namespace Setup;
internal static class Program {
 [STAThread] static void Main(string[] args){
  if(args.Length==2&&args[0]=="--verify-install"){Installer.Install(Installer.Inspect(args[1],null));return;}
  ApplicationConfiguration.Initialize();Application.Run(new Wizard());
 }
}
public sealed class Wizard:Form {
 readonly Label title=new(){Dock=DockStyle.Top,Height=65,Font=new("Microsoft YaHei UI",19,FontStyle.Bold)};
 readonly TextBox body=new(){Multiline=true,ReadOnly=true,TabStop=false,HideSelection=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=SystemColors.Window,ScrollBars=ScrollBars.Vertical};
 readonly Button next=new(){Text="下一步",Width=110,Height=34};readonly Button back=new(){Text="上一步",Width=100,Height=34};
 readonly Button browse=new(){Text="选择项目…",Width=140,Height=34};readonly Button profile=new(){Text="导入团队预设（管理员）",Width=230,Height=34};
 string? project,profilePath;Plan? plan;int step;
 public Wizard(){Text="团队更新器安装向导";ClientSize=new(840,540);MinimumSize=new(860,580);StartPosition=FormStartPosition.CenterScreen;Font=new("Microsoft YaHei UI",11);
  var side=new Panel{Dock=DockStyle.Left,Width=160,BackColor=Color.FromArgb(29,66,108)};side.Controls.Add(new Label{Text="TEAM\nUPDATER\n\n更新与回退\n项目接入向导",ForeColor=Color.White,Dock=DockStyle.Fill,Padding=new Padding(15,45,0,0),Font=new("Microsoft YaHei UI",15)});Controls.Add(side);
  var area=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24,24,24,70)};Controls.Add(area);area.BringToFront();area.Controls.Add(body);area.Controls.Add(title);
  var bottom=new FlowLayoutPanel{Anchor=AnchorStyles.Bottom|AnchorStyles.Right,Left=580,Top=490,Width=250,Height=45,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};area.Controls.Add(bottom);bottom.Left=area.ClientSize.Width-260;bottom.Top=area.ClientSize.Height-50;bottom.BringToFront();bottom.Controls.Add(next);bottom.Controls.Add(back);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55};area.Controls.Add(actions);actions.Controls.Add(browse);actions.Controls.Add(profile);
  var bundled=Path.Combine(AppContext.BaseDirectory,"team-profile.json");if(File.Exists(bundled))profilePath=bundled;
  browse.Click+=(_,_)=>{using var d=new OpenFileDialog{Filter="C# 项目|*.csproj"};if(d.ShowDialog()==DialogResult.OK){project=d.FileName;Render();}};
  profile.Click+=(_,_)=>{using var d=new OpenFileDialog{Filter="团队配置|*.json"};if(d.ShowDialog()==DialogResult.OK){profilePath=d.FileName;Render();}};
  back.Click+=(_,_)=>{step--;Render();};next.Click+=(_,_)=>Advance();Render();
 }
 void Render(){back.Enabled=step>0&&step<3;browse.Visible=profile.Visible=step==1;next.Enabled=true;
  title.Text=step switch{0=>"欢迎使用更新器安装向导",1=>"选择需要接入的项目",2=>"确认接入",_=>"项目文件接入完成"};next.Text=step==2?"安装":step==3?"完成":"下一步";
  body.Text=step switch{
   0=>"向导将安装更新和版本回退组件。\r\n\r\n支持 .NET 8 及以上 Windows Forms 项目。\r\n无需填写 AppId，首次安装自动生成并保存。\r\n\r\n团队服务器由管理员统一预设；没有预设时仅完成本地接入，在线更新暂不可用。\r\n\r\n不会改写业务入口或现有 CI。安装后只需添加一行接入代码。",
   1=>$"项目：\r\n{project??"尚未选择"}\r\n\r\n团队预设：\r\n{profilePath??"未配置；将安装为待配置状态，不可在线更新"}",
   2=>$"项目：{project}\r\n\r\n{(plan!.Existing?"检测到已接入，将保留现有文件和产品标识。":"将备份项目、安装 NuGet、生成配置和按钮接入代码。产品标识自动保存。") }\r\n\r\n{(plan.Connected?"已读取更新地址，仍需验证服务器可用性。":"服务器未配置：安装后不能检查真实更新，需管理员统一配置。")}",
   _=>$"在主窗口 InitializeComponent(); 后添加：\r\n\r\nTeamUpdaterBootstrap.Attach(this);\r\n\r\n运行 dotnet build 并检查按钮位置。\r\n\r\n{(plan!.Connected?"已写入预设地址，发布首个版本后验证。":"在线更新尚未启用。请管理员替换生成配置中 updates.invalid 的地址。")}\r\n\r\n原项目备份和说明位于 TeamUpdater.Generated。\r\n回退要求保留旧目录，不回退业务数据。"};
 }
 void Advance(){try{if(step==3){Close();return;}if(step==1){if(project is null)throw new Exception("请先选择项目。");plan=Installer.Inspect(project,profilePath);}if(step==2){Installer.Install(plan!);}step++;Render();}catch(Exception e){MessageBox.Show(this,e.Message,"接入未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
}


