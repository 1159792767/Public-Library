namespace Team.DesktopUpdater;
/// <summary>Modal WinForms integration. Call RegisterCurrent after application startup succeeds.</summary>
public static class UpdateDialog {
 public static void Show(IWin32Window owner,UpdateClient client,Action exitApplication){
  using var dialog=new Form{Text="更新与版本回退",Width=760,Height=500,StartPosition=FormStartPosition.CenterParent,Font=new Font("Microsoft YaHei UI",10),MinimumSize=new Size(700,460)};
  var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};dialog.Controls.Add(layout);
  layout.Controls.Add(new Label{AutoSize=true,Text="当前程序版本："+client.Options.CurrentVersion});
  var check=new Button{Text="检查更新",AutoSize=true};var install=new Button{Text="下载更新并重启",AutoSize=true,Enabled=false};var rollback=new Button{Text="回退到选中版本并重启",AutoSize=true};
  var row=new FlowLayoutPanel{Width=680,Height=42};row.Controls.Add(check);row.Controls.Add(install);layout.Controls.Add(row);
  var status=new Label{Width=680,Height=70,Text="更新失败会保留当前版本。回退只切换程序，不恢复业务数据。"};layout.Controls.Add(status);
  layout.Controls.Add(new Label{AutoSize=true,Text="本机旧版本（无需联网；原目录必须保留）"});
  var versions=new ListBox{Width=680,Height=130,HorizontalScrollbar=true};layout.Controls.Add(versions);layout.Controls.Add(rollback);
  void Refresh(){versions.Items.Clear();foreach(var v in client.History.Candidates(client.Options.CurrentVersion))versions.Items.Add(v);if(versions.Items.Count>0)versions.SelectedIndex=0;rollback.Enabled=versions.Items.Count>0;}
  Refresh();UpdateManifest? manifest=null;
  void Busy(bool busy){check.Enabled=!busy;install.Enabled=!busy&&manifest is not null;rollback.Enabled=!busy&&versions.SelectedItem is not null;}
  check.Click+=async(_,_)=>{Busy(true);try{status.Text="正在检查…";manifest=await client.CheckAsync();status.Text=manifest is null?"已是最新版本。":$"发现 {manifest.Version}。{manifest.Notes}";}catch(Exception e){manifest=null;status.Text="检查失败："+e.Message;}finally{Busy(false);}};
  install.Click+=async(_,_)=>{
   if(manifest is null||MessageBox.Show(dialog,"保存工作后再更新。是否继续？","确认更新",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
   Busy(true);try{var exe=await client.StageAsync(manifest,new Progress<string>(s=>status.Text=s));client.Launch(exe);dialog.Close();exitApplication();}catch(Exception e){status.Text="更新未完成，当前版本保留："+e.Message;Busy(false);}
  };
  rollback.Click+=(_,_)=>{
   if(versions.SelectedItem is not InstalledVersion target)return;
   if(MessageBox.Show(dialog,$"回退至 {target.Version}？业务数据不会自动恢复，请先保存工作并确认旧版本兼容当前数据。","确认回退",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
   try{client.Rollback(target);dialog.Close();exitApplication();}catch(Exception e){status.Text="回退失败，当前版本保留："+e.Message;}
  };
  dialog.ShowDialog(owner);
 }
}
