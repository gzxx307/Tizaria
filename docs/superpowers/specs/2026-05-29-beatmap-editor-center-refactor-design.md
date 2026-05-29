# BeatmapEditor 中心编辑区 GameObject 化重构设计

## 背景

当前中心编辑区 5 个渲染器全部使用 RawImage + Texture2D 逐像素绘制。问题：
- FilterMode.Point 导致锯齿严重
- 浮点数坐标舍入导致节拍线粗细不均匀
- 纹理重建频繁，波形渲染需协程

## 目标

完全移除纹理渲染逻辑，改为 GameObject 化 + 虚拟化滚动，解决锯齿和线宽问题。

## 约束

- 歌曲最坏情况：8 分钟 @ 200BPM，beatDivision=16 → ~25,600 条细分线
- 布局不变：水平排列的时间标尺 | 波形 | BPM曲线 | SV曲线 | 音符网格
- Unity 2021+ 内置 ObjectPool

---

## 架构

```
Center
├── MainScrollView (ScrollRect, 不变)
│   └── Viewport (Mask, 不变)
│       └── Content (HLG, 不变)
│           ├── TimeRulerView     — 池化刻度线 Image + TMP 标签
│           ├── WaveformView      — 池化振幅竖条 Image
│           ├── BpmCurvePanel
│           │   ├── BpmCurveView       — 池化旋转线段 Image + 关键帧点
│           │   └── BpmKeyframeContainer  (保持现有)
│           ├── SvCurvePanel
│           │   ├── SvCurveView        — 池化旋转线段 Image + 关键帧点
│           │   └── SvKeyframeContainer   (保持现有)
│           └── NoteGridPanel
│               ├── BeatGridView       — 池化横线/竖线 Image
│               └── NotesContainer     (保持现有)
└── Playhead (保持现有)
```

### 数据流

```
BeatmapEditorController
  ├─ 数据变更 → RefreshAll()
  │               → 各 Renderer.Render(dataChanged:true, ...)
  └─ 滚动/缩放 → RefreshRenderers()
                  → 各 Renderer.Render(dataChanged:false, ...)
```

### 核心接口

```csharp
public interface IVirtualizedRenderer
{
    // dataChanged: true=数据变了(重建所有视觉), false=仅滚动/缩放(只重定位)
    void Render(bool dataChanged, int totalMs, float pixelsPerMs,
                int visibleStartMs, int visibleEndMs);
}
```

所有新渲染器实现此接口。Controller 通过接口调用，方便增删条带。

### 坐标反算

每个渲染器暴露：
```csharp
public int TimeAtScreenPoint(Vector2 screenPoint);
```

为后续点击面板创建关键帧预留。

---

## 虚拟化策略

- Content 和各 Strip 保持全高（`totalMs * pixelsPerMs`），ScrollRect 滚动条行为不变
- 激活仅 `visibleStartMs~visibleEndMs + 20% 缓冲区` 内的 GameObject
- 区外对象 `SetActive(false)` 回池
- 密度过滤：相邻元素屏幕间距 < 2px 时跳过（小节线始终显示）
- 使用 `UnityEngine.Pool.ObjectPool<T>`

---

## 渲染器设计

### 1. BeatGridRenderer

| 元素 | 类型 | 尺寸 | 说明 |
|------|------|------|------|
| 小节线 | Image | 高3px, 宽100% | 始终显示 |
| 节拍线 | Image | 高2px, 宽100% | 间距 < 2px 时跳过 |
| 细分线 | Image | 高1px, 宽100% | 间距 < 2px 时跳过 |
| 列分隔线 | Image | 宽2px, 高100% | 数量 = columnCount+1 |

4 个 `ObjectPool<GameObject>`，对应 4 种线型。每个预制模板带 RectTransform + Image + LayoutElement(ignoreLayout=true)。

渲染：从 visibleStartMs 向 visibleEndMs 迭代 BPM 段，逐细分推进，双精度计算时间。

### 2. TimeRulerRenderer

| 元素 | 尺寸 | 说明 |
|------|------|------|
| 大刻度线 | 宽100%, 高1px | 整秒，密度降级时始终显示 |
| 中刻度线 | 宽50%, 高1px | 0.5秒 |
| 小刻度线 | 宽30%, 高1px | 0.25秒，间距 < 2px 跳过 |
| 标签 | TextMeshProUGUI | 仅大刻度处，自适应间隔(1s/5s/10s/30s/60s) |

1 个线池 + 1 个标签池。

### 3. WaveformRenderer

| 元素 | 尺寸 | 说明 |
|------|------|------|
| 振幅竖条 | 宽3px, 高=振幅 | 颜色按阈值区分 peak/普通 |
| 中心线 | 高2px, 宽100% | 单例，不池化 |

1 个竖条池。可见时间范围等分为时间桶，每桶取 max amplitude 决定竖条高度。中心线始终存在。

### 4. BpmCurveRenderer / SvCurveRenderer

| 元素 | 说明 |
|------|------|
| 线段 | 高2px 的 Image，pivot 左中，绕 Z 轴旋转连接两关键帧 |
| 关键帧点 | 小圆点 Image (8x8)，可点击 |
| 中心线(SV) | SV=1.0 处竖线，单例 |

1 个线段池 + 1 个关键帧点池。找出可视区涉及的相邻关键帧对，逐对生成线段。BPM 无中心线，SV 中心线为单例。

关键帧点 `raycastTarget=true`，预留点击交互（本次不实现 BpmSvInteraction）。

---

## 文件清单

| 文件 | 操作 |
|------|------|
| `Scripts/UI/BeatmapEditorScene/IVirtualizedRenderer.cs` | 新建 |
| `Scripts/UI/BeatmapEditorScene/BeatGridRenderer.cs` | 重写 |
| `Scripts/UI/BeatmapEditorScene/TimeRulerRenderer.cs` | 重写 |
| `Scripts/UI/BeatmapEditorScene/WaveformRenderer.cs` | 重写 |
| `Scripts/UI/BeatmapEditorScene/BpmCurveRenderer.cs` | 重写 |
| `Scripts/UI/BeatmapEditorScene/SvCurveRenderer.cs` | 重写 |
| `Scripts/UI/BeatmapEditorScene/BeatmapEditorController.cs` | 微调 |
| `Editor/BeatmapEditorSceneBuilder.cs` | 微调 |
| `Scripts/UI/BeatmapEditorScene/BpmSvInteraction.cs` | 预留(空文件) |

---

## 扩展预留

| 扩展需求 | 设计支撑 |
|---------|---------|
| 点击 BPM/SV 面板创建关键帧 | 透明输入层 + `TimeAtScreenPoint()` → Controller |
| Hold 长条创建/删除 | NoteGridInteraction 已有，不变 |
| 面板高度缩放 | `dataChanged=false` 路径已区分，仅重定位 |
| 条带增删 | `IVirtualizedRenderer` 接口，Controller 遍历调用 |

## 不在本次范围

- `BpmSvInteraction.cs` 实现（留空文件）
- KeyframeMarker 改动（保持现有）
- NoteGridInteraction 改动（保持现有）
