# Video Duplicate Finder

Video Duplicate Finder（下称 VDF）是一款跑在 Windows 上的视频与图片查重工具，按画面内容的相似度比对，而不是文件名或文件大小。与其他同类工具不同的是，它还能找出分辨率不同、帧率不同、甚至加了水印的重复文件。

# 功能特性

- 扫描速度快
- 重复扫描极快（复用已有缓存）
- 可选原生调用 ffmpeg 函数，进一步提速
- 按相似度查找重复的视频 / 图片（可选用 pHash 比对，零额外开销）
- 部分片段检测 —— 找出从长视频里剪出来的短片（基于音频指纹）
- 可选的 AI 匹配 —— 用神经网络图像嵌入找出裁剪、镜像、缩放、重度编辑过的副本，也能定位长视频中剪出的片段（画面不重叠也行）。全程 100% 本机运行。
- 桌面图形界面（Windows）

# 部分片段检测

VDF 能识别出「较短的那个视频其实是较长视频的一个片段」这种情况 —— 比如从电影里剪出来的一场戏，或者从一段长录像里截下来的片段。候选由音频指纹找出，因此能抓到普通画面扫描漏掉的片段；默认情况下，每个音频匹配还会在对应偏移位置做一次画面确认。

它作为常规画面查重之后的**可选第二阶段**运行，走的是音频指纹流水线（Chromaprint 风格的色度提取 + 滑动窗口汉明相似度匹配）。匹配到的文件对会出现在结果列表里，并带一列 **片段偏移**，标明片段在源文件中从哪个位置开始。

### 如何启用

进入 **设置 → 部分片段**，勾选 **启用部分片段检测**，然后调整下面几项：

| 设置项 | 默认值 | 说明 |
|--------|--------|------|
| 最小片段/源比例 (%) | 10 | 片段时长占源视频时长的最小百分比，短于该比例的片段会被忽略 |
| 最小音频相似度 (%) | 80 | 滑动窗口指纹匹配被接受所需的最低平均汉明相似度 |
| 要求视觉匹配 | 开 | 在检测到的偏移位置上再核对画面，画面不像的音频匹配会被否决 |
| 最小视觉相似度 (%) | 85 | 视觉确认步骤所需的最低画面相似度 |

> **注意**：部分片段检测要求两个文件都有音轨，没有音频的视频会被跳过 —— 这种情况请看下方的 **AI 匹配**里的纯画面版本。

---

# AI 匹配（可选）

VDF 还可以用神经网络图像嵌入来额外比对视频 —— 通过 [ONNX Runtime](https://onnxruntime.ai/) 运行的 [DINOv2](https://github.com/facebookresearch/dinov2) 视觉模型。经典比对依然是权威结果，AI 这一轮只会**增加**它有把握的配对，所以开启它绝不会掩盖你本来能拿到的结果。它恰好擅长像素方法无能为力的场景：

- **变形副本** —— 裁剪、镜像、缩放、加黑边、调色或经过其他重度编辑的同一段视频。这样找到的配对在结果里会带一个 **AI** 标记。
- **视觉部分检测** —— 通过匹配采样关键帧与一致的时间偏移，找出较长录像里的剪辑片段。与上面基于音频的检测不同，它也能处理无声、静音和重新配音的视频。匹配结果同样带 **片段偏移** 列。

### 如何启用

两个开关互相独立，默认都是关闭：

| 设置项 | 位置 | 默认值 | 说明 |
|--------|------|--------|------|
| AI 匹配（附加检测） | 设置 → 匹配 | 关 | 在所选经典模式之上叠加嵌入比对 |
| AI 相似度阈值 (%) | 设置 → 匹配 | 94 | 两个文件的嵌入需要达到的相似程度。降到 92 左右可以找到编辑幅度更大的副本，但误报风险略高 |
| 视觉检测部分重复（AI） | 设置 → 部分片段 | 关 | 密集关键帧匹配，用于剪辑/嵌套片段，不需要音频 |
| AI 帧命中阈值 (%) | 设置 → 部分片段 | 89 | 单个关键帧被判为命中所需的相似度；至少要 4 个命中落在同一时间偏移上，两个视频才会配对。若无关视频被配对，调高此项 |

### 组件、隐私与开销

- 首次使用时 VDF 会下载两个组件（**一次性约 100 MB**）：来自[微软官方发布](https://github.com/microsoft/onnxruntime/releases)的 ONNX Runtime 库，以及嵌入模型（按固定 SHA256 校验完整性）。它们存放在扫描数据库旁边。图形界面会在下载前询问。
- **全部计算都在你本机的 CPU 上完成。** 没有云服务、不需要账号、不上传任何东西 —— 模型就在你自己的机器上分析画面，仅此而已。
- 开销：哈希阶段大约每文件 50 毫秒；嵌入会缓存进扫描数据库（每文件约 2 KB），所以重新扫描依然很快。视觉部分检测把关键帧缓存单独存在 `DenseEmbeddings.db` 旁挂文件里（每个视频约 25 KB），会自行清理。
- 仅支持 Windows (x64)。

---

# 下载

[每日构建](https://github.com/0x90d/videoduplicatefinder/releases/tag/4.1.x) —— 每次提交后附件都会自动重建并替换。

[版本化发布](https://github.com/0x90d/videoduplicatefinder/releases)（形如 `v4.1.1` 的标签）不定期发布，发布后文件不再变动，适合包管理器和需要固定下载源的人。

> **想用经典界面？** 4.1 换了新界面。最后的经典界面版本仍保留在 [4.0.x 发布页](https://github.com/0x90d/videoduplicatefinder/releases/tag/4.0.x)，数据库和设置双向兼容。

> **从 3.x 升级：** 扫描数据库会在首次加载时自动迁移。缓存的图片哈希会在下次扫描时重新计算（图片处理从 ImageSharp 换成了 FFmpeg）；视频哈希不受影响。迁移之后不建议再降回 3.x。最后的 3.x 版本仍可在 [3.0.x 发布页](https://github.com/0x90d/videoduplicatefinder/releases/tag/3.0.x) 获取。

各平台可用的包：

- `GUI-<platform>` —— 桌面程序

---

# 桌面界面

### 环境要求

需要 FFmpeg 和 FFprobe。首次启动时 VDF 会尝试自动下载。
原生 FFmpeg 绑定需要 FFmpeg 8.x 的**共享库版本**（不是 master 分支）。

#### Windows

从 https://ffmpeg.org/download.html 下载最新的 FFmpeg GPL shared 包，然后把 `ffmpeg.exe` 和 `ffprobe.exe` 解压到 `VDF.GUI.exe` 所在目录、名为 `bin` 的子目录里，或者确保它们在 `PATH` 上。

---

# 截图（已过时）

<img src="https://user-images.githubusercontent.com/46010672/129763067-8855a538-4a4f-4831-ac42-938eae9343bd.png" width="510">

# 许可证

Video Duplicate Finder 采用 AGPLv3 授权。

可选的 AI 组件在首次使用时单独下载，各自遵循自己的许可证：ONNX Runtime（MIT）与 DINOv2-small 嵌入模型（Apache-2.0）。两者都没有打包进、也没有链接进发布版的二进制文件。

# 致谢 / 第三方

- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [ActiPro Avalonia Controls (Free Edition)](https://github.com/Actipro/Avalonia-Controls)
- [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)

- [AcoustID.NET by wo80](https://github.com/wo80/AcoustID.NET) —— 部分片段检测所用的音频指纹流水线（Chromaprint 风格的色度提取、FIR 平滑与指纹编码）派生自该库，遵循 LGPL 2.1 授权

- [ONNX Runtime](https://github.com/microsoft/onnxruntime)（Microsoft，MIT）—— 可选 AI 匹配功能的推理引擎
- [DINOv2](https://github.com/facebookresearch/dinov2)（Meta AI，Apache-2.0）—— AI 匹配背后的图像嵌入模型，使用来自 [Xenova/dinov2-small](https://huggingface.co/Xenova/dinov2-small) 的 int8 量化 ONNX 导出（镜像于本仓库的 [ai-models-v1 发布](https://github.com/0x90d/videoduplicatefinder/releases/tag/ai-models-v1)）

# 构建

- .NET 10.x
- 建议使用 Visual Studio 2022 或更高版本

# 贡献

- 每项新增或修复单独开一个 pull request —— 不要合并成一个 PR
- 除非对应已有 issue，否则在 PR 里写清楚它做了什么
- 较大的 PR 请先开 issue 讨论
