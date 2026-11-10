# 大肥鱼管家 · Dafeiyu Manager

大肥鱼管家是 Windows 上的 DeepSeek Harness 与 QQ 机器人控制台。它用 WinForms 提供桌面界面，通过 PowerShell 管理 Harness、独立 NapCat 实例和 Windows 计划任务。可选的 Harness 插件把 QQ 私聊、成长日记、B 站只读浏览和语音回复串到同一个会话中。

适合在 Windows 主机或云服务器上维护一个由本人扫码授权的 QQ 机器人。项目由社区维护，与 DeepSeek、腾讯和哔哩哔哩无隶属关系。

## 功能

| 功能 | 行为 |
| --- | --- |
| 一键启停 | 复用正在运行的实例并补启动缺失服务。一键关闭会停止管理范围内的 Harness 和 NapCat。关闭管家窗口后，后台继续运行。 |
| 暂停自动回复 | 暂停私聊处理并保留 QQ 登录。恢复后只接受配置中的私聊白名单。 |
| 开机自启 | Windows 开机后启动后台，指定用户登录时打开管家窗口。两个阶段由独立计划任务处理。 |
| 自动修复 | 每两分钟检查进程、QQ 会话和 OneBot 连接。连续发现同一故障后补启动进程或重连，修复间隔至少五分钟。手动关闭服务会暂停本次开机期间的修复。 |
| 收发诊断 | 显示 QQ 登录、OneBot 连接、最近私聊收发时间和模型处理状态。QQ 在线与 WebSocket 接通只是连接检查。 |
| 成长与自主生活 | 可选的 `dsh-fish` 提供成长会话、兴趣和日记。适配层支持公开文章阅读和在自己的作品目录中保存故事或小项目。 |
| 主动分享 | 默认关闭。启用后只向本机配置的主人 QQ 发送带真实成长记录依据的分享，每个北京时间自然日最多五条。 |
| B 站浏览 | 扫码登录后可读标题、简介、字幕和评论。配置 FFmpeg 后，成长插件可抽帧供模型查看。部署说明使用只读策略。 |
| QQ 原生语音 | 可选 BreezeBlue API。设置音色、试听和每日申请字符上限。试听和合成会消耗服务商额度。 |
| 本机管理 | 支持深浅主题、放大字体、日记目录和本机凭据文件入口。 |

## 运行环境

| 组件 | 要求 |
| --- | --- |
| Windows | 64 位 Windows，包含 .NET Framework 4.x、Windows PowerShell 5.1 和任务计划程序。原部署已在 Windows Server 2019 Datacenter 上运行。 |
| DeepSeek Harness | 桌面版，具备可用的模型登录。适配层以 0.2.2 系列宿主 SDK 为基线。 |
| QQ 与 NapCat | 原部署组合为 NapCat 4.18.42 Windows Node 包与 QQ 9.9.31-49738。用户自行安装和扫码。归档来源及校验值见 [runtime/upstream.json](runtime/upstream.json)。 |
| OneBot | Harness 插件 `dsh-onebot-qq` 0.9.19，使用反向 WebSocket。 |
| Node.js 与 npm | Node.js 22 或更高版本用于工具和测试。Harness 插件运行在 Harness 自带的宿主中。 |
| 可选依赖 | `@ssamurias/dsh-fish` 0.9.5、FFmpeg、BreezeBlue API Key。 |

端口固定在回环地址。`6100` 是 NapCat WebUI，`6101` 是大肥鱼 B 站登录页，`8643` 是 OneBot 反向 WebSocket。每台 Windows 主机按一个实例部署，计划任务名称和端口由该实例占用。

## 安装

1. 下载或克隆源码。用 Windows PowerShell 5.1 在项目目录执行下面的命令。构建脚本调用系统自带的 C# 编译器，输出 `dist/大肥鱼管家.exe`。

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
   ```

2. 创建 `private/instance.json`。复制 [instance.example.json](instance.example.json)，填写 Windows 本机的绝对路径、机器人 QQ 和主人 QQ。QQ 号用 JSON 字符串表示。`fishHome` 要与成长插件实际数据目录一致。`ffmpegExe` 可以留空，让成长插件从 PATH 查找。

   ```powershell
   New-Item -ItemType Directory -Path .\private -Force | Out-Null
   Copy-Item .\instance.example.json .\private\instance.json
   notepad.exe .\private\instance.json
   ```

3. 安装 QQ 和 NapCat。将独立 Windows Node 运行包解压到 `<runtimeDir>/napcat`。确认该目录下有 `node.exe` 和 `index.js`，子目录 `napcat` 用作 NapCat 配置目录。QQ 数据放在 `<runtimeDir>/qq-data`。把 NapCat WebUI 绑定到 `127.0.0.1:6100`，在 OneBot 网络配置里启用反向 WebSocket 客户端 `ws://127.0.0.1:8643/ws`。WebUI Token 和 OneBot 访问 Token 分开设置。

4. 在 Harness 插件市场安装并启用 `dsh-onebot-qq`。把 [onebot.example.yml](onebot.example.yml) 的条目合并进该 profile 的 `cordis.patch.yml`。替换 QQ 号、工作区、媒体目录及 OneBot Token，Token 要和 NapCat 客户端一致。该文件只保留一个 `id: dsh-onebot` 条目。群聊保持 `disabled`，私聊白名单只填允许联系机器人的账号。

5. 在运行 Harness 的 Windows 用户下执行以下命令，按提示填写 **NapCat WebUI Token**。脚本创建本机数据目录、登录页令牌和两个交互式任务。已有运行配置与令牌会保留。

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare-instance.ps1
   ```

6. 按下文安装 Harness 适配层，重启 Harness。打开管家，点「一键启动」并在 NapCat WebUI 完成 QQ 登录。看到「QQ 已在线」及 OneBot 接通后，用主人 QQ 发一条私聊确认实际收发。已保存的 QQ 授权可用于后续快速登录，授权失效时需要重新扫码。

### Harness 适配层与 B 站

1. 准备 `dsh-bili-whale` 0.4.6 的源码，固定到提交 `8b0009f8ce2ebdc421afcb2e727b19ac18912ec6`。将其置于 `<runtimeDir>/bili-plugin`，该目录应直接包含 `lib/auth.js`、`lib/credentials.js`、`tools/bili.mjs` 与 `package.json`。
2. 在上游源码目录用 `git apply <本项目绝对路径>/life/bili-whale-readonly.patch` 应用只读补丁。运行 `npm install --omit=dev --ignore-scripts` 安装上游的二维码依赖。保留 `<runtimeDir>/life/bili-whale.config.json` 中的 `readOnly: true`。
3. 退出 Harness。在它的 Desktop profile 中备份 `package.json`、`pnpm-lock.yaml` 和 `cordis.patch.yml`。将 `dafeiyu-life`、`dafeiyu-voice` 安装为本地 `link:` 包，分别指向仓库内的 `life`、`voice` 绝对目录，启用两个 bundle。例如 profile 的 `package.json` 中保留原有条目，再追加以下项。

   ```json
   {
     "dependencies": {
       "dafeiyu-life": "link:C:/Projects/dafeiyu-manager/life",
       "dafeiyu-voice": "link:C:/Projects/dafeiyu-manager/voice"
     },
     "dsh": { "profile": { "bundles": ["dafeiyu-life", "dafeiyu-voice"] } }
   }
   ```

   在 profile 目录通过 Harness 自带的 pnpm 刷新依赖。不同宿主版本的插件管理界面可能有差异，以现有 profile 的包管理方式为准。两个插件使用 Harness 提供的 `@deepseek-ai/dsh-tools` 服务。

4. 重新启动 Harness。点管家「B 站登录网页」扫码，按需要开启自主活动。先用「现在活动一次」查看效果。公开配置默认暂停自主活动。

### 成长插件与主动分享

安装市场版 `@ssamurias/dsh-fish` 0.9.5，按其设置页初始化本机数据目录。把 `<runtimeDir>/life/config.json` 的 `fishBridge` 改为 `true`、`scheduler` 设为 `"fish"` 后重启 Harness。适配层将主人的私聊路由到 `dsh-fish` 的家中会话。

在成长插件的额外工具白名单中添加 `dafeiyu_life`、`dafeiyu_workspace` 和 `dafeiyu_qq_voice`。开启主动 QQ 分享时，额外加入 `dafeiyu_share`，把它配置为主动发送工具，并将 `allowProactiveQQ` 显式设为 `true`。成长插件的 B 站评论功能保持关闭。关闭成长桥可将 `fishBridge` 设为 `false` 并移除 `scheduler` 后重启。成长事件流和已有日记请通过插件提供的方式管理。

### 开机自启与自动修复

在 Windows PowerShell 5.1 中执行下面的命令，用与 Harness 相同的 Windows 账号注册任务。任务计划程序保存 Windows 登录凭据。Windows 账号密码修改后，重新注册这批任务。

```powershell
.\install-autostart.ps1 -Credential (Get-Credential)
.\control.ps1 -Action EnableAutoRepair
```

在管家右下角分别控制开机自启和自动修复。开机任务可以在 Windows 登录前启动后台。要将开机启动的 Harness 切换到桌面会话，可在 Windows 登录后点击「一键启动」。关闭服务后再点「一键启动」可恢复本次开机的自动修复。

### 语音

在管家的「大肥鱼语音」中填写 BreezeBlue API Key，选择音色并保存。可先点「生成试听」，试听音频保存在 `<runtimeDir>/media/voice`。在已经连通的 QQ 私聊中可要求大肥鱼用语音回复。语音合成和发送是两个步骤，语音入口失败时可使用文字交流。

## 数据和排查

- `private/` 保存本机路径、账号配置、B 站管理令牌、语音密钥和可选备忘。`<runtimeDir>` 保存 NapCat 授权、浏览登录态、运行日志和本地日记。请限制这些目录的访问权限，并让它们留在本机。
- 开机验收记录在 `logs/boot-status.json`，自动修复记录在 `logs/auto-repair.json`，NapCat 日志在 `<runtimeDir>/logs`。反馈问题时可描述错误和组件版本，提交前清理日志里的账号和凭据。
- 「等待连接」时核对 NapCat 反向 WS 地址、Token 和 Harness 插件状态。「已连接 · 待验证」时用已允许的 QQ 发一条新消息，核对收发时间。「需重新登录」时打开 QQ 登录网页扫码。
- 修改 profile 配置前先备份。升级 Harness、OneBot 或成长插件后先核对兼容性，成长适配层使用宿主和上游的内部接口。

## 源码与验证

`Manager.cs`、`VoiceSettings.cs` 和 `Theme.cs` 实现桌面界面。`control.ps1`、`repair-watch.ps1` 和 `runtime/` 管理后台进程及任务。`health/` 保存状态诊断逻辑，`life/` 和 `voice/` 是 Harness 扩展。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
node --test health/core.test.mjs health/probe.test.mjs life/core.test.mjs life/fish-core.test.mjs voice/core.test.mjs
```

在已登录、已配置的实例上，可另用 `test.ps1 -Live` 验证管家后端的真实状态。公开版本以源码形式发布，构建产生的 EXE 可在本机使用。

## 许可证

本仓库自有源码采用 [MIT 许可证](LICENSE)。外部组件分别安装并遵循各自许可。来源与使用范围见 [第三方声明](THIRD-PARTY-NOTICES.md)。
