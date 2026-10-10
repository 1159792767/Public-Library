LabVIEW 公共 DLL 更新工具 预览版

运行 DllUpdateManager.exe。更新器独立运行，不会被 LabVIEW 加载，因此更新器自身的 .NET 8 与被调用 DLL 的运行时无关。
managed 表示 .NET 节点；native 表示 Call Library Function Node。x86/x64 必须按 LabVIEW 或已发布宿主的位数选择，不是按 Windows 位数。

使用
维护人员为每个产品、调用方式和位数提供独立更新包与配置。用户导入配置，选择实际 DLL 目录和宿主进程名，退出 LabVIEW/宿主并确认，再安装。也可以先使用本地 ZIP 验证，随后对接 HTTPS 服务。
本版需手动确认安装，不会热替换正在加载的 DLL，不自动杀进程或重启 LabVIEW。更新后由用户重新启动并验收。

每个包 ZIP 根目录放 package.json 及列出的 DLL，不嵌套目录；只支持 DLL，非 DLL 依赖需由维护者另行安装。manifest 示例：
{"product":"Company.Common","version":"1.0.1","kind":"native","architecture":"x64","files":[{"name":"Common.dll","sha256":"真实64位SHA256"}]}

团队配置示例：
{"product":"Company.Common","kind":"native","architecture":"x64","feed":"https://updates.example.com/common/native/x64/update.json"}
远程清单示例：
{"url":"https://updates.example.com/common/native/x64/common-1.0.1.zip","sha256":"ZIP真实64位SHA256"}
当前要求清单和包在同一 HTTPS 主机。HTTPS 服务器由公司提供；软件不会自动建立服务器。不要在配置中写密码。

回退
备份存放于 DLL 目录的 .dll-updater 子目录。支持回退上一次更新；更新后会阻止再安装下一包，直到回退或由管理员归档该备份记录。这是保守的试点限制，不是完整多版本管理。不要手工修改备份内容。回退不恢复数据库。

兼容性必须由维护人员验证
.NET 节点能加载的框架取决于 LabVIEW 版本与宿主支持；可能需要 .NET Framework 目标。不能仅把 DLL 改名或改位数。
原生 DLL 要提供稳定 C ABI、调用约定、参数和内存约定，位数必须匹配。AnyCPU DLL 的本机依赖也需要相应位数。
清单的 managed/native/x86/x64 标签由维护者填写，工具校验标签和文件哈希，不自动证明二进制 ABI 正确。
当前未在真实 LabVIEW 环境验证。先在独立测试目录和非生产项目测试四类 DLL，再正式部署。
更新和回退会复制多个文件；普通异常会尝试恢复，断电等中断需管理员用备份恢复。无数字签名、自动推送通知、增量下载或配置迁移。
