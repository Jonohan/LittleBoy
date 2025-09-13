using Invector.vCharacterController;
using System.Collections;
using UnityEngine;
using Xuwu.FourDimensionalPortals;

namespace Invector.vCamera
{

    public class vThirdPersonCamera : MonoBehaviour
    {
        private static vThirdPersonCamera _instance;

        public static vThirdPersonCamera instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = GameObject.FindObjectOfType<vThirdPersonCamera>();

                    //Tell unity not to destroy this object when loading a new scene!
                    //DontDestroyOnLoad(_instance.gameObject);
                }

                return _instance;
            }
        }

        #region inspector properties    

        public Transform mainTarget;
        [Tooltip("Lerp speed between Camera States")]
        [SerializeField] protected float _smoothBetweenState = 6f;
        public virtual float smoothBetweenState { get { return _smoothBetweenState; } set { _smoothBetweenState = value; } }
        [SerializeField] protected float _smoothCameraRotation = 12f;
        public virtual float smoothCameraRotation { get { return _smoothCameraRotation; } set { _smoothCameraRotation = value; } }
        [SerializeField] protected float _smoothSwitchSide = 2f;
        public virtual float smoothSwitchSide { get { return _smoothSwitchSide; } set { _smoothSwitchSide = value; } }

        [SerializeField] protected float _scrollSpeed = 10f;
        public virtual float scrollSpeed { get { return _scrollSpeed; } set { _scrollSpeed = value; } }
        [Tooltip("Multiplier of Mouse x and y when using joystick")]
        [SerializeField] protected float _joystickSensitivity = 1;
        public virtual float joystickSensitivity { get { return _joystickSensitivity; } set { _joystickSensitivity = value; } }
        [Tooltip("What layer will be culled")]
        public LayerMask cullingLayer = 1 << 0;
        [Tooltip("Change this value If the camera pass through the wall")]
        public float clipPlaneMargin;
        public float checkHeightRadius;
        public bool showGizmos;
        public bool startUsingTargetRotation = true;
        public bool startSmooth = false;
        [vHideInInspector("startSmooth")]
        public float startSmoothFactor = 1f;
        [Tooltip("Returns to behind the target automatically after 'behindTargetDelay' period")]
        public bool autoBehindTarget = false;
        [vHideInInspector("autoBehindTarget")]
        public float behindTargetDelay = 2f;
        [vHideInInspector("autoBehindTarget")]
        public float behindTargetSmoothRotation = 1f;

        [Tooltip("Debug purposes, lock the camera behind the character for better align the states")]
        [SerializeField] protected bool lockCamera;

        WaitForEndOfFrame waitFrame = new WaitForEndOfFrame();
        public Vector2 offsetMouse;
        #endregion

        #region hide properties    
        [HideInInspector]
        public int indexList, indexLookPoint;
        [HideInInspector]
        public float offSetPlayerPivot;
        [HideInInspector]
        public float distance = 5f;
        [HideInInspector]
        public string currentStateName;
        [HideInInspector]
        public Transform currentTarget;
        [HideInInspector]
        public vThirdPersonCameraState currentState;
        [HideInInspector]
        public vThirdPersonCameraListData CameraStateList;
        [HideInInspector]
        public Transform lockTarget;
        [HideInInspector]
        public Vector2 movementSpeed;
        [HideInInspector]
        public vThirdPersonCameraState lerpState;

        protected float lockTargetSpeed;
        protected float lockTargetWeight;
        protected float initialCameraRotation;

        protected bool cameraIsRotating;
        protected Quaternion lastCameraRotation;
        protected float lastRotationTimer;

        protected Vector3 currentTargetPos;
        protected Vector3 lookPoint;
        protected Vector3 current_cPos;
        protected Vector3 desired_cPos;
        protected Vector3 lookTargetAdjust;

        internal float _mouseY = 0f;
        internal virtual float mouseY { get { return _mouseY; } set { _mouseY = value; } }
        internal float _mouseX = 0f;
        internal virtual float mouseX { get { return _mouseX; } set { _mouseX = value; } }

        protected virtual float currentHeight { get; set; }
        protected virtual float currentZoom { get; set; }
        protected virtual float cullingHeight { get; set; }
        protected virtual float cullingDistance { get; set; }
        internal float _switchRight;
        internal virtual float switchRight { get { return _switchRight; } set { _switchRight = value; } }
        protected virtual float currentSwitchRight { get; set; }
        protected virtual float heightOffset { get; set; }
        public virtual bool isInit { get; set; }
        public virtual bool useSmooth { get; set; }
        protected virtual bool isNewTarget { get; set; }
        protected virtual bool firstStateIsInit { get; set; }
        protected virtual bool firstUpdated { get; set; }

        protected Quaternion fixedRotation;
        internal Camera targetCamera;

        protected float transformWeight;
        protected virtual float mouseXStart { get; set; }
        protected virtual float mouseYStart { get; set; }
        protected virtual Vector3 startPosition { get; set; }
        protected virtual Quaternion startRotation { get; set; }
        protected Vector3 cameraVelocityDamp;

        protected Transform _lookAtTarget;

        protected Vector3 lastLookAtPosition, lastLookAtForward;
        public bool isFreezed;
        // Portal View Support
        protected bool portalViewActive;
        protected Camera portalVirtualCamera;
        protected Portal activeFromPortal;
        protected Portal activeToPortal;
        
        [Header("体型检查配置")]
        [SerializeField] private GameObject playerObject;
        protected Transform targetLookAt
        {
            get
            {
                if (!_lookAtTarget)
                {
                    _lookAtTarget = new GameObject("targetLookAt").transform;
                    _lookAtTarget.rotation = transform.rotation;
                    _lookAtTarget.position = mainTarget.position;
                }
                return _lookAtTarget;
            }
        }
        #endregion

        protected Rigidbody _selfRigidbody;
        public Rigidbody selfRigidbody
        {
            get
            {
                if (!_selfRigidbody)
                {
                    _selfRigidbody = gameObject.AddComponent<Rigidbody>();
                    _selfRigidbody.isKinematic = true;
                    _selfRigidbody.interpolation = RigidbodyInterpolation.None;
                }
                return _selfRigidbody;
            }

        }
        /// <summary>
        /// Lock camera angle based to the <seealso cref="currentTarget"/>. if you need just to reset angle use <seealso cref="ResetAngle"/>
        /// </summary>
        public bool LockCamera
        {
            get
            {
                return lockCamera;
            }
            set
            {

                lockCamera = value;
            }
        }

        protected virtual void OnDrawGizmos()
        {
            if (showGizmos)
            {
                if (currentTarget)
                {
                    var targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y + offSetPlayerPivot, currentTarget.position.z);
                    Gizmos.DrawWireSphere(targetPos + Vector3.up * cullingHeight, checkHeightRadius);
                    Gizmos.DrawLine(targetPos, targetPos + Vector3.up * cullingHeight);
                }
            }
        }

        protected virtual void Start()
        {

            Init();
        }

        /// <summary>
        /// Init camera.
        /// </summary>
        public virtual void Init()
        {
            if (mainTarget == null)
            {
                return;
            }

            firstUpdated = true;
            useSmooth = true;
            targetLookAt.rotation = startUsingTargetRotation ? mainTarget.rotation : transform.rotation;
            targetLookAt.position = mainTarget.position;
            targetLookAt.hideFlags = HideFlags.HideInHierarchy;
            startPosition = selfRigidbody.position;
            startRotation = selfRigidbody.rotation;
            initialCameraRotation = smoothCameraRotation;
            if (!targetCamera)
            {
                targetCamera = Camera.main;
            }

            currentTarget = mainTarget;
            switchRight = 1;
            currentSwitchRight = 1f;
            mouseXStart = transform.eulerAngles.NormalizeAngle().y;
            mouseYStart = transform.eulerAngles.NormalizeAngle().x;

            if (startSmooth)
            {
                distance = Vector3.Distance(targetLookAt.position, transform.position);
            }
            else
            {
                transformWeight = 1;
            }

            if (startUsingTargetRotation)
            {
                mouseY = currentTarget.eulerAngles.NormalizeAngle().x;
                mouseX = currentTarget.eulerAngles.NormalizeAngle().y;
            }
            else
            {
                mouseY = transform.eulerAngles.NormalizeAngle().x;
                mouseX = transform.eulerAngles.NormalizeAngle().y;
            }


            ChangeState("Default", startSmooth);
            currentZoom = currentState.defaultDistance;
            currentHeight = currentState.height;

            currentTargetPos = new Vector3(currentTarget.position.x, currentTarget.position.y + offSetPlayerPivot, currentTarget.position.z) + currentTarget.transform.up * lerpState.height;
            targetLookAt.position = currentTargetPos;

            isInit = true;
        }

        /// <summary>
        /// Call this method to change a CameraListData
        /// </summary>
        /// <param name="list"></param>
        public void ChangeStateList(vThirdPersonCameraListData list)
        {
            if (CameraStateList != list)
            {
                string stateName = lerpState.Name;
                CameraStateList = list;
                lerpState = CameraStateList.tpCameraStates.Find(state => state.Name == stateName);
            }
        }

        public virtual void FixedUpdate()
        {

            if (mainTarget == null || targetLookAt == null || currentState == null || lerpState == null || !isInit || isFreezed)
            {
                return;
            }

            switch (currentState.cameraMode)
            {
                case TPCameraMode.FreeDirectional:
                    CameraMovement();
                    break;
                case TPCameraMode.FixedAngle:
                    CameraMovement();
                    break;
                case TPCameraMode.FixedPoint:
                    CameraFixed();
                    break;
            }
        }

        /// <summary>
        /// Set a <seealso cref="lockTarget"/> to the  camera  auto rotate to look to.
        /// </summary>   
        public virtual void SetLockTarget(Transform lockTarget)
        {
            if (this.lockTarget != null && this.lockTarget == lockTarget)
            {
                return;
            }

            isNewTarget = lockTarget != this.lockTarget;
            this.lockTarget = lockTarget;
            lockTargetWeight = 0;
            this.lockTargetSpeed = 1;
        }

        /// <summary>
        /// Set a <seealso cref="lockTarget"/> to the  camera  auto rotate to look to.
        /// </summary>
        /// <param name="lockTarget">Target to look</param>
        /// <param name="heightOffset">Height offset</param>
        /// <param name="lockSpeed">speed to look</param>
        public virtual void SetLockTarget(Transform lockTarget, float heightOffset, float lockSpeed = 1)
        {
            if (this.lockTarget != null && this.lockTarget == lockTarget)
            {
                return;
            }

            isNewTarget = lockTarget != this.lockTarget;
            this.lockTarget = lockTarget;
            this.heightOffset = heightOffset;
            lockTargetWeight = 0;
            this.lockTargetSpeed = lockSpeed;
        }

        /// <summary>
        /// Remove the <seealso cref="lockTarget"/>
        /// </summary>
        public virtual void RemoveLockTarget()
        {
            lockTargetWeight = 0;
            lockTarget = null;
        }

        /// <summary>
        /// Set <seealso cref="currentTarget"/>. If you need to retorn to <seealso cref="mainTarget"/>, use <seealso cref="ResetTarget"/>
        /// </summary>
        /// <param name="newTarget"></param>
        public virtual void SetTarget(Transform newTarget)
        {
            lockTargetWeight = 0;
            currentTarget = newTarget ? newTarget : mainTarget;
        }

        /// <summary>
        /// Set<seealso cref="mainTarget"/> and<seealso cref= "currentTarget" />
        /// </summary>
        /// <param name= "newTarget" ></ param>
        public virtual void SetMainTarget(Transform newTarget)
        {
            mainTarget = newTarget;
            currentTarget = newTarget;
            if (!isInit)
            {
                Init();
            }
        }

        /// <summary>
        /// Set <seealso cref="currentTarget"/> to <seealso cref="mainTarget"/>
        /// </summary>
        public virtual void ResetTarget()
        {
            if (currentTarget != mainTarget)
            {
                currentTarget = mainTarget;
                if (!isInit)
                {
                    Init();
                }
            }
        }

        /// <summary>
        /// Set the camera angle based to <seealso cref="currentTarget"/>
        /// </summary>
        public virtual void ResetAngle()
        {
            if (currentTarget)
            {
                mouseY = currentTarget.eulerAngles.NormalizeAngle().x;
                mouseX = currentTarget.eulerAngles.NormalizeAngle().y;
            }
            else
            {
                mouseY = 0;
                mouseX = 0;
            }
        }

        /// <summary>
        /// Reset the camera angle back t
        /// </summary>
        public virtual void ResetAngleWithoutSmooth()
        {
            ResetAngle();

            targetLookAt.forward = currentTarget.forward;
        }

        /// <summary>    
        /// Convert a point in the screen in a Ray for the world
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public virtual Ray ScreenPointToRay(Vector3 Point)
        {
            return this.GetComponent<Camera>().ScreenPointToRay(Point);
        }

        /// <summary>
        /// Change CameraState
        /// </summary>
        /// <param name="stateName"></param>       
        public virtual void ChangeState(string stateName)
        {
            ChangeState(stateName, true);
        }

        /// <summary>
        /// Change CameraState
        /// </summary>
        /// <param name="stateName"></param>
        /// <param name="Use smoth"></param>
        public virtual void ChangeState(string stateName, bool hasSmooth)
        {
            if (currentState != null && currentState.Name.Equals(stateName) || !isInit && firstStateIsInit)
            {
                if (firstStateIsInit)
                {
                    useSmooth = hasSmooth;
                }

                return;
            }
            useSmooth = !firstStateIsInit ? startSmooth : hasSmooth;
            // search for the camera state string name
            vThirdPersonCameraState state = CameraStateList != null ? CameraStateList.tpCameraStates.Find(delegate (vThirdPersonCameraState obj) { return obj.Name.Equals(stateName); }) : new vThirdPersonCameraState("Default");

            if (state != null)
            {
                currentStateName = stateName;
                currentState.cameraMode = state.cameraMode;
                lerpState = state; // set the state of transition (lerpstate) to the state finded on the list
                if (!firstStateIsInit)
                {
                    currentState.defaultDistance = Vector3.Distance(targetLookAt.position, transform.position);
                    currentState.forward = lerpState.forward;
                    currentState.height = state.height;
                    currentState.fov = state.fov;
                    if (useSmooth)
                    {
                        StartCoroutine(ResetFirstState());
                    }
                    else
                    {
                        distance = lerpState.defaultDistance;
                        firstStateIsInit = true;
                    }
                }
                // in case there is no smooth, a copy will be make without the transition values
                if (currentState != null && !useSmooth)
                {
                    currentState.CopyState(state);
                }
            }
            else
            {
                // if the state choosed if not real, the first state will be set up as default
                if (CameraStateList != null && CameraStateList.tpCameraStates.Count > 0)
                {
                    if (lerpState != null)
                    {
                        return;
                    }

                    state = CameraStateList.tpCameraStates[0];
                    currentStateName = state.Name;
                    currentState.cameraMode = state.cameraMode;
                    lerpState = state;

                    if (currentState != null && !useSmooth)
                    {
                        currentState.CopyState(state);
                    }
                }
            }
            // in case a list of states does not exist, a default state will be created
            if (currentState == null)
            {
                currentState = new vThirdPersonCameraState("Null");
                currentStateName = currentState.Name;
            }
            if (CameraStateList != null)
            {
                indexList = CameraStateList.tpCameraStates.IndexOf(state);
            }

            currentZoom = state.defaultDistance;

            if (currentState.cameraMode == TPCameraMode.FixedAngle)
            {
                mouseX = currentState.fixedAngle.x;
                mouseY = currentState.fixedAngle.y;
            }

            currentState.fixedAngle = new Vector3(mouseX, mouseY);
            indexLookPoint = 0;
            if (!isInit)
            {
                CameraMovement(true);
            }
        }

        /// <summary>
        /// Change State using look at point if the cameraMode is FixedPoint  
        /// </summary>
        /// <param name="stateName"></param>
        /// <param name="pointName"></param>
        /// <param name="hasSmooth"></param>
        public virtual void ChangeState(string stateName, string pointName, bool hasSmooth)
        {
            useSmooth = hasSmooth;
            if (!currentState.Name.Equals(stateName))
            {
                // search for the camera state string name
                var state = CameraStateList.tpCameraStates.Find(delegate (vThirdPersonCameraState obj)
                {
                    return obj.Name.Equals(stateName);
                });

                if (state != null)
                {
                    currentStateName = stateName;
                    currentState.cameraMode = state.cameraMode;
                    lerpState = state; // set the state of transition (lerpstate) to the state finded on the list
                                       // in case there is no smooth, a copy will be make without the transition values
                    if (currentState != null && !hasSmooth)
                    {
                        currentState.CopyState(state);
                    }
                }
                else
                {
                    // if the state choosed if not real, the first state will be set up as default
                    if (CameraStateList.tpCameraStates.Count > 0)
                    {
                        state = CameraStateList.tpCameraStates[0];
                        currentStateName = state.Name;
                        currentState.cameraMode = state.cameraMode;
                        lerpState = state;
                        if (currentState != null && !hasSmooth)
                        {
                            currentState.CopyState(state);
                        }
                    }
                }
                // in case a list of states does not exist, a default state will be created
                if (currentState == null)
                {
                    currentState = new vThirdPersonCameraState("Null");
                    currentStateName = currentState.Name;
                }

                indexList = CameraStateList.tpCameraStates.IndexOf(state);
                currentZoom = state.defaultDistance;
                currentState.fixedAngle = new Vector3(mouseX, mouseY);
                indexLookPoint = 0;
            }

            if (currentState.cameraMode == TPCameraMode.FixedPoint)
            {
                var point = currentState.lookPoints.Find(delegate (LookPoint obj)
                {
                    return obj.pointName.Equals(pointName);
                });
                if (point != null)
                {
                    indexLookPoint = currentState.lookPoints.IndexOf(point);
                }
                else
                {
                    indexLookPoint = 0;
                }
            }
        }

        protected virtual IEnumerator ResetFirstState()
        {
            yield return new WaitForEndOfFrame();
            firstStateIsInit = true;
        }

        /// <summary>
        /// Change the lookAtPoint of current state if cameraMode is FixedPoint
        /// </summary>
        /// <param name="pointName"></param>
        public virtual void ChangePoint(string pointName)
        {
            if (currentState == null || currentState.cameraMode != TPCameraMode.FixedPoint || currentState.lookPoints == null)
            {
                return;
            }

            var point = currentState.lookPoints.Find(delegate (LookPoint obj) { return obj.pointName.Equals(pointName); });
            if (point != null)
            {
                indexLookPoint = currentState.lookPoints.IndexOf(point);
            }
            else
            {
                indexLookPoint = 0;
            }
        }

        public virtual void FreezeCamera()
        {
            isFreezed = true;
            if (mainTarget)
            {
                lastLookAtForward = mainTarget.InverseTransformDirection(targetLookAt.forward);
                lastLookAtPosition = mainTarget.InverseTransformPoint(targetLookAt.position);
                current_cPos = mainTarget.InverseTransformPoint(current_cPos);
                desired_cPos = mainTarget.InverseTransformPoint(desired_cPos);
            }
        }

        public virtual void UnFreezeCamera()
        {
            if (mainTarget)
            {
                targetLookAt.forward = mainTarget.TransformDirection(lastLookAtForward);
                targetLookAt.position = mainTarget.TransformPoint(lastLookAtPosition);
                current_cPos = mainTarget.TransformPoint(current_cPos);
                desired_cPos = mainTarget.TransformPoint(desired_cPos);
            }
            isFreezed = false;
        }

        /// <summary>    
        /// Zoom behavior 
        /// </summary>
        /// <param name="scroolValue"></param>
        /// <param name="zoomSpeed"></param>
        public virtual void Zoom(float scroolValue)
        {
            currentZoom -= scroolValue * scrollSpeed;
        }

        public virtual void CheckCameraIsRotating()
        {
            cameraIsRotating = (transform.eulerAngles - lastCameraRotation.eulerAngles).magnitude > 0.1 || movementSpeed.magnitude > 0;
            lastCameraRotation.eulerAngles = transform.eulerAngles;
        }

        /// <summary>
        /// Camera Rotation behaviour
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public virtual void RotateCamera(float x, float y)
        {
            if (currentState.cameraMode.Equals(TPCameraMode.FixedPoint) || !isInit || transformWeight < 1)
            {
                smoothCameraRotation = initialCameraRotation;
                return;
            }

            if (!currentState.cameraMode.Equals(TPCameraMode.FixedAngle))
            {
                // lock into a target            
                if (!lockTarget)
                {
                    // free rotation 
                    mouseX += x * (vInput.instance.inputDevice == InputDevice.Joystick ? currentState.xMouseSensitivity * joystickSensitivity : currentState.xMouseSensitivity);
                    mouseY -= y * (vInput.instance.inputDevice == InputDevice.Joystick ? currentState.yMouseSensitivity * joystickSensitivity : currentState.yMouseSensitivity);

                    movementSpeed.x = x;
                    movementSpeed.y = -y;

                    CheckCameraIsRotating();
                    var isAlignedWithTarget = (transform.forward - currentTarget.forward).magnitude <= 0.5f;

                    if (!LockCamera && cameraIsRotating)
                    {
                        lastRotationTimer = Time.time;

                        if (movementSpeed.x != 0 || movementSpeed.y != 0)
                        {
                            smoothCameraRotation = initialCameraRotation;
                        }

                        mouseY = vExtensions.ClampAngle(mouseY, lerpState.yMinLimit, lerpState.yMaxLimit);
                        mouseX = vExtensions.ClampAngle(mouseX, lerpState.xMinLimit, lerpState.xMaxLimit);
                    }
                    else if (LockCamera || !isAlignedWithTarget && autoBehindTarget)
                    {
                        if (autoBehindTarget)
                            smoothCameraRotation = Mathf.Lerp(smoothCameraRotation, behindTargetSmoothRotation, 6f * Time.fixedDeltaTime);

                        if (LockCamera || Time.time > lastRotationTimer + behindTargetDelay)
                        {
                            mouseY = currentTarget.eulerAngles.NormalizeAngle().x;
                            mouseX = currentTarget.eulerAngles.NormalizeAngle().y;
                        }
                    }
                }
                else
                {
                    smoothCameraRotation = initialCameraRotation;
                }
            }
            else
            {
                smoothCameraRotation = initialCameraRotation;
                // fixed rotation
                var _x = lerpState.fixedAngle.x;
                var _y = lerpState.fixedAngle.y;
                mouseX = useSmooth ? Mathf.LerpAngle(mouseX, _x, smoothBetweenState * Time.fixedDeltaTime) : _x;
                mouseY = useSmooth ? Mathf.LerpAngle(mouseY, _y, smoothBetweenState * Time.fixedDeltaTime) : _y;
            }

        }

        /// <summary>
        /// Switch Camera Right 
        /// </summary>
        /// <param name="value"></param>
        public virtual void SwitchRight(bool value = false)
        {
            switchRight = value ? -1 : 1;
        }

        protected virtual void CalculeLockOnPoint()
        {
            if (currentState.cameraMode.Equals(TPCameraMode.FixedAngle) && lockTarget)
            {
                return;   // check if angle of camera is fixed         
            }

            var collider = lockTarget.GetComponent<Collider>();                                  // collider to get center of bounds

            if (collider == null)
            {
                return;
            }

            var _point = collider.bounds.center;
            Vector3 relativePos = _point - (desired_cPos);                      // get position relative to transform
            Quaternion rotation = Quaternion.LookRotation(relativePos);         // convert to rotation

            //convert angle (360 to 180)
            var y = 0f;
            var x = rotation.eulerAngles.y;
            if (rotation.eulerAngles.x < -180)
            {
                y = rotation.eulerAngles.x + 360;
            }
            else if (rotation.eulerAngles.x > 180)
            {
                y = rotation.eulerAngles.x - 360;
            }
            else
            {
                y = rotation.eulerAngles.x;
            }

            if (lockTargetWeight < 1f)
            {
                lockTargetWeight += Time.fixedDeltaTime * lockTargetSpeed;
            }

            mouseY = Mathf.LerpAngle(mouseY, vExtensions.ClampAngle(y, currentState.yMinLimit, currentState.yMaxLimit), lockTargetWeight);
            mouseX = Mathf.LerpAngle(mouseX, vExtensions.ClampAngle(x, currentState.xMinLimit, currentState.xMaxLimit), lockTargetWeight);
        }

        public virtual void CameraMovement(bool forceUpdate = false)
        {
            if (currentTarget == null || targetCamera == null || (!firstStateIsInit && !forceUpdate))
            {
                return;
            }

            transformWeight = Mathf.Clamp(transformWeight += Time.fixedDeltaTime * startSmoothFactor, 0f, 1f);
            if (useSmooth)
            {
                currentState.Slerp(lerpState, smoothBetweenState * Time.fixedDeltaTime);
            }
            else
            {
                currentState.CopyState(lerpState);
            }

            if (currentState.useZoom)
            {
                currentZoom = Mathf.Clamp(currentZoom, currentState.minDistance, currentState.maxDistance);
                distance = useSmooth ? Mathf.Lerp(distance, currentZoom, lerpState.smooth * Time.fixedDeltaTime) : currentZoom;
            }
            else
            {
                distance = useSmooth ? Mathf.Lerp(distance, currentState.defaultDistance, lerpState.smooth * Time.fixedDeltaTime) : currentState.defaultDistance;
                currentZoom = currentState.defaultDistance;
            }

            targetCamera.fieldOfView = currentState.fov;
            cullingDistance = Mathf.Lerp(cullingDistance, currentZoom, smoothBetweenState * Time.fixedDeltaTime);
            currentSwitchRight = Mathf.Lerp(currentSwitchRight, switchRight, smoothSwitchSide * Time.fixedDeltaTime);
            var camDir = (currentState.forward * targetLookAt.forward) + ((currentState.right * currentSwitchRight) * targetLookAt.right);

            camDir = camDir.normalized;

            var targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y, currentTarget.position.z) + currentTarget.transform.up * offSetPlayerPivot;
            currentTargetPos = targetPos;
            desired_cPos = targetPos + currentTarget.transform.up * currentState.height;
            current_cPos = firstUpdated ? targetPos + currentTarget.transform.up * currentHeight : Vector3.SmoothDamp(current_cPos, targetPos + currentTarget.transform.up * currentHeight, ref cameraVelocityDamp, lerpState.smoothDamp * Time.fixedDeltaTime);
            firstUpdated = false;
            RaycastHit hitInfo;

            ClipPlanePoints planePoints = targetCamera.NearClipPlanePoints(current_cPos + (camDir * (distance)), clipPlaneMargin);
            ClipPlanePoints oldPoints = targetCamera.NearClipPlanePoints(desired_cPos + (camDir * currentZoom), clipPlaneMargin);

            if (!portalViewActive)
            {
                //Check if Height is not blocked 
                if (Physics.SphereCast(targetPos, checkHeightRadius, currentTarget.transform.up, out hitInfo, currentState.cullingHeight + 0.2f, cullingLayer))
                {
                    var t = hitInfo.distance - 0.2f;
                    t -= currentState.height;
                    t /= (currentState.cullingHeight - currentState.height);
                    cullingHeight = Mathf.Lerp(currentState.height, currentState.cullingHeight, Mathf.Clamp(t, 0.0f, 1.0f));
                }
                else
                {
                    cullingHeight = useSmooth ? Mathf.Lerp(cullingHeight, currentState.cullingHeight, smoothBetweenState * Time.fixedDeltaTime) : currentState.cullingHeight;
                }
                //Check if desired target position is not blocked            
                if (CullingRayCast(desired_cPos, oldPoints, out hitInfo, currentZoom + 0.2f, cullingLayer, Color.blue))
                {
                    var dist = hitInfo.distance;
                    if (dist < currentState.defaultDistance)
                    {
                        var t = dist;
                        t -= currentState.cullingMinDist;
                        t /= (currentZoom - currentState.cullingMinDist);
                        currentHeight = Mathf.Lerp(cullingHeight, currentState.height, Mathf.Clamp(t, 0.0f, 1.0f));
                        current_cPos = targetPos + currentTarget.transform.up * currentHeight;
                    }
                }
                else
                {
                    currentHeight = useSmooth ? Mathf.Lerp(currentHeight, currentState.height, smoothBetweenState * Time.fixedDeltaTime) : currentState.height;
                }

                if (cullingDistance < distance)
                {
                    distance = cullingDistance;
                }

                //Check if target position with culling height applied is not blocked
                if (CullingRayCast(current_cPos, planePoints, out hitInfo, distance, cullingLayer, Color.cyan))
                {
                    distance = Mathf.Clamp(cullingDistance, 0.0f, currentZoom);
                }
            }
            else
            {
                // 处于传送门相交状态：忽略对墙体的碰撞推回，直接使用目标高度与缩放
                cullingHeight = useSmooth ? Mathf.Lerp(cullingHeight, currentState.cullingHeight, smoothBetweenState * Time.fixedDeltaTime) : currentState.cullingHeight;
                currentHeight = useSmooth ? Mathf.Lerp(currentHeight, currentState.height, smoothBetweenState * Time.fixedDeltaTime) : currentState.height;
                current_cPos = targetPos + currentTarget.transform.up * currentHeight;
                cullingDistance = currentZoom;
                // 不修改 distance（保持基于缩放/默认的值），不再进行任何 CullingRayCast
            }

            var lookPoint = current_cPos + targetLookAt.forward * targetCamera.farClipPlane;
            lookPoint += (targetLookAt.right * Vector3.Dot(camDir * (distance), targetLookAt.right));
            targetLookAt.position = current_cPos;

            float _mouseY = Mathf.LerpAngle(mouseYStart, mouseY, transformWeight);
            float _mouseX = Mathf.LerpAngle(mouseXStart, mouseX, transformWeight);
            Quaternion newRot = Quaternion.Euler(_mouseY + offsetMouse.y, _mouseX + offsetMouse.x, 0);
            targetLookAt.rotation = useSmooth ? Quaternion.Lerp(targetLookAt.rotation, newRot, smoothCameraRotation * Time.fixedDeltaTime) : newRot;
            selfRigidbody.MovePosition(Vector3.Lerp(startPosition, current_cPos + (camDir * (distance)), transformWeight));
            var rotation = Quaternion.LookRotation((lookPoint) - selfRigidbody.position);

            if (lockTarget)
            {
                CalculeLockOnPoint();

                if (!(currentState.cameraMode.Equals(TPCameraMode.FixedAngle)))
                {
                    var collider = lockTarget.GetComponent<Collider>();
                    if (collider != null)
                    {
                        var point = (collider.bounds.center + Vector3.up * heightOffset) - selfRigidbody.position;
                        var euler = Quaternion.LookRotation(point).eulerAngles - rotation.eulerAngles;
                        if (isNewTarget)
                        {
                            lookTargetAdjust.x = Mathf.LerpAngle(lookTargetAdjust.x, euler.x, lockTargetWeight);
                            lookTargetAdjust.y = Mathf.LerpAngle(lookTargetAdjust.y, euler.y, lockTargetWeight);
                            lookTargetAdjust.z = Mathf.LerpAngle(lookTargetAdjust.z, euler.z, lockTargetWeight);
                            // Quaternion.LerpUnclamped(lookTargetAdjust, Quaternion.Euler(euler), currentState.smoothFollow * Time.deltaTime);
                            if (Vector3.Distance(lookTargetAdjust, euler) < .5f)
                            {
                                isNewTarget = false;
                            }
                        }
                        else
                        {
                            lookTargetAdjust = euler;
                        }
                    }
                }
            }
            else
            {
                lookTargetAdjust.x = Mathf.LerpAngle(lookTargetAdjust.x, 0, currentState.smooth * Time.fixedDeltaTime);
                lookTargetAdjust.y = Mathf.LerpAngle(lookTargetAdjust.y, 0, currentState.smooth * Time.fixedDeltaTime);
                lookTargetAdjust.z = Mathf.LerpAngle(lookTargetAdjust.z, 0, currentState.smooth * Time.fixedDeltaTime);
                //lookTargetAdjust = Quaternion.LerpUnclamped(lookTargetAdjust, Quaternion.Euler(Vector3.zero), 1 * Time.deltaTime);
            }
            var _euler = rotation.eulerAngles + lookTargetAdjust;
            _euler.z = 0;
            var _rot = Quaternion.Euler(_euler + currentState.rotationOffSet);
            selfRigidbody.MoveRotation(Quaternion.Lerp(startRotation, _rot, transformWeight));
            movementSpeed = Vector2.zero;

            // 检测：从玩家到相机的向量是否与传送门相交，且玩家位于传送门正方向
            CheckPlayerToCameraPortalIntersection();
        }

        /// <summary>
        /// 从玩家位置沿指向相机的方向发射射线，检测是否与传送门相交，且玩家在传送门正方向。
        /// </summary>
        protected virtual void CheckPlayerToCameraPortalIntersection()
        {
            if (currentTarget == null)
            {
                return;
            }

            Vector3 playerPos = currentTarget.position;
            Vector3 cameraPos = selfRigidbody.position;
            Vector3 toCamera = cameraPos - playerPos;
            float distanceToCamera = toCamera.magnitude;
            if (distanceToCamera <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 dir = toCamera / distanceToCamera;

            var hits = Physics.RaycastAll(playerPos, dir, distanceToCamera, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var portal = hit.collider != null ? hit.collider.GetComponent<Portal>() : null;
                if (portal == null)
                {
                    continue;
                }

                Vector3 toPlayerFromPortal = playerPos - portal.transform.position;
                if (Vector3.Dot(portal.transform.forward, toPlayerFromPortal) > 0f)
                {
                    ActivatePortalView(portal);
                    return;
                }
            }

            DeactivatePortalView();
        }

        /// <summary>
        /// 启用并更新传送门虚拟相机：以对侧传送门为基准，映射当前相机的位置与旋转。
        /// </summary>
        /// <param name="fromPortal"></param>
        protected virtual void ActivatePortalView(Portal fromPortal)
        {
            if (fromPortal == null || !fromPortal.IsWorkable() || fromPortal.LinkedPortal == null)
            {
                DeactivatePortalView();
                return;
            }
            
            // 检查玩家体型是否为4.5级，如果是则禁用传送门虚拟相机
            if (playerObject)
            {
                var sizeController = playerObject.GetComponent<Xuwu.Character.CharacterSizeController>();
                if (sizeController)
                {
                    bool isLimitBreakerLevel5 = (sizeController.GetCurrentSizeLevel() == Xuwu.Character.CharacterSizeLevel.LimitBreaker && 
                                               sizeController.GetCurrentLimitBreakerLevel() == 5);
                    if (isLimitBreakerLevel5)
                    {
                        DeactivatePortalView();
                        return;
                    }
                }
            }

            activeFromPortal = fromPortal;
            activeToPortal = fromPortal.LinkedPortal;

            EnsurePortalVirtualCamera();
            if (!portalVirtualCamera)
            {
                return;
            }

            // 将当前相机的期望画面映射到另一侧
            Vector3 mappedPos = fromPortal.TransferPoint(selfRigidbody.position);
            Quaternion mappedRot = fromPortal.TransferRotation(selfRigidbody.rotation);

            portalVirtualCamera.transform.SetPositionAndRotation(mappedPos, mappedRot);
            portalVirtualCamera.fieldOfView = targetCamera.fieldOfView;

            // 替换画面：禁用主相机，启用虚拟相机
            if (!portalViewActive)
            {
                portalVirtualCamera.enabled = true;
                if (targetCamera)
                {
                    targetCamera.enabled = false;
                }
                portalViewActive = true;
            }
        }

        /// <summary>
        /// 关闭传送门虚拟相机，还原主相机。
        /// </summary>
        protected virtual void DeactivatePortalView()
        {
            if (!portalViewActive)
            {
                return;
            }

            if (portalVirtualCamera)
            {
                portalVirtualCamera.enabled = false;
            }
            if (targetCamera)
            {
                targetCamera.enabled = true;
            }
            activeFromPortal = null;
            activeToPortal = null;
            portalViewActive = false;
        }

        /// <summary>
        /// 懒加载并配置虚拟相机，复制主相机的渲染设置。
        /// </summary>
        protected virtual void EnsurePortalVirtualCamera()
        {
            if (portalVirtualCamera && portalVirtualCamera)
            {
                return;
            }

            var go = GameObject.Find("__PortalVirtualCamera__");
            if (!go)
            {
                go = new GameObject("__PortalVirtualCamera__");
                go.hideFlags = HideFlags.HideAndDontSave;
                portalVirtualCamera = go.AddComponent<Camera>();
            }
            else
            {
                portalVirtualCamera = go.GetComponent<Camera>();
                if (!portalVirtualCamera)
                {
                    portalVirtualCamera = go.AddComponent<Camera>();
                }
            }

            if (targetCamera)
            {
                portalVirtualCamera.CopyFrom(targetCamera);
                portalVirtualCamera.enabled = false;
            }
        }

        protected virtual void CameraFixed()
        {
            if (useSmooth)
            {
                currentState.Slerp(lerpState, smoothBetweenState);
            }
            else
            {
                currentState.CopyState(lerpState);
            }

            transformWeight = Mathf.Clamp(transformWeight += Time.fixedDeltaTime, 0f, 1f);
            var targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y + offSetPlayerPivot + currentState.height, currentTarget.position.z);
            currentTargetPos = useSmooth ? Vector3.MoveTowards(currentTargetPos, targetPos, currentState.smooth * Time.fixedDeltaTime) : targetPos;
            current_cPos = currentTargetPos;
            var pos = isValidFixedPoint ? currentState.lookPoints[indexLookPoint].positionPoint : transform.position;
            transform.position = Vector3.Lerp(startPosition, useSmooth ? Vector3.Lerp(transform.position, pos, currentState.smooth * Time.fixedDeltaTime) : pos, transformWeight);
            targetLookAt.position = current_cPos;
            if (isValidFixedPoint && currentState.lookPoints[indexLookPoint].freeRotation)
            {
                var rot = Quaternion.Euler(currentState.lookPoints[indexLookPoint].eulerAngle);
                transform.rotation = Quaternion.Lerp(startRotation, useSmooth ? Quaternion.Slerp(transform.rotation, rot, (currentState.smooth * 0.5f) * Time.fixedDeltaTime) : rot, transformWeight);
            }
            else if (isValidFixedPoint)
            {
                var rot = Quaternion.LookRotation(currentTargetPos - transform.position);
                transform.rotation = Quaternion.Lerp(startRotation, useSmooth ? Quaternion.Slerp(transform.rotation, rot, (currentState.smooth) * Time.fixedDeltaTime) : rot, transformWeight);
            }
            targetCamera.fieldOfView = currentState.fov;
        }

        protected virtual bool isValidFixedPoint
        {
            get
            {
                return (currentState.lookPoints != null && currentState.cameraMode.Equals(TPCameraMode.FixedPoint) && (indexLookPoint < currentState.lookPoints.Count || currentState.lookPoints.Count > 0));
            }
        }

        protected virtual bool CullingRayCast(Vector3 from, ClipPlanePoints _to, out RaycastHit hitInfo, float distance, LayerMask cullingLayer, Color color)
        {
            bool value = false;
            if (showGizmos)
            {
                Debug.DrawRay(from, _to.LowerLeft - from, color);
                Debug.DrawLine(_to.LowerLeft, _to.LowerRight, color);
                Debug.DrawLine(_to.UpperLeft, _to.UpperRight, color);
                Debug.DrawLine(_to.UpperLeft, _to.LowerLeft, color);
                Debug.DrawLine(_to.UpperRight, _to.LowerRight, color);
                Debug.DrawRay(from, _to.LowerRight - from, color);
                Debug.DrawRay(from, _to.UpperLeft - from, color);
                Debug.DrawRay(from, _to.UpperRight - from, color);
            }
            if (Physics.Raycast(from, _to.LowerLeft - from, out hitInfo, distance, cullingLayer))
            {
                value = true;
                cullingDistance = hitInfo.distance;
            }

            if (Physics.Raycast(from, _to.LowerRight - from, out hitInfo, distance, cullingLayer))
            {
                value = true;
                if (cullingDistance > hitInfo.distance)
                {
                    cullingDistance = hitInfo.distance;
                }
            }

            if (Physics.Raycast(from, _to.UpperLeft - from, out hitInfo, distance, cullingLayer))
            {
                value = true;
                if (cullingDistance > hitInfo.distance)
                {
                    cullingDistance = hitInfo.distance;
                }
            }

            if (Physics.Raycast(from, _to.UpperRight - from, out hitInfo, distance, cullingLayer))
            {
                value = true;
                if (cullingDistance > hitInfo.distance)
                {
                    cullingDistance = hitInfo.distance;
                }
            }

            return hitInfo.collider && value;
        }
    }
}