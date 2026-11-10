# 第三方组件

| 组件 | 使用范围与来源 |
| --- | --- |
| DeepSeek Harness | 用户另行安装的桌面程序和插件宿主。使用宿主提供的 SDK 服务。 |
| NapCatQQ 与 QQ | 用户另行安装的 QQ 接入运行时与客户端。安装来源及基线版本见 `runtime/upstream.json`，发行许可见各上游项目。 |
| dsh-onebot-qq | 用户通过 Harness 市场安装的 OneBot 传输插件。 |
| dsh-fish | 可选的成长插件，单独安装市场发布版。上游采用 MIT。 [项目仓库](https://github.com/ssamurias/dsh-fish) |
| dsh-bili-whale | 复用单独下载的扫码授权、凭据管理和视频资料读取模块。仓库包含本项目的只读补丁与上游 MIT 许可声明。 [项目仓库](https://github.com/Alterssss/dsh-bili-whale) |
| FFmpeg | 可选的外部抽帧程序，具体许可取决于使用的构建。 [FFmpeg](https://ffmpeg.org/legal.html) |
| BreezeBlue | 可选的第三方语音合成 API。用户提供自己的 API Key。 [开发者文档](https://docs.breezeblue.ai/) |

上游 B 站模块版权归其作者所有，详见 [上游 MIT 许可证](life/LICENSE.bili-whale)。本项目新增的只读检查记录在 [补丁](life/bili-whale-readonly.patch)。
