# Team Desktop Updater 0.2.0

面向 .NET 8 Windows 桌面程序的更新与本地回退组件。提供 WinForms 对话框及无界面的 UpdateClient API。不是给任意软件安装后自动生效的 MSI；同事需要在自己的项目中安装 NuGet 并接入启动代码。

## 安装
把 Team.DesktopUpdater.1159792767.0.2.0.nupkg 放入本地 packages 文件夹：

```powershell
dotnet add package Team.DesktopUpdater.1159792767 --version 0.2.0 --source ./packages
```

## 接入示例
项目目标 net8.0-windows，启用 UseWindowsForms。AppId 必须为每个产品唯一且跨版本保持不变。

```csharp
using Team.DesktopUpdater;
var updater = new UpdateClient(new UpdateOptions(
    "Company.ProductA", "2.0.0", "ProductA.exe",
    new Uri("https://downloads.example.com/product-a/update.json"),
    "https://downloads.example.com/product-a/"));
// 在主窗口正常初始化后注册；不要在安装过程中提前注册新版。
updater.RegisterCurrent();
// 放在“更新与回退”按钮的 Click 事件中：
UpdateDialog.Show(this, updater, Application.Exit);
```

如果程序支持无界面启动自检并以非零退出码表示失败，可以设置 HealthCheckArgument 为对应参数。没有实现该参数的程序不要填写，否则可能误启动窗口并超时。

## 发布清单

```json
{
  "version": "2.0.1",
  "url": "https://downloads.example.com/product-a/ProductA-2.0.1.zip",
  "sha256": "这里填安装包真实的64位SHA256",
  "notes": "修复问题的说明"
}
```

版本使用数字格式（例如 2.0.1），不支持预发布标签。ZIP 根目录直接放 EXE 及所有依赖，不要多包一层文件夹。先上传包，再发布清单。按产品、架构、发布通道分别提供清单。程序入口名称保持一致。自包含 win-x64 发布可让用户不必另装 .NET。

GitLab CI 可以生成包并发布到外网可访问的更新服务；组件不依赖 GitHub。私有服务可传入配置过身份认证的 HttpClient，凭据不要硬编码进软件或 Git。

## 回退与路径
历史记录位于 %LOCALAPPDATA%/TeamDesktopUpdater/<AppId>/history.json。下载版本位于同目录的 versions 子目录。回退不需要网络，列表只显示本机记录中仍存在的较旧版本。旧目录被删除后不能回退。
更新和回退均以独立目录启动目标版本，原文件保留。不会修改原快捷方式；以后请使用当前版本所在目录启动，或由项目另接固定启动器。可以通过 AppContext.BaseDirectory 显示当前运行目录。

## 限制与生产建议
第一版提供手动回退，不提供启动失败自动回退、固定启动器、更新包数字签名、历史版本自动清理、跨进程并发更新锁或数据库回退。多实例应用应由宿主保证单实例并在更新前保存数据。每个用户独立保存历史。旧版也须集成组件，才能在回退后继续更新。
SHA256 用于完整性校验，不能替代独立发布签名。不要让旧程序直接读取不兼容的新数据库；迁移与备份由业务项目负责。TLS 使用系统证书验证，不关闭校验。

## 验证
`dotnet run --project tests/Tests.csproj` 检查历史、回退目标、版本比较、损坏文件和路径越界。

