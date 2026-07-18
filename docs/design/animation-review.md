# VibeController 动效复查

复查基线：`review-animations`。界面属于高频生产力工具，因此页面切换和手柄输入反馈保持即时，仅保留指针按压反馈。

| Before | After | Why |
| --- | --- | --- |
| 按钮同时过渡 `transform`、背景色和阴影 | 仅过渡 `transform 120ms var(--ease-out)` | 高频按钮避免不必要的绘制；120ms、`scale(0.97)` 符合短促按压反馈范围 |
| 手柄输入可能沿用按钮过渡 | 手柄 SVG 的 `data-pressed` 状态不设置 transition | 实体按键反馈必须在每次输入时立即更新，不允许动画阻塞或产生拖影 |
| 默认系统动态效果 | `prefers-reduced-motion` 将移动过渡降至近即时 | 保留状态变化，同时取消可感知位移 |

## Verdict

- 性能：没有 `transition: all`、关键帧或布局属性动画；唯一过渡为 GPU 友好的 `transform`。
- 可打断性与时序：按钮使用 CSS transition，可从当前状态立即反向；时长 120ms。
- 可访问性：实现减少动态效果、高对比度、减少透明度与键盘焦点样式。

**Approve** — 未发现会破坏手感的高频动画或 P0/P1 动效问题。
