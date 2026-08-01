# Codex Quota 软启动清单

目标是在 7 天内用小范围、可回滚的社区发布验证产品表达、安装路径和跨平台兼容性。当前 Windows 版是未签名 Beta，macOS 版也尚未进行 Developer ID 签名与公证，因此本轮重点是获得高质量反馈，不追求一次性大规模曝光。

所有对外文案取自 [`launch-copy.md`](launch-copy.md)。不要实际发布未经当日复核的草稿。

## 1. 启动前门槛

### 仓库与版本

- [ ] 仓库名、README、下载链接和 Release 名称一致
- [ ] README 首屏在移动端和桌面端都能看懂：它是什么、支持什么平台、如何下载
- [ ] README 明确写出“非 OpenAI 官方项目”
- [ ] README 明确写出 Windows 为未签名 Beta，可能触发 SmartScreen
- [ ] README 明确写出 macOS 尚未 Developer ID 签名与公证
- [ ] README 写明额度通过本机 `codex app-server` 读取，不读取或保存登录 token
- [ ] 最新 Release 同时提供 macOS、Windows x64、Windows ARM64 和对应 SHA-256
- [ ] 从一台非开发机实际下载并完成一次 macOS 安装测试
- [ ] 从一台非开发机实际下载并完成一次 Windows x64 安装测试
- [ ] Windows ARM64 未经实机验证时，下载项明确标注测试状态
- [ ] 所有下载和文档链接在未登录 GitHub 的浏览器中可访问
- [ ] License、Security、Contributing 和 Issue 模板可见
- [ ] CI 在默认分支和发布标签上通过

### 产品与诊断

- [ ] ChatGPT 套餐账户能正确显示百分比
- [ ] API Key 按量计费模式显示可理解的“不支持套餐百分比”提示
- [ ] macOS 的前台、最小化、移动窗口、多显示器行为已验证
- [ ] Windows 的 100%、125%、150%、200% DPI 至少完成核心路径验证
- [ ] Windows “复制诊断信息”不包含 token、`auth.json` 内容或其他凭证
- [ ] 失败提示包含可执行的下一步，而不只是“无法获取额度”
- [ ] 颜色选择在浅色、深色和常见色觉差异下仍可辨识

### 推广素材

- [ ] 1280 × 640 社交预览图已设置到 GitHub 仓库
- [ ] 10–15 秒演示 GIF 在 README 首屏可见且小于建议体积
- [ ] GIF 中没有通知、邮箱、真实 token、私人路径或不希望公开的用户名
- [ ] macOS 完整界面截图来自真实应用
- [ ] Windows 完整界面截图来自真实应用；合成图不标成实机截图
- [ ] Windows SmartScreen 说明图没有声称“误报”或“绝对安全”
- [ ] 所有图片包含可理解的 alt text
- [ ] 中文和英文首屏素材的用词一致

### 社区准备

- [ ] 阅读每个社区当天的版规、自我推广规则和合适分区
- [ ] 发布账号有正常参与记录；不在多个社区同一时刻机械群发
- [ ] 至少预留发布后 2 小时用于集中回复
- [ ] 准备好 Issue 标签：`bug`、`windows`、`macos`、`arm64`、`dpi`、`install`、`privacy`、`documentation`
- [ ] 准备一条置顶回复，说明诊断信息中不得包含 token
- [ ] 指定暂停条件和当日负责人

## 2. 渠道链接与 UTM

所有社区帖子优先指向仓库首页，让用户先看到说明、风险和安装方法，不要把带 UTM 的链接直接指向二进制文件。

### 命名规则

基础链接：

```text
https://github.com/qxlfd666-wq/codex-quota
```

统一参数：

```text
utm_medium=community
utm_campaign=codex_quota_soft_launch
utm_content=<内容变体>
```

渠道 `utm_source`：

| 渠道 | `utm_source` | 建议 `utm_content` |
| --- | --- | --- |
| V2EX | `v2ex` | `launch_post_cn` |
| Linux.do | `linuxdo` | `launch_post_cn` |
| 即刻 | `jike` | `short_post_cn` |
| 掘金 | `juejin` | `build_story_cn` |
| Reddit r/codex | `reddit_codex` | `launch_post_en` |
| OpenAI Developer Community | `openai_community` | `feedback_post_en` |
| Show HN | `hacker_news` | `show_hn_en` |
| Product Hunt | `product_hunt` | `launch_page_en` |

示例：

```text
https://github.com/qxlfd666-wq/codex-quota?utm_source=v2ex&utm_medium=community&utm_campaign=codex_quota_soft_launch&utm_content=launch_post_cn
```

### 发布前替换项

- `{CHANNEL_URL}`：替换为对应渠道的完整 UTM 链接
- `{ISSUES_URL}`：替换为 `https://github.com/qxlfd666-wq/codex-quota/issues/new/choose`
- Product Hunt 等平台不允许或不适合 UTM 时，使用平台提供的官方追踪链接
- 复制正文后搜索 `{`，确保没有遗漏占位符
- 点击最终正文中的链接，确认没有多余句号、中文括号或转义字符

## 3. 反馈指标

启动前记录 24 小时基线，之后每天在固定时间记录一次。GitHub Traffic 只保留有限时间窗口，因此建议保存截图或填入独立表格。

### 核心指标

| 指标 | 目的 | 记录方法 |
| --- | --- | --- |
| 仓库独立访客 | 判断帖子是否带来真实到访 | GitHub Insights → Traffic |
| Referring sites | 比较渠道质量 | GitHub Insights → Traffic |
| Release 下载量（按平台） | 判断用户是否进入安装步骤 | GitHub Release asset download count |
| 安装成功反馈 | 衡量真实激活，不只看下载 | Issue / 社区回复中的成功确认 |
| 首次成功显示额度 | 验证核心价值 | 可选匿名问卷或 Issue 模板，不在应用内增加追踪 |
| 有效 Issue 数 | 发现兼容性与文档问题 | GitHub Issues，排除重复项 |
| 首次响应时间 | 保持早期用户信任 | Issue 与社区发布时间差 |
| SmartScreen / Gatekeeper 阻断率 | 决定签名优先级 | 回复和 Issue 中按平台标记 |
| “无法获取额度”占比 | 验证 Windows 修复 | `windows` + `bug` Issue 标签 |
| Star / Fork | 观察持续兴趣 | GitHub 仓库统计；不作为唯一成功指标 |

### 建议定性标签

- `message-clear`：用户无需追问就理解产品用途
- `install-macos-success` / `install-windows-success`
- `blocked-smartscreen` / `blocked-gatekeeper`
- `quota-unavailable`
- `positioning` / `dpi` / `multi-monitor`
- `privacy-question`
- `feature-request`
- `docs-confusing`

### 7 天目标建议

这些是判断是否继续扩大发布的门槛，不是对外承诺：

- 至少 10 条包含系统环境的有效反馈
- macOS 至少 5 次核心路径成功确认
- Windows x64 至少 5 次核心路径成功确认，或清楚定位主要阻断原因
- 所有凭证或隐私相关报告在 4 小时内响应
- 严重崩溃、错误额度或凭证暴露问题为 0
- 重复出现 3 次以上的安装或定位问题都有公开 Issue 和处理状态
- README 能回答发布回复中 80% 以上的重复问题

## 4. 七天软启动顺序

原则：一次只扩大一层受众。每天开始前先处理前一天的高频问题并更新文档；相同文案不要原样群发。

### Day 0：内部预检，不对外发布

- 完成启动前门槛和真实机器安装测试
- 记录仓库流量、下载量、Issue 数和 Star 数基线
- 为每个渠道生成并测试 UTM 链接
- 准备演示 GIF、平台截图和风险说明图
- 建立已知问题 Issue：Windows 未签名、macOS 未公证、API Key 模式不支持
- 确认发布者第二天有至少 2 小时回复窗口

**继续门槛：** 没有凭证泄露、错误下载或无法复现的核心崩溃；README 与 Release 链接全部有效。

### Day 1：V2EX，小范围中文首发

- 在 `分享创造` 使用 V2EX 长文版，附 GIF 和两张真实截图
- 发布后 2 小时内集中回复，不主动顶帖
- 记录访问、下载、安装成功和重复问题
- 把每个高频疑问转换为 README 或 Issue 改进

**暂停条件：** 2 个以上用户报告安装包下载错误、额度明显错误或隐私边界与说明不一致。

### Day 2：Linux.do，获取技术反馈

- 先合入 Day 1 的文档修正
- 明确写出“当前仅 macOS / Windows，没有 Linux 包”
- 使用技术细节版，重点询问 app-server 边界、Windows ARM64、DPI 和多屏
- 不因为社区名称而暗示 Linux 已支持

**继续门槛：** 核心数据流没有未解释的安全问题；诊断信息确认不含凭证。

### Day 3：即刻，测试短文案与视觉理解

- 使用一张清晰首图和短帖，不堆叠技术术语
- 首屏保留“非官方”和“Windows 未签名 Beta”
- 比较 GIF 与静态首图的点击、评论质量和安装反馈
- 收集“不看 README 是否理解产品”的自然反馈

**当日输出：** 确定后续英文帖使用的最佳一句话介绍和首图。

### Day 4：掘金，发布实现复盘

- 发布完整技术文章，加入本机数据流、跨平台窗口跟随和诊断设计
- 链接到源码中的具体实现与隐私章节
- 在结尾征集 Windows ARM64、DPI、多显示器测试
- 对 Day 1–3 的问题做诚实复盘，不包装成“完美上线”

**继续门槛：** 中文安装说明已覆盖重复问题，严重 Issue 已关闭或有明确规避方案。

### Day 5：Reddit r/codex，开始英文反馈

- 当天检查版规、置顶帖和自我推广要求
- 使用英文长文版与 GIF，避免只有链接的帖子
- 如版规要求集中到 megathread，则在对应主题内发布精简版
- 明确 Windows unsigned beta、macOS not notarized、unofficial / no affiliation
- 根据时区安排发布后持续回复

**暂停条件：** SmartScreen 或 Gatekeeper 成为多数用户的阻断，或英文 README 无法独立完成安装。

### Day 6：OpenAI Developer Community

- 使用兼容性反馈版，不寻求或暗示 OpenAI 背书
- 重点说明 `account/rateLimits/read`、API Key 模式限制和不读取 token
- 链接现有兼容性 Issue，邀请提供系统与 Codex 版本
- 汇总英文用户的问题并修正文档

**当日输出：** 一份已知问题清单和签名 / 分发优先级判断。

### Day 7：Show HN，或选择延期

只有在前六天核心安装流程稳定、README 英文首屏清晰、作者能够持续回复时才发布 Show HN。使用技术叙事版，保持仓库无需登录即可查看和下载。

如果以下任一条件成立，则延期 Show HN，用当天修复问题：

- 严重崩溃、额度错误或隐私问题仍未解决
- Windows “无法获取额度”没有可执行的诊断路径
- 安装风险说明仍被多名用户误解
- README 首屏缺少演示、下载或 Windows Beta 提示
- 发布者当天无法及时参与讨论

### Product Hunt：不放入当前未签名的 7 天扩散

Product Hunt 文案和 gallery 可以在本周准备，但默认在下一个阶段发布。建议满足以下条件后再排期：

- 至少一个平台具备低摩擦可信分发路径，例如签名安装包或正式商店页面
- Windows 若仍未签名，必须作为 Early Access / Beta 清晰标注，并在首图可见
- 来自软启动的高频安装问题已解决
- 有真实 macOS 和 Windows 演示素材
- 能在发布当天持续回复，并准备公开 roadmap

若上述条件未满足，不要为了完成日程而发布 Product Hunt。

## 5. 每日操作模板

### 发布前 30 分钟

- [ ] 拉取最新默认分支并确认 CI 绿色
- [ ] 在无登录窗口打开仓库、Release 和 Issues
- [ ] 检查平台当日规则与正确分区
- [ ] 用对应 UTM 替换 `{CHANNEL_URL}`
- [ ] 搜索并清除全部占位符
- [ ] 检查正文包含非官方、Windows Beta / SmartScreen、本机读取三项说明
- [ ] 重新检查图片隐私信息
- [ ] 保存最终正文和发布时间

### 发布后 0–2 小时

- [ ] 回答安装和隐私问题
- [ ] 将可复现问题转为 GitHub Issue
- [ ] 提醒用户不要粘贴 token 或 `auth.json`
- [ ] 不删除合理批评，不与用户争论系统安全提示
- [ ] 对暂时无法解决的问题给出状态和下一次更新点

### 发布后 24 小时

- [ ] 记录独立访客、来源、各平台下载量和 Issue
- [ ] 按平台统计安装成功与阻断
- [ ] 汇总重复 3 次以上的问题
- [ ] 更新 README / FAQ / 已知问题
- [ ] 决定继续、维持当前范围或暂停下一渠道

## 6. 暂停与事件响应

出现以下任一情况，暂停所有新渠道发布：

- 安装包哈希与 Release 公布值不一致
- 应用或诊断信息暴露凭证、token、私有路径或不必要的个人信息
- 大范围显示错误额度，可能误导用户决策
- 发布资产被替换、仓库账号异常或下载链接指向非预期文件
- macOS / Windows 核心路径出现高比例崩溃
- 对外文案错误声称官方背书、已签名或已上架

处理顺序：撤下错误链接或文案 → 在仓库发布清晰说明 → 保留证据并定位影响版本 → 修复与发布新哈希 → 在原渠道更新同一条帖子的状态。不要用删帖代替透明说明。

## 7. Product Hunt 前的第二阶段清单

- [ ] 决定签名与商店分发路线，并只描述已经完成的状态
- [ ] 完成产品 icon、gallery、GIF / MP4 和英文 landing copy
- [ ] 准备 30 秒以内安装演示
- [ ] 将 Windows Beta 状态放在下载按钮附近
- [ ] 根据软启动数据挑选 3–5 个真实、可核验的用户反馈；未经许可不引用姓名或头像
- [ ] 准备公开 roadmap 和已知限制
- [ ] 选择发布日期，确保至少一名维护者全天可回复
- [ ] 对 Product Hunt 最终页面、外链、下载和追踪参数做无登录测试
