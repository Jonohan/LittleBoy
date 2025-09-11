# Boss部件系统使用指南

## 概述

Boss部件系统管理一个唯一的Boss部件，该部件可以动态移动到传送门位置进行攻击。系统设计为每次攻击流程获取最新传送门位置，将Boss部件移动到传送门处进行攻击。

## 核心组件

### 1. BossPart
- **功能**: 唯一的Boss部件，包含攻击和受击判定
- **特性**: 
  - 可以独立激活/非激活
  - 有自己的攻击伤害值
  - 可以接收玩家攻击并传递给Boss
  - 支持音效和视觉效果

### 2. BossPartManager
- **功能**: 管理唯一的Boss部件
- **特性**:
  - 管理单个Boss部件
  - 动态移动到传送门位置
  - 控制部件攻击
  - 与传送门系统集成

## 使用方法

### 1. 设置Boss部件

1. 在场景中创建独立的GameObject作为Boss部件
2. 为部件添加 `BossPart` 组件（**不需要先添加Collider组件**）
3. 设置部件的标签为 "BossPart"（用于自动查找）
4. **配置Boss连接**（推荐方式）：
   - 直接拖拽Boss对象到 `Boss Object` 字段
   - 系统会自动获取BossBlackboard组件
5. 配置其他部件参数：
   - `partName`: 部件名称（用于识别）
   - `attackDamage`: 攻击伤害值
   - `damageMultiplier`: 受击伤害倍数
   - `canAttack`: 是否具有攻击能力
   - `canReceiveDamage`: 是否可以被攻击
6. **重要**: 现在可以灵活选择Body part Collider，不需要强制在组件自身添加Collider

### 2. 设置BossPartManager

1. 在Boss对象上添加 `BossPartManager` 组件
2. 启用 `autoFindPart` 选项
3. 设置 `bossPartTag` 为 "BossPart"（与部件标签匹配）
4. 设置传送门管理器引用
5. 配置移动参数：
   - `useInstantMovement`: 是否瞬间移动到传送门位置（推荐开启）

### 3. 配置碰撞器

#### 攻击碰撞器
- 设置为触发器，用于检测玩家碰撞
- 可以通过Inspector手动指定或自动使用组件自身的碰撞器

#### Body Part Collider（受击碰撞器）
- **自动设置**: 启用 `autoSetDamageCollider` 选项，系统会自动使用组件自身的碰撞器
- **手动选择**: 禁用 `autoSetDamageCollider` 选项，然后手动拖拽指定的碰撞器到 `damageCollider` 字段
- **重要**: 必须设置为普通碰撞器（非触发器），用于接收玩家攻击

#### 调试功能
BossPart组件提供了以下调试按钮来帮助配置：

**Boss连接调试：**
- **自动查找Boss对象**: 自动查找Tag为"enemy"的Boss对象
- **验证Boss连接**: 检查Boss对象和BossBlackboard连接是否正常

**Body Part Collider调试：**
- **自动设置Body Part Collider**: 自动使用组件自身的碰撞器
- **清除Body Part Collider**: 清除当前设置的碰撞器
- **验证Body Part Collider设置**: 检查碰撞器配置是否正确

### 4. 在Boss Task中使用

在Boss的技能任务中直接调用BossPartManager的方法：

```csharp
// 获取BossPartManager
var partManager = GetComponent<BossPartManager>();

// 移动到传送门位置并攻击
partManager.MoveToPortalAndAttack();

// 激活部件
partManager.ActivatePart();

// 激活攻击
partManager.ActivatePartAttack();

// 关闭攻击
partManager.DeactivatePartAttack();
```

### 5. 通过BossBlackboard调用

```csharp
// 通过BossBlackboard调用
bossBlackboard.MoveBossPartToPortalAndAttack();
bossBlackboard.ActivateBossPart();
bossBlackboard.ActivateBossPartAttack();
```

## 攻击流程

### 标准攻击流程：
1. **前摇阶段**: 生成传送门
2. **获取位置**: 调用 `GetLatestPortalPosition()` 获取最新传送门位置
3. **移动部件**: 调用 `MoveToPosition()` 将Boss部件移动到传送门位置
4. **激活攻击**: 调用 `ActivatePartAttack()` 激活攻击判定
5. **后摇阶段**: 调用 `DeactivatePartAttack()` 关闭攻击判定

### 一键攻击方法：
```csharp
// 一键完成移动和攻击
partManager.MoveToPortalAndAttack();
```

## 伤害系统集成

### 攻击玩家
- 当玩家的碰撞器进入Boss部件的攻击碰撞器时
- 系统会自动创建 `vDamage` 对象并传递给玩家的 `vHealthController`
- 伤害值由部件的 `attackDamage` 参数决定

### 接收玩家攻击
- 当玩家攻击Boss部件时
- 部件会接收 `vDamage` 对象
- 应用 `damageMultiplier` 倍数后传递给Boss的 `vHealthController`
- 这样玩家攻击部件就能对Boss造成伤害

## 配置建议

### 1. 移动配置
- **移动时间**: 建议设置为1-2秒，给玩家反应时间
- **平滑移动**: 启用平滑移动提供更好的视觉效果
- **移动速度**: 根据游戏节奏调整

### 2. 攻击配置
- **攻击伤害**: 应该与Boss的整体强度匹配
- **受击伤害倍数**: 可以用来创建弱点机制

### 3. 视觉效果
- 为激活/非激活状态配置不同的视觉效果
- 为受击配置特效，增强反馈

### 4. 音效设计
- 为不同状态配置音效
- 增强战斗的沉浸感

## 调试功能

BossPartManager提供了丰富的调试按钮：
- 激活/非激活部件
- 激活/关闭攻击
- 移动到传送门并攻击
- 显示部件状态

## 注意事项

1. **碰撞器设置**: 确保攻击碰撞器是触发器，受击碰撞器不是触发器
2. **层级设置**: 确保碰撞器在正确的层级上
3. **传送门引用**: 确保BossPartManager正确引用PortalManager
4. **部件唯一性**: 确保场景中只有一个BossPart实例

## 扩展建议

1. **攻击模式**: 可以扩展不同的攻击模式（如连续攻击、蓄力攻击）
2. **移动轨迹**: 实现更复杂的移动轨迹（如弧形移动）
3. **动态配置**: 根据Boss阶段动态调整移动和攻击参数
4. **AI集成**: 让AI根据玩家位置智能选择传送门位置