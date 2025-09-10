# 传送门插槽设置指南

## 🎯 新的传送门系统设计

根据您的需求，传送门系统现在包含以下特性：

### 1. **插槽平面系统**
- 每个插槽都是一个平面，用于VFX播放
- VFX在平面上移动，追踪玩家位置
- 传送门从场景中的固定位置移动到插槽位置

### 2. **三阶段生成流程**
1. **生成阶段**: VFX在插槽平面上持续播放，追踪玩家
2. **前摇阶段**: 场景中的传送门开始移动到插槽位置
3. **激活阶段**: 传送门就位，可以正常使用

## 📋 设置步骤

### 第一步：创建传送门插槽

#### 1. 创建插槽GameObject
```
1. 在场景中创建空GameObject，命名为"PortalSlot_Ceiling_01"
2. 添加PortalSlot组件
3. 配置插槽类型为Ceiling
```

#### 2. 设置插槽平面
```
1. 创建子物体，命名为"SlotPlane"
2. 添加一个Quad或Plane作为视觉参考
3. 将SlotPlane的Transform拖拽到PortalSlot的Slot Plane字段
```

#### 3. 设置VFX生成点
```
1. 创建子物体，命名为"VfxSpawnPoint"
2. 放置在插槽平面的中心
3. 将VfxSpawnPoint的Transform拖拽到PortalSlot的Vfx Spawn Point字段
```

#### 4. 设置场景传送门
```
1. 在场景中创建传送门对象，命名为"ScenePortal_Ceiling"
2. 放置在远离插槽的位置（原始位置）
3. 将传送门对象拖拽到PortalSlot的Scene Portal字段
4. 初始状态下设置为非激活状态
```

### 第二步：配置PortalSlot参数

#### 基础配置
```csharp
// 在Inspector中设置：
- Slot Type: Ceiling/WallLeft/WallRight/Ground
- Slot Plane: 插槽平面Transform
- Vfx Spawn Point: VFX生成点Transform
- Scene Portal: 场景传送门GameObject
```

#### VFX配置
```csharp
// VFX预制体：
- Generating Vfx Prefab: 生成阶段VFX
- Telegraphing Vfx Prefab: 前摇阶段VFX
- Vfx Move Speed: 2.0 (VFX移动速度)
- Player Tracking Strength: 0.5 (追踪强度)
```

#### 传送门配置
```csharp
// 传送门移动：
- Portal Move Speed: 5.0 (传送门移动速度)
- Portal Move Curve: 移动曲线
```

#### 目标追踪
```csharp
// 玩家追踪：
- Player Target: 玩家Transform
- Tracking Range: 10.0 (追踪范围)
```

### 第三步：创建VFX预制体

#### 1. 生成阶段VFX
```
1. 创建GameObject，命名为"Vfx_Generating"
2. 添加粒子系统或动画
3. 设置为蓝色/橙色/红色（根据传送门类型）
4. 保存为预制体
```

#### 2. 前摇阶段VFX
```
1. 创建GameObject，命名为"Vfx_Telegraphing"
2. 添加预警特效（如红色圆圈、警告标志等）
3. 保存为预制体
```

### 第四步：配置PortalManager

#### 插槽数组配置
```csharp
// 在PortalManager中：
- Ceiling Slots: 拖拽所有天花板插槽
- Wall Left Slots: 拖拽所有左墙插槽
- Wall Right Slots: 拖拽所有右墙插槽
- Ground Slots: 拖拽所有地面插槽
```

## 🎮 使用流程

### 1. 技能开始
```csharp
// BossSkillTask调用：
_portalManager.StartPortalGeneration(PortalType.Ceiling, PortalColor.Blue);
```

### 2. 生成阶段
- VFX在插槽平面上播放
- VFX追踪玩家位置
- 持续播放直到前摇阶段

### 3. 前摇阶段
```csharp
// BossSkillTask调用：
_portalManager.StartPortalTelegraphing(portalData, telegraphTime);
```

### 4. 传送门移动
- 场景中的传送门开始移动到插槽位置
- 移动过程有动画曲线
- 移动完成后传送门激活

### 5. 技能执行
- 传送门就位，可以正常使用
- 执行技能效果
- 技能结束后传送门关闭

## 🔧 调试功能

### PortalSlot调试按钮
```csharp
// 在Inspector中：
- Test Start Generating: 测试生成阶段
- Test Start Telegraphing: 测试前摇阶段
- Test Close Portal: 测试关闭传送门
- Test Reset Slot: 重置插槽
```

### 可视化调试
- Scene视图中显示插槽平面
- 显示追踪范围
- 显示VFX生成点位置

## 📝 示例配置

### 天花板插槽示例
```
PortalSlot_Ceiling_01:
├── Slot Type: Ceiling
├── Slot Plane: 指向下方的平面
├── Vfx Spawn Point: 平面中心
├── Scene Portal: 场景中的天花板传送门
├── Generating Vfx: 蓝色粒子效果
└── Telegraphing Vfx: 红色预警圆圈
```

### 地面插槽示例
```
PortalSlot_Ground_01:
├── Slot Type: Ground
├── Slot Plane: 指向上方的平面
├── Vfx Spawn Point: 平面中心
├── Scene Portal: 场景中的地面传送门
├── Generating Vfx: 橙色粒子效果
└── Telegraphing Vfx: 橙色预警圆圈
```

## 🚀 测试建议

1. **先创建单个插槽**：测试基本功能
2. **配置VFX预制体**：确保视觉效果正确
3. **测试传送门移动**：验证移动动画
4. **测试玩家追踪**：确保VFX正确追踪玩家
5. **创建多个插槽**：测试插槽管理系统
6. **集成到技能系统**：测试完整的技能流程

这样您就可以创建一个完整的、具有视觉冲击力的传送门系统了！
