using UnityEngine;
using Invector.vCharacterController;
using System.Collections.Generic;
using Invector.vCamera;
using Sirenix.OdinInspector;

namespace Xuwu.Character
{
    /// <summary>
    /// 角色体型等级枚举
    /// </summary>
    public enum CharacterSizeLevel
    {
        Mini = 1,        // 1级：迷你体型（0.5倍）
        Standard = 2,    // 2级：标准体型（1倍）
        Giant = 3,       // 3级：巨大体型（2倍）
        LimitBreaker = 4 // 4级：限制器突破（4.1~4.5级，最多5次）
    }
    
    /// <summary>
    /// 角色体型控制器 - 安全地调整Invector角色体型而不破坏控制器功能
    /// 支持4个体型等级和传送门体型变化系统
    /// </summary>
    [AddComponentMenu("Xuwu/Character/Character Size Controller")]
    [RequireComponent(typeof(vThirdPersonMotor))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterSizeController : MonoBehaviour
    {
        [Header("体型设置")]
        [Range(0.5f, 6.0f)]
        [Tooltip("体型缩放倍数，1.0为原始大小")]
        public float sizeMultiplier = 1.0f;
        
        [Header("体型等级系统")]
        [Tooltip("当前体型等级")]
        public CharacterSizeLevel currentSizeLevel = CharacterSizeLevel.Standard;
        
        [Tooltip("限制器突破等级（4.1~4.5，最多5次）")]
        [Range(1, 5)]
        public int limitBreakerLevel = 1;
        
        [Header("体型等级效果")]
        [Tooltip("体力消耗倍率（1级迷你体型时减少）")]
        [Range(0.1f, 2.0f)]
        public float staminaConsumptionMultiplier = 1.0f;
        
        [Tooltip("攻击伤害倍率（3级巨大体型和4级限制器突破时提升）")]
        [Range(0.1f, 5.0f)]
        public float attackDamageMultiplier = 1.0f;
        
        [Header("免疫状态")]
        [Tooltip("免疫异能轰炸攻击（1级迷你体型）")]
        public bool immuneToEnergyBombardment = false;
        
        [Tooltip("免疫异能洪水攻击（3级巨大体型）")]
        public bool immuneToEnergyFlood = false;
        
        [Tooltip("限制器突破状态（4级）")]
        public bool isLimitBreakerActive = false;
        
        [Header("体型等级独立乘区")]
        [Tooltip("按体型等级叠乘在体型倍数上的移动速度独立倍率")] 
        [Range(0.1f, 5.0f)]
        public float levelMoveBonusMultiplier = 1.0f;
        [Tooltip("按体型等级叠乘在体型倍数上的跳跃高度独立倍率")] 
        [Range(0.1f, 5.0f)]
        public float levelJumpBonusMultiplier = 1.0f;
        
        [Header("动画设置")]
        [Tooltip("是否同时调整动画播放速度")]
        public bool adjustAnimationSpeed = false;
        
        [Tooltip("是否同时调整移动速度")]
        public bool adjustMovementSpeed = true;
        
        [Tooltip("是否同时调整跳跃高度")]
        public bool adjustJumpHeight = true;
        
        [Header("相机设置")]
        [Tooltip("是否同时调整相机距离")]
        public bool adjustCameraDistance = true;
        
        [Header("地面检测设置")]
        [Tooltip("是否强制重新计算地面检测")]
        public bool forceGroundRecalculation = true;
        
        [Header("组件引用")]
        [SerializeField] private vThirdPersonMotor _motor;
        [SerializeField] private CapsuleCollider _capsuleCollider;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _cameraTarget;
        
        [Header("冷却设置")]
        [Tooltip("每次体型变化的冷却时间（秒）")]
        [Range(0f, 10f)]
        public float sizeChangeCooldown = 1.0f;
        private float _lastSizeChangeTime = -999f;
        
        [Header("视觉模型根（分布缩放）")]
        [Tooltip("第一个需要缩放的子物体，默认自动查找 '3D Model'")]
        [SerializeField] private Transform _modelRootA;
        [Tooltip("第二个需要缩放的子物体，默认自动查找 'Invector Components'")]
        [SerializeField] private Transform _modelRootB;
        [SerializeField] private string _modelChildNameA = "3D Model";
        [SerializeField] private string _modelChildNameB = "Invector Components";
        
        // 原始值存储
        private float _originalCapsuleHeight;
        private float _originalCapsuleThickness;
        private Vector3 _originalCapsuleOffset;
        private float _originalWalkSpeed;
        private float _originalRunSpeed;
        private float _originalSprintSpeed;
        private float _originalCrouchSpeed;
        private float _originalJumpHeight;
        private float _originalAnimationSpeed;
        private Vector3 _originalLocalScale;
        private float _originalCameraDistance;
        private Vector3 _originalModelLocalScaleA;
        private Vector3 _originalModelLocalScaleB;
        
        // Invector vThirdPersonCamera 距离同步
        private float _originalTPCameraDistance;
        private float _lastAppliedTPCameraDistance;
        private bool _cachedTPCamera;
        
        [Header("相机状态缩放")]
        [Tooltip("是否按体型倍数同步缩放所有CameraState的min/max/default（更稳，不与状态机冲突）")]
        public bool scaleCameraStates = true;
        [Tooltip("是否同时按体型倍数缩放CameraState的height参数")]
        public bool scaleCameraStateHeight = true;
        [Tooltip("相机距离缩放倍率（统一用于所有相机调整）")]
        [Range(0.1f, 6.0f)]
        public float cameraDistanceMultiplier = 1.0f;
        [Tooltip("相机高度缩放倍率（用于CameraState的height参数）")]
        [Range(0.1f, 6.0f)]
        public float cameraHeightMultiplier = 1.0f;
        private readonly Dictionary<Invector.vThirdPersonCameraState, (float def,float min,float max,float height)> _cameraStateBase = new Dictionary<Invector.vThirdPersonCameraState, (float def, float min, float max, float height)>();
        
        // 相机multiplier系统
        private float _currentCameraMultiplier = 1.0f;
        private bool _cameraMultiplierApplied = false;
        
        // 初始相机缩放参数（启动时自动应用）
        private bool _hasAppliedInitialCameraScale = false;
        
        // 地面检测参数
        private float _originalGroundMinDistance;
        private float _originalGroundMaxDistance;
        private float _originalSphereCastRadius;
        private float _originalCastLengthAirborne;
        private float _originalCastLengthGrounded;
        private float _originalStepHeight;
        private float _originalPlaneSize;
        
        // 当前状态
        private bool _isInitialized = false;
        private float _currentSizeMultiplier = 1.0f;
        
        #region Unity生命周期
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void Start()
        {
            SaveOriginalValues();
            ApplySizeChange(sizeMultiplier);
            _isInitialized = true;
            // 延迟缓存 & 应用第三人称相机的distance（相机通常在Start期间绑定目标）
            StartCoroutine(CacheAndScaleTPCameraCoroutine());
        }
        
        private void OnDestroy()
        {
            // 游戏结束时重置相机multiplier系统
            ResetCameraMultiplierSystem();
        }
        
        private void OnValidate()
        {
            if (_isInitialized && Application.isPlaying)
            {
                ApplySizeChange(sizeMultiplier);
            }
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            if (!_motor) _motor = GetComponent<vThirdPersonMotor>();
            if (!_capsuleCollider) _capsuleCollider = GetComponent<CapsuleCollider>();
            if (!_animator) _animator = GetComponent<Animator>();
            
            // 尝试找到相机目标
            if (!_cameraTarget)
            {
                var cameraController = FindObjectOfType<vThirdPersonCamera>();
                if (cameraController)
                {
                    _cameraTarget = cameraController.transform;
                }
            }
            
            // 自动查找要缩放的两个子物体
            if (!_modelRootA)
                _modelRootA = FindChildByName(transform, _modelChildNameA);
            if (!_modelRootB)
                _modelRootB = FindChildByName(transform, _modelChildNameB);
            
            // 验证必需组件
            if (!_motor)
            {
                Debug.LogError($"[CharacterSizeController] 未找到vThirdPersonMotor组件在 {gameObject.name}");
                enabled = false;
                return;
            }
            
            if (!_capsuleCollider)
            {
                Debug.LogError($"[CharacterSizeController] 未找到CapsuleCollider组件在 {gameObject.name}");
                enabled = false;
                return;
            }
        }
        
        /// <summary>
        /// 保存原始值
        /// </summary>
        private void SaveOriginalValues()
        {
            if (!_motor) return;
            
            // 碰撞体参数
            _originalCapsuleHeight = _motor.capsuleHeight;
            _originalCapsuleThickness = _motor.capsuleThickness;
            _originalCapsuleOffset = _motor.capsuleOffset;
            
            // 移动速度
            _originalWalkSpeed = _motor.freeSpeed.walkSpeed;
            _originalRunSpeed = _motor.freeSpeed.runningSpeed;
            _originalSprintSpeed = _motor.freeSpeed.sprintSpeed;
            _originalCrouchSpeed = _motor.freeSpeed.crouchSpeed;
            
            // 跳跃高度
            _originalJumpHeight = _motor.jumpHeight;
            
            // 动画速度
            _originalAnimationSpeed = _animator ? _animator.speed : 1.0f;
            
            // 原始缩放
            _originalLocalScale = transform.localScale;
            _originalModelLocalScaleA = _modelRootA ? _modelRootA.localScale : Vector3.one;
            _originalModelLocalScaleB = _modelRootB ? _modelRootB.localScale : Vector3.one;
            
            // 地面检测参数
            _originalGroundMinDistance = _motor.groundMinDistance;
            _originalGroundMaxDistance = _motor.groundMaxDistance;
            _originalSphereCastRadius = _motor.sphereCastRadius;
            _originalCastLengthAirborne = _motor.castLengthAirborne;
            _originalCastLengthGrounded = _motor.castLengthGrounded;
            _originalStepHeight = _motor.stepHeight;
            _originalPlaneSize = _motor.planeSize;
            
            // 相机距离（如果有相机目标）
            if (_cameraTarget)
            {
                _originalCameraDistance = Vector3.Distance(transform.position, _cameraTarget.position);
            }
            
        }
        
        #endregion
        
        #region 体型调整
        
        /// <summary>
        /// 应用体型变化
        /// </summary>
        /// <param name="newSizeMultiplier">新的缩放倍数</param>
        public void ApplySizeChange(float newSizeMultiplier)
        {
            if (!_motor || !_capsuleCollider) return;
            
            newSizeMultiplier = Mathf.Clamp(newSizeMultiplier, 0.3f, 6.0f);
            _currentSizeMultiplier = newSizeMultiplier;
            
            // 1. 调整碰撞体参数
            //AdjustCollider(newSizeMultiplier);
            
            // 2. 调整地面检测参数（重要！解决悬空和穿模问题）
            AdjustGroundDetection(newSizeMultiplier);
            
            // 3. 调整移动速度
            if (adjustMovementSpeed)
            {
                AdjustMovementSpeed(newSizeMultiplier);
            }
            
            // 4. 调整跳跃高度
            if (adjustJumpHeight)
            {
                //AdjustJumpHeight(newSizeMultiplier);
            }
            
            // 5. 调整动画速度
            /*
            if (adjustAnimationSpeed && _animator)
            {
                AdjustAnimationSpeed(newSizeMultiplier);
            }
            */
            
            // 6. 调整视觉缩放
            AdjustVisualScale(newSizeMultiplier);
            
            // 7. 调整相机距离
            if (adjustCameraDistance && _cameraTarget)
            {
                AdjustCameraDistance(newSizeMultiplier);
            }

        }
        
        /// <summary>
        /// 调整碰撞体参数
        /// </summary>
        private void AdjustCollider(float multiplier)
        {
            _motor.capsuleHeight = _originalCapsuleHeight * multiplier;
            _motor.capsuleThickness = _originalCapsuleThickness * multiplier;
            _motor.capsuleOffset = _originalCapsuleOffset * multiplier;
            
            // 强制更新碰撞体
            _capsuleCollider.height = _motor.capsuleHeight;
            _capsuleCollider.radius = _motor.capsuleThickness / 2f;
            // Invector 的实现：center = capsuleOffset * capsuleHeight
            _capsuleCollider.center = _motor.capsuleOffset * _motor.capsuleHeight;
        }
        
        /// <summary>
        /// 调整地面检测参数（解决悬空和穿模问题）
        /// </summary>
        private void AdjustGroundDetection(float multiplier)
        {
            // 调整地面检测距离
            _motor.groundMinDistance = _originalGroundMinDistance * multiplier;
            _motor.groundMaxDistance = _originalGroundMaxDistance * multiplier;
            
            // 调整球形检测半径
            _motor.sphereCastRadius = _originalSphereCastRadius * multiplier;
            
            // 调整检测长度
            _motor.castLengthAirborne = _originalCastLengthAirborne * multiplier;
            _motor.castLengthGrounded = _originalCastLengthGrounded * multiplier;
            
            // 调整台阶高度
            _motor.stepHeight = _originalStepHeight * multiplier;
            
            // 调整平面大小
            _motor.planeSize = _originalPlaneSize * multiplier;
            
            // 强制重新计算地面检测
            if (forceGroundRecalculation)
            {
                ForceGroundRecalculation();
            }
            
        }
        
        /// <summary>
        /// 强制重新计算地面检测
        /// </summary>
        private void ForceGroundRecalculation()
        {
            // 临时禁用控制器，然后重新启用以强制重新计算
            bool wasEnabled = _motor.enabled;
            _motor.enabled = false;
            
            // 等待一帧
            StartCoroutine(ReEnableMotorAfterFrame(wasEnabled));
        }
        
        private System.Collections.IEnumerator ReEnableMotorAfterFrame(bool wasEnabled)
        {
            yield return null; // 等待一帧
            _motor.enabled = wasEnabled;
        }
        
        /// <summary>
        /// 调整移动速度
        /// </summary>
        private void AdjustMovementSpeed(float multiplier)
        {
            _motor.freeSpeed.walkSpeed = _originalWalkSpeed * multiplier;
            _motor.freeSpeed.runningSpeed = _originalRunSpeed * multiplier;
            _motor.freeSpeed.sprintSpeed = _originalSprintSpeed * multiplier;
            _motor.freeSpeed.crouchSpeed = _originalCrouchSpeed * multiplier;
        }
        
        /// <summary>
        /// 调整跳跃高度
        /// </summary>
        private void AdjustJumpHeight(float multiplier)
        {
            _motor.jumpHeight = _originalJumpHeight * multiplier;
        }
        
        /// <summary>
        /// 调整动画速度
        /// </summary>
        private void AdjustAnimationSpeed(float multiplier)
        {
            if (_animator)
            {
                _animator.speed = _originalAnimationSpeed * multiplier;
            }
        }
        
        /// <summary>
        /// 调整视觉缩放
        /// </summary>
        private void AdjustVisualScale(float multiplier)
        {
            bool anyApplied = false;
            if (_modelRootA)
            {
                _modelRootA.localScale = _originalModelLocalScaleA * multiplier;
                anyApplied = true;
            }
            if (_modelRootB)
            {
                _modelRootB.localScale = _originalModelLocalScaleB * multiplier;
                anyApplied = true;
            }
            if (!anyApplied)
            {
                // 回退：若未指定子物体，则缩放整体
                transform.localScale = _originalLocalScale * multiplier;
            }
        }
        
        /// <summary>
        /// 调整相机距离和高度（通过multiplier而不是直接修改真实值）
        /// </summary>
        private void AdjustCameraDistance(float multiplier)
        {
            // 调整相机跟随目标位置（如果存在）
            if (_cameraTarget)
            {
                float newDistance = _originalCameraDistance * multiplier * cameraDistanceMultiplier;
                Vector3 direction = (_cameraTarget.position - transform.position).normalized;
                _cameraTarget.position = transform.position + direction * newDistance;
            }
            
            // 使用multiplier系统而不是直接修改原始值
            ApplyCameraMultiplierSystem(multiplier);
        }
        
        /// <summary>
        /// 应用相机multiplier系统（不直接修改原始值）
        /// </summary>
        private void ApplyCameraMultiplierSystem(float multiplier)
        {
            var tpCam = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tpCam) return;
            
            // 确保已缓存原始值
            if (!_cachedTPCamera && !InitializeCameraCache())
            {
                return;
            }
            
            // 更新当前multiplier
            _currentCameraMultiplier = multiplier;
            
            // 计算实际缩放倍率
            float distanceScale = multiplier * cameraDistanceMultiplier;
            float heightScale = multiplier * cameraHeightMultiplier;
            
            // 应用multiplier到相机系统（基于原始值计算，但不修改原始值）
            ApplyCameraMultiplierToSystem(tpCam, distanceScale, heightScale);
            
            _cameraMultiplierApplied = true;
        }
        
        /// <summary>
        /// 将multiplier应用到相机系统（基于原始值计算）
        /// </summary>
        private void ApplyCameraMultiplierToSystem(Invector.vCamera.vThirdPersonCamera tpCam, float distanceScale, float heightScale)
        {
            // 使用固定基准值2.8 * multiplier
            float currentDistance = 2.8f * distanceScale;
            
            // 设置当前distance
            tpCam.distance = currentDistance;
            
            // 对于相机状态，使用固定基准值
            if (tpCam.CameraStateList != null && tpCam.CameraStateList.tpCameraStates != null)
            {
                foreach (var state in tpCam.CameraStateList.tpCameraStates)
                {
                    // 使用固定基准值2.8 * multiplier
                    state.defaultDistance = 2.8f * distanceScale;
                    state.minDistance = 2.8f * distanceScale;
                    state.maxDistance = 2.8f * distanceScale;
                    
                    if (scaleCameraStateHeight)
                    {
                        // 使用固定基准值2.3 * multiplier
                        state.height = 2.3f * heightScale;
                    }
                }
            }
        }
        
        /// <summary>
        /// 重置相机multiplier系统
        /// </summary>
        public void ResetCameraMultiplierSystem()
        {
            var tpCam = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tpCam) return;
            
            // 重置到固定基准值
            if (tpCam.CameraStateList != null && tpCam.CameraStateList.tpCameraStates != null)
            {
                foreach (var state in tpCam.CameraStateList.tpCameraStates)
                {
                    // 使用固定基准值
                    state.defaultDistance = 2.8f;
                    state.minDistance = 2.8f;
                    state.maxDistance = 2.8f;
                    state.height = 2.3f;
                }
            }
            
            // 重置当前distance到固定基准值
            tpCam.distance = 2.8f;
            
            _currentCameraMultiplier = 1.0f;
            _cameraMultiplierApplied = false;
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 重置到原始大小
        /// </summary>
        [ContextMenu("重置到原始大小")]
        public void ResetToOriginalSize()
        {
            ResetCameraStatesToOriginal();
            ApplySizeChange(1.0f);
        }
        
        /// <summary>
        /// 重置相机状态到原始数值（优化版本）
        /// </summary>
        [ContextMenu("重置相机状态")]
        public void ResetCameraStatesToOriginal()
        {
            var tp = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tp) 
            {
                //Debug.LogWarning("[CharacterSizeController] 找不到vThirdPersonCamera实例");
                return;
            }
            
            int resetCount = 0;
            
            // 重置相机状态到固定基准值
            if (tp.CameraStateList != null && tp.CameraStateList.tpCameraStates != null)
            {
                foreach (var state in tp.CameraStateList.tpCameraStates)
                {
                    // 使用固定基准值
                    state.defaultDistance = 2.8f;
                    state.minDistance = 2.8f;
                    state.maxDistance = 2.8f;
                    state.height = 2.3f;
                    resetCount++;
                }
            }
            
            // 重置相机距离到固定基准值
            tp.distance = 2.8f;
            _hasAppliedInitialCameraScale = false; // 重置标志，允许下次启动时重新应用
        }
        
        /// <summary>
        /// 设置为指定大小
        /// </summary>
        /// <param name="size">目标大小倍数</param>
        public void SetSize(float size)
        {
            sizeMultiplier = size;
            ApplySizeChange(size);
        }
        
        /// <summary>
        /// 获取当前大小倍数
        /// </summary>
        public float GetCurrentSize()
        {
            return _currentSizeMultiplier;
        }
        
        /// <summary>
        /// 获取当前体型等级
        /// </summary>
        public CharacterSizeLevel GetCurrentSizeLevel()
        {
            return currentSizeLevel;
        }
        
        /// <summary>
        /// 获取当前限制器突破等级（1-5）
        /// </summary>
        public int GetCurrentLimitBreakerLevel()
        {
            return limitBreakerLevel;
        }
        
        /// <summary>
        /// 获取当前相机multiplier
        /// </summary>
        public float GetCurrentCameraMultiplier()
        {
            return _currentCameraMultiplier;
        }
        
        /// <summary>
        /// 检查相机multiplier是否已应用
        /// </summary>
        public bool IsCameraMultiplierApplied()
        {
            return _cameraMultiplierApplied;
        }
        
        /// <summary>
        /// 是否处于体型变化冷却中
        /// </summary>
        public bool IsOnSizeChangeCooldown()
        {
            if (!Application.isPlaying) return false;
            return Time.time < _lastSizeChangeTime + sizeChangeCooldown;
        }
        
        /// <summary>
        /// 冷却剩余时间（秒），未冷却返回0
        /// </summary>
        public float GetSizeChangeCooldownRemaining()
        {
            if (!IsOnSizeChangeCooldown()) return 0f;
            return Mathf.Max(0f, (_lastSizeChangeTime + sizeChangeCooldown) - Time.time);
        }
        
        /// <summary>
        /// 切换到指定体型等级
        /// </summary>
        /// <param name="level">目标体型等级</param>
        /// <param name="limitBreakerLevel">限制器突破等级（仅4级时有效）</param>
        public void SetSizeLevel(CharacterSizeLevel level, int limitBreakerLevel = 1)
        {
            if (IsOnSizeChangeCooldown())
            {
                Debug.LogWarning($"[CharacterSizeController] 体型变化处于冷却中，剩余 {GetSizeChangeCooldownRemaining():F2}s");
                return;
            }
            currentSizeLevel = level;
            this.limitBreakerLevel = Mathf.Clamp(limitBreakerLevel, 1, 5);
            
            // 根据等级计算体型倍数
            float targetSize = CalculateSizeFromLevel(level, this.limitBreakerLevel);
            
            // 应用体型变化
            SetSize(targetSize);
            
            // 更新等级效果
            UpdateLevelEffects();
            if (Application.isPlaying)
                _lastSizeChangeTime = Time.time;
            
            Debug.Log($"[CharacterSizeController] 切换到体型等级: {level} (倍数: {targetSize:F1})");
        }
        
        /// <summary>
        /// 根据体型等级计算体型倍数
        /// </summary>
        private float CalculateSizeFromLevel(CharacterSizeLevel level, int limitBreakerLevel)
        {
            switch (level)
            {
                case CharacterSizeLevel.Mini:
                    return 0.5f;
                case CharacterSizeLevel.Standard:
                    return 1.0f;
                case CharacterSizeLevel.Giant:
                    return 2.0f;
                case CharacterSizeLevel.LimitBreaker:
                    if (limitBreakerLevel <= 5)
                    {
                        return 2.5f + (limitBreakerLevel * 0.5f); // 3, 3.5, 4, 4.5, 5
                    }
                    else
                    {
                        return 6.0f; // 最终形态
                    }
                default:
                    return 1.0f;
            }
        }
        
        /// <summary>
        /// 更新体型等级效果
        /// </summary>
        private void UpdateLevelEffects()
        {
            // 重置所有效果
            staminaConsumptionMultiplier = 1.0f;
            attackDamageMultiplier = 1.0f;
            immuneToEnergyBombardment = false;
            immuneToEnergyFlood = false;
            isLimitBreakerActive = false;
            
            switch (currentSizeLevel)
            {
                case CharacterSizeLevel.Mini:
                    // 1级：迷你体型 - 体力消耗减少，免疫异能轰炸
                    staminaConsumptionMultiplier = 0.5f; // 体力消耗减半
                    immuneToEnergyBombardment = true;
                    // 独立乘区：迷你体型额外2倍移动 & 跳跃
                    levelMoveBonusMultiplier = 2.0f;
                    levelJumpBonusMultiplier = 2.0f;
                    break;
                    
                case CharacterSizeLevel.Standard:
                    // 2级：标准体型 - 正常功能
                    // 所有倍率保持1.0f，无特殊效果
                    levelMoveBonusMultiplier = 1.0f;
                    levelJumpBonusMultiplier = 1.0f;
                    break;
                    
                case CharacterSizeLevel.Giant:
                    // 3级：巨大体型 - 攻击伤害提升，免疫异能洪水
                    attackDamageMultiplier = 1.5f; // 攻击伤害提升50%
                    immuneToEnergyFlood = true;
                    levelMoveBonusMultiplier = 1.0f;
                    levelJumpBonusMultiplier = 1.0f;
                    break;
                    
                case CharacterSizeLevel.LimitBreaker:
                    // 4级：限制器突破 - 攻击伤害巨大提升
                    isLimitBreakerActive = true;
                    attackDamageMultiplier = 2.0f + (limitBreakerLevel * 0.5f); // 2.0, 2.5, 3.0, 3.5, 4.0
                    levelMoveBonusMultiplier = 1.0f;
                    levelJumpBonusMultiplier = 1.0f;
                    break;
            }
            
            // 应用倍率到实际系统
            ApplyStaminaMultiplier();
            ApplyDamageMultiplier();
            
            // 叠乘独立乘区后，重新应用移动速度与跳跃高度
            if (adjustMovementSpeed)
            {
                float effectiveMoveMultiplier = _currentSizeMultiplier * levelMoveBonusMultiplier;
                AdjustMovementSpeed(effectiveMoveMultiplier);
            }
            if (adjustJumpHeight)
            {
                float effectiveJumpMultiplier = _currentSizeMultiplier * levelJumpBonusMultiplier;
                AdjustJumpHeight(effectiveJumpMultiplier);
            }
            
            Debug.Log($"[CharacterSizeController] 体型等级效果更新 - 体力消耗: {staminaConsumptionMultiplier:F1}x, 攻击伤害: {attackDamageMultiplier:F1}x");
        }
        
        /// <summary>
        /// 应用体力消耗倍率到实际系统
        /// </summary>
        private void ApplyStaminaMultiplier()
        {
            if (!_motor) return;
            
            // 应用体力消耗倍率到Invector控制器
            // 修改体力消耗相关的参数
            _motor.sprintStamina = _motor.sprintStamina * staminaConsumptionMultiplier;
            _motor.jumpStamina = _motor.jumpStamina * staminaConsumptionMultiplier;
            
            // 查找MeleeManager并应用体力消耗倍率
            var meleeManager = GetComponent<Invector.vMelee.vMeleeManager>();
            if (meleeManager != null)
            {
                // 应用体力消耗倍率到攻击体力消耗
                meleeManager.defaultStaminaCost = meleeManager.defaultStaminaCost * staminaConsumptionMultiplier;
            }
            
        }
        
        /// <summary>
        /// 应用攻击伤害倍率到实际系统
        /// </summary>
        private void ApplyDamageMultiplier()
        {
            // 查找攻击系统组件
            var meleeManager = GetComponent<Invector.vMelee.vMeleeManager>();
            if (meleeManager != null)
            {
                // 应用伤害倍率到近战管理器
                // 修改默认伤害值
                meleeManager.defaultDamage.damageValue = meleeManager.defaultDamage.damageValue * attackDamageMultiplier;
                
                // 如果有装备的武器，也修改武器的伤害
                if (meleeManager.rightWeapon != null)
                {
                    meleeManager.rightWeapon.damage.damageValue = meleeManager.rightWeapon.damage.damageValue * attackDamageMultiplier;
                }
                if (meleeManager.leftWeapon != null)
                {
                    meleeManager.leftWeapon.damage.damageValue = meleeManager.leftWeapon.damage.damageValue * attackDamageMultiplier;
                }
                
            }

        }
        
        /// <summary>
        /// 限制器突破升级（4级专用）
        /// </summary>
        public bool UpgradeLimitBreaker()
        {
            if (currentSizeLevel != CharacterSizeLevel.LimitBreaker)
            {
                Debug.LogWarning("[CharacterSizeController] 只有4级限制器突破才能升级！");
                return false;
            }
            if (IsOnSizeChangeCooldown())
            {
                Debug.LogWarning($"[CharacterSizeController] 限制器突破升级处于冷却中，剩余 {GetSizeChangeCooldownRemaining():F2}s");
                return false;
            }
            
            if (limitBreakerLevel >= 5)
            {
                Debug.LogWarning("[CharacterSizeController] 限制器突破已达到最高等级！");
                return false;
            }
            
            limitBreakerLevel++;
            SetSizeLevel(currentSizeLevel, limitBreakerLevel);
            
            Debug.Log($"[CharacterSizeController] 限制器突破升级到 {limitBreakerLevel} 级！");
            return true;
        }
        
        /// <summary>
        /// 平滑过渡到指定大小
        /// </summary>
        /// <param name="targetSize">目标大小</param>
        /// <param name="duration">过渡时间</param>
        public void SmoothTransitionToSize(float targetSize, float duration = 1.0f)
        {
            StartCoroutine(SmoothTransitionCoroutine(targetSize, duration));
        }
        
        private System.Collections.IEnumerator SmoothTransitionCoroutine(float targetSize, float duration)
        {
            float startSize = _currentSizeMultiplier;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float currentSize = Mathf.Lerp(startSize, targetSize, progress);
                
                ApplySizeChange(currentSize);
                yield return null;
            }
            
            ApplySizeChange(targetSize);
        }
        
        #endregion
        
        #region 体型等级测试按钮
        
        [Header("体型等级测试按钮")]
        [Space(10)]
        [SerializeField] private bool _testButtonsSection = true; // 占位字段，用于显示Header和Space
        
        [Button("切换到1级迷你体型")]
        public void TestSetMiniLevel()
        {
            SetSizeLevel(CharacterSizeLevel.Mini);
        }
        
        [Button("切换到2级标准体型")]
        public void TestSetStandardLevel()
        {
            SetSizeLevel(CharacterSizeLevel.Standard);
        }
        
        [Button("切换到3级巨大体型")]
        public void TestSetGiantLevel()
        {
            SetSizeLevel(CharacterSizeLevel.Giant);
        }
        
        [Button("切换到4级限制器突破")]
        public void TestSetLimitBreakerLevel()
        {
            SetSizeLevel(CharacterSizeLevel.LimitBreaker, 1);
        }
        
        [Button("限制器突破升级")]
        public void TestUpgradeLimitBreaker()
        {
            UpgradeLimitBreaker();
        }
        
        [Button("显示当前状态")]
        public void TestShowCurrentStatus()
        {
            Debug.Log($"[CharacterSizeController] 当前状态:\n" +
                     $"体型等级: {currentSizeLevel}\n" +
                     $"体型倍数: {GetCurrentSize():F1}x\n" +
                     $"限制器突破等级: {limitBreakerLevel}\n" +
                     $"体力消耗倍率: {staminaConsumptionMultiplier:F1}x\n" +
                     $"攻击伤害倍率: {attackDamageMultiplier:F1}x\n" +
                     $"免疫异能轰炸: {immuneToEnergyBombardment}\n" +
                     $"免疫异能洪水: {immuneToEnergyFlood}\n" +
                     $"限制器突破激活: {isLimitBreakerActive}");
        }
        
        [Button("测试倍率应用")]
        public void TestApplyMultipliers()
        {
            Debug.Log("[CharacterSizeController] 测试倍率应用...");
            ApplyStaminaMultiplier();
            ApplyDamageMultiplier();
        }
        
        #endregion
        
        #region 调试信息
        
        private void OnDrawGizmosSelected()
        {
            if (!_motor || !_capsuleCollider) return;
            
            // 绘制碰撞体轮廓
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_motor.capsuleThickness, _motor.capsuleHeight, _motor.capsuleThickness));
            
            // 标注被缩放的视觉子物体位置
            if (_modelRootA)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_modelRootA.position, 0.05f);
            }
            if (_modelRootB)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_modelRootB.position, 0.05f);
            }
            
            // 绘制地面检测点
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 groundCheckPos = transform.position + Vector3.down * (_motor.capsuleHeight / 2f + 0.1f);
            Gizmos.DrawWireSphere(groundCheckPos, _motor.capsuleThickness / 2f);
        }
        
        #endregion

        #region 相机集成（Invector vThirdPersonCamera）- 优化版本
        
        /// <summary>
        /// 缓存相机原始值并应用初始缩放
        /// </summary>
        private System.Collections.IEnumerator CacheAndScaleTPCameraCoroutine()
        {
            // 等待一到两帧，确保tpCamera已实例化并完成SetMainTarget
            yield return null;
            yield return null;
            
            // 初始化相机缓存
            if (!InitializeCameraCache())
            {
                yield break;
            }
            
            // 应用游戏开始时的固定相机设置（无论SizeController如何切换）
            ApplyInitialCameraSettings();
            
            // 应用初始缩放（如果需要）
            if (!_hasAppliedInitialCameraScale && !Mathf.Approximately(sizeMultiplier, 1.0f))
            {
                ApplyCameraMultiplierSystem(sizeMultiplier);
                _hasAppliedInitialCameraScale = true;
            }
        }
        
        /// <summary>
        /// 应用游戏开始时的固定相机设置（无论SizeController如何切换）
        /// </summary>
        private void ApplyInitialCameraSettings()
        {
            var tpCam = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tpCam) return;
            
            // 设置固定的相机距离和高度
            tpCam.distance = 2.8f;
            
            // 设置所有相机状态的固定距离和高度
            if (tpCam.CameraStateList != null && tpCam.CameraStateList.tpCameraStates != null)
            {
                foreach (var state in tpCam.CameraStateList.tpCameraStates)
                {
                    state.defaultDistance = 2.8f;
                    state.minDistance = 2.8f;
                    state.maxDistance = 2.8f;
                    state.height = 2.3f;
                }
            }
            
            Debug.Log($"[CharacterSizeController] 应用初始相机设置 - 距离: 2.8, 高度: 2.3 (所有相机状态)");
        }
        
        /// <summary>
        /// 初始化相机缓存（保存原始值）
        /// </summary>
        private bool InitializeCameraCache()
        {
            var tpCam = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tpCam) return false;
            
            if (!_cachedTPCamera)
            {
                _originalTPCameraDistance = tpCam.distance;
                _cachedTPCamera = true;
                
                // 保存所有相机状态的原始值
                if (scaleCameraStates && tpCam.CameraStateList != null && tpCam.CameraStateList.tpCameraStates != null)
                {
                    foreach (var st in tpCam.CameraStateList.tpCameraStates)
                    {
                        if (!_cameraStateBase.ContainsKey(st))
                        {
                            _cameraStateBase[st] = (st.defaultDistance, st.minDistance, st.maxDistance, st.height);
                        }
                    }
                }
                
            }
            
            return true;
        }
        
        #endregion
        
        private Transform FindChildByName(Transform root, string childName)
        {
            if (!root || string.IsNullOrEmpty(childName)) return null;
            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                if (trs[i].name == childName) return trs[i];
            }
            return null;
        }
    }
}
