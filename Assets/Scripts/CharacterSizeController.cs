using UnityEngine;
using Invector.vCharacterController;
using System.Collections.Generic;
using Invector.vCamera;

namespace Xuwu.Character
{
    /// <summary>
    /// 角色体型控制器 - 安全地调整Invector角色体型而不破坏控制器功能
    /// </summary>
    [AddComponentMenu("Xuwu/Character/Character Size Controller")]
    [RequireComponent(typeof(vThirdPersonMotor))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterSizeController : MonoBehaviour
    {
        [Header("体型设置")]
        [Range(0.5f, 2.0f)]
        [Tooltip("体型缩放倍数，1.0为原始大小")]
        public float sizeMultiplier = 1.0f;
        
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
        
        [Tooltip("（直接目标点跟随用）相机距离倍率，对_target位移生效")]
        [Range(0.1f, 3.0f)]
        public float cameraDistanceMultiplier = 1.0f;
        
        [Header("地面检测设置")]
        [Tooltip("是否强制重新计算地面检测")]
        public bool forceGroundRecalculation = true;
        
        [Header("组件引用")]
        [SerializeField] private vThirdPersonMotor _motor;
        [SerializeField] private CapsuleCollider _capsuleCollider;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _cameraTarget;
        
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
        [Tooltip("（状态系统用）distance倍率，最终= sizeMultiplier * cameraStateDistanceMultiplier")]
        [Range(0.1f, 3.0f)]
        public float cameraStateDistanceMultiplier = 1.0f;
        [Tooltip("（状态系统用）height倍率，最终= sizeMultiplier * cameraStateHeightMultiplier")]
        [Range(0.1f, 3.0f)]
        public float cameraStateHeightMultiplier = 1.0f;
        private readonly Dictionary<Invector.vThirdPersonCameraState, (float def,float min,float max,float height)> _cameraStateBase = new Dictionary<Invector.vThirdPersonCameraState, (float def, float min, float max, float height)>();
        
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
                    Debug.Log($"[CharacterSizeController] 找到vThirdPersonCamera组件在 {cameraController.gameObject.name}");
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
            
            newSizeMultiplier = Mathf.Clamp(newSizeMultiplier, 0.3f, 3.0f);
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
        /// 调整相机距离
        /// </summary>
        private void AdjustCameraDistance(float multiplier)
        {
            if (_cameraTarget)
            {
                float newDistance = _originalCameraDistance * multiplier * cameraDistanceMultiplier;
                Vector3 direction = (_cameraTarget.position - transform.position).normalized;
                _cameraTarget.position = transform.position + direction * newDistance;
            }
            // 同步Invector相机状态与距离
            if (scaleCameraStates)
                ScaleCameraStatesAndRemapDistance(multiplier);
            else
                AdjustThirdPersonCameraDistance(multiplier);
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 重置到原始大小
        /// </summary>
        [ContextMenu("重置到原始大小")]
        public void ResetToOriginalSize()
        {
            ApplySizeChange(1.0f);
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
        
        #region 测试方法
        
        [ContextMenu("测试 - 设置为1.5倍大小")]
        public void TestSetSize1_5x()
        {
            SetSize(1.5f);
        }
        
        [ContextMenu("测试 - 设置为0.8倍大小")]
        public void TestSetSize0_8x()
        {
            SetSize(0.8f);
        }
        
        [ContextMenu("测试 - 设置为2.0倍大小")]
        public void TestSetSize2_0x()
        {
            SetSize(2.0f);
        }
        
        [ContextMenu("测试 - 设置为0.5倍大小")]
        public void TestSetSize0_5x()
        {
            SetSize(0.5f);
        }
        
        [ContextMenu("测试 - 平滑过渡到1.2倍")]
        public void TestSmoothTransition()
        {
            SmoothTransitionToSize(1.2f, 2.0f);
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

        #region 相机集成（Invector vThirdPersonCamera）
        private System.Collections.IEnumerator CacheAndScaleTPCameraCoroutine()
        {
            // 等待一到两帧，确保tpCamera已实例化并完成SetMainTarget
            yield return null;
            yield return null;
            var tpCam = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (tpCam)
            {
                if (!_cachedTPCamera)
                {
                    _originalTPCameraDistance = tpCam.distance;
                    _cachedTPCamera = true;
                }
                if (scaleCameraStates)
                    ScaleCameraStatesAndRemapDistance(sizeMultiplier);
                else
                {
                    var targetDistance = _originalTPCameraDistance * sizeMultiplier * cameraDistanceMultiplier;
                    _lastAppliedTPCameraDistance = tpCam.distance;
                    tpCam.distance = targetDistance;
                    if (!Mathf.Approximately(_lastAppliedTPCameraDistance, targetDistance))
                    {
                        Debug.Log($"[CharacterSizeController] vThirdPersonCamera.distance 改为 {targetDistance:F2}（原 {_lastAppliedTPCameraDistance:F2}）");
                    }
                }
            }
        }

        private void AdjustThirdPersonCameraDistance(float multiplier)
        {
            var tpCam = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tpCam) return;
            if (!_cachedTPCamera)
            {
                _originalTPCameraDistance = tpCam.distance;
                _cachedTPCamera = true;
            }
            var targetDistance = _originalTPCameraDistance * multiplier * cameraDistanceMultiplier;
            var before = tpCam.distance;
            if (!Mathf.Approximately(before, targetDistance))
            {
                tpCam.distance = targetDistance;
                Debug.Log($"[CharacterSizeController] vThirdPersonCamera.distance 改为 {targetDistance:F2}（原 {before:F2}）");
                _lastAppliedTPCameraDistance = targetDistance;
            }
        }

        private void ScaleCameraStatesAndRemapDistance(float scale)
        {
            var tp = Invector.vCamera.vThirdPersonCamera.instance ?? FindObjectOfType<Invector.vCamera.vThirdPersonCamera>();
            if (!tp || tp.CameraStateList == null || tp.CameraStateList.tpCameraStates == null) return;
            // 记录当前distance在区间内的相对位置
            var cur = tp.currentState;
            float t = 0.5f;
            if (cur != null && cur.maxDistance > cur.minDistance)
                t = Mathf.InverseLerp(cur.minDistance, cur.maxDistance, tp.distance);

            // 计算状态缩放倍率（体型 * 自定义倍率）
            float distScale = scale * cameraStateDistanceMultiplier;
            float hScale = scale * cameraStateHeightMultiplier;

            foreach (var st in tp.CameraStateList.tpCameraStates)
            {
                if (!_cameraStateBase.ContainsKey(st))
                    _cameraStateBase[st] = (st.defaultDistance, st.minDistance, st.maxDistance, st.height);
                var b = _cameraStateBase[st];
                st.defaultDistance = b.def * distScale;
                st.minDistance = b.min * distScale;
                st.maxDistance = b.max * distScale;
                if (scaleCameraStateHeight) st.height = b.height * hScale;
            }

            // 将当前distance映射回新的边界，保持玩家相对缩放位置
            cur = tp.currentState;
            if (cur != null)
            {
                var newDist = Mathf.Lerp(cur.minDistance, cur.maxDistance, t);
                if (!Mathf.Approximately(tp.distance, newDist))
                {
                    var before = tp.distance;
                    tp.distance = newDist;
                    Debug.Log($"[CharacterSizeController] CameraState缩放后 distance 映射为 {newDist:F2}（原 {before:F2}）");
                }
            }
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
