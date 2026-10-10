# GitHub 自动更新接入预览 0.4.0

## 已配置的演示

公共库：https://github.com/1159792767/Public-Library

项目库：https://github.com/1159792767/Project-Library

先运行配套的 2.0.2 旧版演示程序。启动后自动查询新版本，按钮打开更新对话框，检查后下载更新并重启。界面显示程序版本和公共库版本。保留旧版解压目录，可在新版回退。

## 将其他项目接入

1. 项目应先正常引用公共库中的 NuGet 包，版本必须是明确的稳定数字版本。
2. 将项目放在其 GitHub 仓库的本地克隆目录内，运行 TeamUpdaterSetup.exe。
3. 选择 csproj，填写公共库和项目库的 GitHub 首页地址；可带 .git 后缀。点击“检查并接入”。
4. 在自己的 Git 工具检查生成的改动，将项目文件、TeamUpdater.Generated 中 cs/nupkg/json、.team-updater.json、scripts/auto_release.py、.github/workflows/team-updater.yml 提交推送到项目库默认分支。备份 project.original、*.before-github 和 bin/obj 不必提交。
5. GitHub 的 Actions 页面查看 Automatic public package delivery。首次成功后从 Releases 下载程序。第一次发布前，客户端可能显示检查失败，这是更新频道尚未建立。

无需手填 AppId、下载地址、哈希或 Attach 启动代码。若原项目已经手动 Attach，重复接入保护会避免重复按钮。若应用有启动欢迎窗/登录窗，自动识别的首个 WinForms 窗口可能不是业务主窗口，这类项目需将接入代码改到明确的主窗口。

## 日常更新

修改公共库代码，递增 Demo.Common.csproj 中 Version 并推送。Publish public package 会测试、打包并发布 feed。版本一旦发布不能覆盖；再次修改必须增加版本号。

项目每小时 23 分检查公共包，GitHub 可能延迟。可在 Actions 中选择 Automatic public package delivery → Run workflow 立即执行同一流程。该方案是轮询，不是实时推送。

检测到包或项目源码变化后，构建完整应用。成功后上传 ZIP 和哈希清单，最后切换 updater-channel 分支中的应用清单。失败不更新频道。客户端启动及每 15 分钟检查，发现更新后显示按钮提醒；用户保存工作并确认后安装。

流水线不会修改 main 的依赖版本，实际解析版本保存在发布包 resolved-dependencies.json 和 Release 说明中。旧版项目文件仍可复现开发基线，发布包可以追踪实际版本和源码提交。

## 首版范围

- GitHub 公开仓库，Windows .NET 8+ WPF / WinForms，一个项目仓库一个应用，x86/x64。
- 公共库遵循演示的 /feed/index.json NuGet 静态源约定；新建空仓库须由管理员先初始化包源及发布流水线。不是任意 GitHub 仓库粘贴地址即可发现所有 DLL。
- 首次上传代码和授权由工程师现有 Git 工具完成；客户端不保存 GitHub 密钥。仓库需允许 Actions 和 contents:write，保护规则可能要求管理员配置。
- 自动识别明确标记 IsTestProject=true 的测试项目；没有测试时仅保证编译成功，不保证业务行为兼容。可在 .team-updater.json 的 testProjects 中补充测试路径。
- 不支持中央包管理、浮动/预发布包版本、多目标项目或特殊本地硬件驱动构建环境。LabVIEW、私有仓库和 GitLab 不在本轮交付范围。
- 更新是完整程序切换，不热替换 DLL。回退依赖保留旧目录，不恢复数据库或业务文件。界面包装和退出行为须结合实际应用验收；SHA256 校验不等同独立代码签名。

## 新公共库初始化

已有 Public-Library 已配置好，无需重复操作。其他公共库管理员可参考该仓库的 .github/workflows/publish-package.yml、scripts/publish_package.py 和 feed/index.json；调整根目录库项目路径、测试路径、包 ID 和 feed URL。首次初始化属于管理员工作，普通工程师只填写已有公共库地址。
