using Invector.vEventSystems;
using System.Collections;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// 非人型Boss AI控制器
    /// 直接继承vSimpleMeleeAI_Controller，只修复动画层问题
    /// </summary>
    [vClassHeader("Non-Humanoid Boss AI", "专门为非人型Boss设计的AI控制器，修复动画层问题")]
    public class NonHumanoidBossAI : vSimpleMeleeAI_Controller
    {
        [TabGroup("动画层修复")]
        [Header("动画层设置")]
        [Tooltip("是否启用动画层修复")]
        public bool enableAnimationLayerFix = true;
        
        [TabGroup("动画层修复")]
        [ShowIf("enableAnimationLayerFix")]
        [Tooltip("自定义基础层名称")]
        public string customBaseLayer = "Base Layer";
        [TabGroup("动画层修复")]
        [ShowIf("enableAnimationLayerFix")]
        [Tooltip("自定义上层名称")]
        public string customUpperLayer = "UpperBody";
        [TabGroup("动画层修复")]
        [ShowIf("enableAnimationLayerFix")]
        [Tooltip("自定义右臂层名称")]
        public string customRightArmLayer = "RightArm";
        [TabGroup("动画层修复")]
        [ShowIf("enableAnimationLayerFix")]
        [Tooltip("自定义左臂层名称")]
        public string customLeftArmLayer = "LeftArm";
        [TabGroup("动画层修复")]
        [ShowIf("enableAnimationLayerFix")]
        [Tooltip("自定义全身层名称")]
        public string customFullBodyLayer = "FullBody";
        
        [TabGroup("动画层修复")]
        [ShowIf("enableAnimationLayerFix")]
        [Button("检查动画层状态")]
        public void CheckAnimationLayerStatus()
        {
            if (animator)
            {
                Debug.Log($"[NonHumanoidBossAI] 动画层状态:");
                Debug.Log($"- Base Layer: {animator.GetLayerIndex(customBaseLayer)}");
                Debug.Log($"- UnderBody Layer: {animator.GetLayerIndex(customUpperLayer)}");
                Debug.Log($"- RightArm Layer: {animator.GetLayerIndex(customRightArmLayer)}");
                Debug.Log($"- LeftArm Layer: {animator.GetLayerIndex(customLeftArmLayer)}");
                Debug.Log($"- UpperBody Layer: {animator.GetLayerIndex(customUpperLayer)}");
                Debug.Log($"- FullBody Layer: {animator.GetLayerIndex(customFullBodyLayer)}");
            }
        }
        
        // 重写UpdateAnimator方法，修复动画层问题
        public new void UpdateAnimator(float deltaTime, float inputMagnitude)
        {
            if (!enableAnimationLayerFix)
            {
                // 使用原始逻辑
                base.UpdateAnimator(deltaTime, inputMagnitude);
                return;
            }
            
            // 使用修复后的动画控制
            UpdateAnimatorFixed(deltaTime, inputMagnitude);
        }
        
        /// <summary>
        /// 修复后的动画更新方法
        /// </summary>
        private void UpdateAnimatorFixed(float deltaTime, float inputMagnitude)
        {
            if (animator == null || !animator.enabled) return;
            
            // 更新层信息（修复版本）
            UpdateLayerInfoFixed();
            
            // 基础移动参数
            animator.SetFloat("InputMagnitude", inputMagnitude, 0.1f, deltaTime);
            animator.SetFloat("InputHorizontal", 0f, 0.1f, deltaTime);
            animator.SetFloat("InputVertical", inputMagnitude, 0.1f, deltaTime);
            
            // 垂直速度
            if (GetComponent<Rigidbody>())
            {
                animator.SetFloat("VerticalVelocity", GetComponent<Rigidbody>().velocity.y, 0.1f, deltaTime);
            }
            
            // 状态参数
            animator.SetBool("IsStrafing", isStrafing);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsDead", currentHealth <= 0);
            animator.SetBool("IsBlocking", isBlocking);
            
            // 攻击参数
            if (meleeManager != null)
            {
                animator.SetInteger("AttackID", meleeManager.GetAttackID());
                animator.SetInteger("DefenseID", meleeManager.GetDefenseID());
                animator.SetFloat("MoveSet_ID", meleeManager.GetMoveSetID());
            }
        }
        
        /// <summary>
        /// 修复后的层信息更新
        /// </summary>
        private void UpdateLayerInfoFixed()
        {
            // 只获取存在的层的状态信息
            int baseLayerIndex = animator.GetLayerIndex(customBaseLayer);
            int upperLayerIndex = animator.GetLayerIndex(customUpperLayer);
            int rightArmLayerIndex = animator.GetLayerIndex(customRightArmLayer);
            int leftArmLayerIndex = animator.GetLayerIndex(customLeftArmLayer);
            int fullBodyLayerIndex = animator.GetLayerIndex(customFullBodyLayer);
            
            if (baseLayerIndex != -1) baseLayerInfo = animator.GetCurrentAnimatorStateInfo(baseLayerIndex);
            if (upperLayerIndex != -1) underBodyInfo = animator.GetCurrentAnimatorStateInfo(upperLayerIndex);
            if (rightArmLayerIndex != -1) rightArmInfo = animator.GetCurrentAnimatorStateInfo(rightArmLayerIndex);
            if (leftArmLayerIndex != -1) leftArmInfo = animator.GetCurrentAnimatorStateInfo(leftArmLayerIndex);
            if (upperLayerIndex != -1) upperBodyInfo = animator.GetCurrentAnimatorStateInfo(upperLayerIndex);
            if (fullBodyLayerIndex != -1) fullBodyInfo = animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex);
        }
        
        // 重写FixedUpdate方法，替换ControlLocomotion调用
        protected new void FixedUpdate()
        {
            if (!enableAnimationLayerFix)
            {
                // 使用原始逻辑
                base.FixedUpdate();
                return;
            }
            
            // 使用修复后的移动控制
            ControlLocomotionFixed();
        }
        
        /// <summary>
        /// 修复后的移动控制方法
        /// </summary>
        private void ControlLocomotionFixed()
        {
            if (AgentDone() && agent.updatePosition || lockMovement)
            {
                agent.speed = 0f;
                combatMovement = Vector3.zero;
            }
            if (agent.isOnOffMeshLink)
            {
                float speed = agent.desiredVelocity.magnitude;
                UpdateAnimator(AgentDone() ? 0f : speed, direction);
            }
            else
            {
                var desiredVelocity = agent.enabled ? agent.updatePosition ? agent.desiredVelocity : (agent.nextPosition - transform.position) : (destination - transform.position);
                if (OnStrafeArea)
                {
                    var destin = transform.InverseTransformDirection(desiredVelocity).normalized;
                    combatMovement = Vector3.Lerp(combatMovement, destin, 2f * Time.deltaTime);
                    UpdateAnimator(AgentDone() ? 0f : combatMovement.z, combatMovement.x);
                }
                else
                {
                    float speed = desiredVelocity.magnitude;
                    combatMovement = Vector3.zero;
                    UpdateAnimator(AgentDone() ? 0f : speed, 0f);
                }
            }
        }
    }
}