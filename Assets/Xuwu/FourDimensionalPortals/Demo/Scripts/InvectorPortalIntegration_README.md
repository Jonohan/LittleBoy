# Invector传送门集成指南

## 概述
这个适配器系统让您可以在不修改invector核心代码的情况下，让invector控制器支持传送门功能。

## 快速设置

### 1. 为角色添加传送门适配器
1. 选择您的invector角色对象
2. 添加 `InvectorPortalAdapter` 组件
3. 组件会自动配置所需设置

### 2. 相机设置
相机无需额外适配器。请确保主相机上存在 `PortalSystemAdditionalCameraData` 组件（系统会自动添加或由角色适配器更新）。

### 3. 配置设置
- **Auto Configure Portal Settings**: 自动配置传送门参数（推荐开启）
- **Transfer Pivot Offset**: 传送门检测点偏移（通过基类属性设置）
- **Clone Layer**: 传送门克隆渲染层（通过基类属性设置）
- **Portal System Camera Data**: 传送门相机数据组件

## 功能特性

### ✅ 支持的功能
- 传送门穿越
- 传送门穿透视图
- 重力方向变化处理
- 速度转换
- 相机系统集成
- 动画状态保持

### ❌ 不支持的功能
- 输入系统修改（保持invector原有输入）
- 复杂的传送门动画
- 自定义传送门效果

## 使用方法

### 基本使用
1. 确保场景中有传送门对象
2. 角色会自动检测并穿越传送门
3. 相机会自动处理传送门穿透视图

### 高级使用
```csharp
// 获取适配器组件
var adapter = GetComponent<InvectorPortalAdapter>();

// 手动触发传送门穿越
adapter.ForcePortalTransition(targetPortal);

// 检查是否在传送门中
bool inPortal = adapter.PenetratingPortal != null;
```

## 故障排除

### 常见问题

**Q: 角色无法穿越传送门**
A: 检查：
- 角色是否有Rigidbody组件
- 传送门是否正确配置
- 角色是否在传送门检测层

**Q: 相机效果不正常**
A: 检查：
- PortalSystemAdditionalCameraData是否正确配置
- 相机剔除遮罩设置

**Q: 穿越后角色状态异常**
A: 检查：
- 传送门配置是否正确
- 重力设置是否合适
- 动画状态是否正常

### 调试技巧
1. 在Scene视图中查看传送门检测点（青色球体）
2. 检查Console中的警告信息
3. 使用Gizmos查看传送门连接

## 性能优化

### 建议设置
- 合理设置传送门检测层
- 限制传送门数量
- 优化相机剔除遮罩

### 性能监控
- 监控传送门检测频率
- 检查相机渲染调用
- 观察内存使用情况

## 扩展开发

### 自定义传送门效果
```csharp
public class CustomPortalEffect : MonoBehaviour
{
    public void OnPortalEnter(Portal portal)
    {
        // 自定义进入传送门效果
    }
    
    public void OnPortalExit(Portal portal)
    {
        // 自定义离开传送门效果
    }
}
```

### 集成其他系统
- 音效系统
- 粒子效果
- UI系统
- 游戏逻辑

## 版本兼容性
- Unity 2022.3+
- Invector 3rd Person Controller
- Four Dimensional Portals

## 支持
如有问题，请检查：
1. Unity Console错误信息
2. 组件配置是否正确
3. 传送门系统是否正常工作
