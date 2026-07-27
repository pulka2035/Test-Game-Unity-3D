using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float jumpForce = 8f;
    public float gravity = -20f;

    [Header("Camera Settings")]
    public float cameraDistance = 5f;
    public float cameraHeight = 1.5f;
    public float cameraSensitivity = 2f;
    public float minCameraAngle = -30f;
    public float maxCameraAngle = 70f;
    public bool invertLookX = false;
    public bool invertLookY = false;

    [Header("Rotation Settings")]
    public float characterRotationSpeed = 10f;
    public bool rotateWhenMoving = true;

    [Header("Movement Smoothing")]
    public float acceleration = 10f;
    public float deceleration = 20f;
    public float stopThreshold = 0.1f;

    [Header("Flight Settings")]
    public float flightNormalSpeed = 12f;
    public float flightBoostSpeed = 20f;
    public float flightSideSpeed = 10f;
    public float flightBackwardSpeed = 6f;
    public float flightAscentSpeed = 8f;
    public float flightDescentSpeed = 4f;
    public float maxFlightHeight = 50f;
    public float takeoffHeight = 6f;
    public float takeoffTime = 1.5f;
    public float hoverOscillationSpeed = 2f;
    public float hoverOscillationAmount = 0.3f;
    public float flightTransitionTime = 0.5f;
    public float flightInertia = 0.08f;
    public float flightBankingAmount = 8f;

    [Header("Flight Responsiveness")]
    public float forwardResponsiveness = 0.15f;
    public float sideResponsiveness = 0.12f;
    public float backwardResponsiveness = 0.12f;
    public float flightStopSpeed = 30f;

    private CharacterController _characterController;
    private Camera _mainCamera;

    private Transform _cameraPivot;
    private Transform _characterModel;

    private float _verticalRotation = 0f;
    private float _horizontalRotation = 0f;
    private float _characterRotationY = 0f;

    private Vector3 _velocity;
    private bool _isRunning = false;
    private bool _isFlightBoosting = false;
    private Vector3 _moveDirection;
    private Vector3 _currentVelocity;
    private Vector3 _lastMoveDirection;

    // Переменные для полета
    private bool _isFlying = false;
    private bool _isTakingOff = false;
    private float _takeoffTimer = 0f;
    private float _originalHeight;
    private float _targetFlightHeight;
    private float _currentFlightHeight;
    private float _hoverTimer = 0f;
    private float _flightTransitionTimer = 0f;
    private float _spaceHoldTime = 0f;
    private bool _spaceWasPressed = false; // БЫЛО нажатие пробела
    private bool _jumpQueued = false;      // ОЧЕРЕДЬ для прыжка
    private Vector3 _flightInput;
    private float _currentBankAngle = 0f;
    private const float SPACE_HOLD_THRESHOLD = 1.0f; // Увеличил до 1 секунды

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
            _characterController.center = new Vector3(0, 1f, 0);
            _characterController.height = 2f;
            _characterController.radius = 0.3f;
        }

        SetupHierarchy();
    }

    void SetupHierarchy()
    {
        GameObject pivotObj = new GameObject("CameraPivot");
        _cameraPivot = pivotObj.transform;
        _cameraPivot.SetParent(transform);
        _cameraPivot.localPosition = new Vector3(0, cameraHeight, 0);
        _cameraPivot.localRotation = Quaternion.identity;

        GameObject modelObj = new GameObject("CharacterModel");
        _characterModel = modelObj.transform;
        _characterModel.SetParent(transform);
        _characterModel.localPosition = Vector3.zero;
        _characterModel.localRotation = Quaternion.identity;

        MoveVisualsToCharacterModel();
    }

    void MoveVisualsToCharacterModel()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != _cameraPivot && child != _characterModel)
            {
                child.SetParent(_characterModel);
            }
        }
    }

    void Start()
    {
        _mainCamera = Camera.main;

        if (_mainCamera != null)
        {
            _mainCamera.transform.SetParent(_cameraPivot);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _characterRotationY = transform.eulerAngles.y;

        _currentFlightHeight = transform.position.y;
        _targetFlightHeight = _currentFlightHeight;
    }

    void Update()
    {
        if (_characterController == null || _mainCamera == null) return;

        HandleInput();           // Обработка ввода
        HandleJumpLogic();       // НОВЫЙ: Логика прыжка
        HandleFlight();
        HandleMovement();
        ApplyGravity();
        ApplyMovement();

        HandleMouseWheel();
        UpdateFlightTransition();
        UpdateFlightBanking();
        HandleCamera(); 
    }

    void HandleInput()
    {
        // Обработка нажатия пробела
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _spaceWasPressed = true;
            _spaceHoldTime = 0f;
        }

        // Обработка удержания пробела
        if (Input.GetKey(KeyCode.Space))
        {
            _spaceHoldTime += Time.deltaTime;

            // Если удерживаем пробел больше 1 секунды и не летим - готовим взлет
            if (_spaceHoldTime >= SPACE_HOLD_THRESHOLD &&
                _characterController.isGrounded &&
                !_isFlying &&
                !_isTakingOff)
            {
                // Отменяем прыжок, если планировался
                _jumpQueued = false;
            }
        }

        // Обработка отпускания пробела
        if (Input.GetKeyUp(KeyCode.Space))
        {
            // Если нажали и быстро отпустили (менее 1 секунды) - ставим прыжок в очередь
            if (_spaceHoldTime < SPACE_HOLD_THRESHOLD &&
                _spaceWasPressed &&
                _characterController.isGrounded &&
                !_isFlying &&
                !_isTakingOff)
            {
                _jumpQueued = true;
            }

            _spaceWasPressed = false;
        }

        // В полете: пробел - подъем, Shift - снижение
        if (_isFlying && !_isTakingOff)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                _targetFlightHeight = Mathf.Min(_targetFlightHeight + flightAscentSpeed * Time.deltaTime, maxFlightHeight);
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                _targetFlightHeight = Mathf.Max(_targetFlightHeight - flightDescentSpeed * Time.deltaTime, 0f);
            }
        }

        // Бег на земле и ускорение в полете
        _isRunning = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        _isFlightBoosting = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    void HandleJumpLogic()
    {
        // Если есть прыжок в очереди - выполняем его
        if (_jumpQueued && _characterController.isGrounded && !_isFlying && !_isTakingOff)
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            _jumpQueued = false;
            Debug.Log("Прыжок!");
        }

        // Если удерживаем пробел больше 1 секунды и на земле - начинаем взлет
        if (_spaceHoldTime >= SPACE_HOLD_THRESHOLD &&
            Input.GetKey(KeyCode.Space) &&
            _characterController.isGrounded &&
            !_isFlying &&
            !_isTakingOff)
        {
            StartTakeoff();
            _spaceHoldTime = 0f; // Сбрасываем таймер после начала взлета
        }
    }

    void StartTakeoff()
    {
        _isTakingOff = true;
        _isFlying = true;
        _takeoffTimer = 0f;
        _originalHeight = transform.position.y;
        _targetFlightHeight = _originalHeight + takeoffHeight;
        _currentFlightHeight = _originalHeight;

        _velocity.y = 0;

        Debug.Log("Взлет начался (без прыжка)!");
    }

    void HandleFlight()
    {
        if (!_isFlying) return;

        if (_isTakingOff)
        {
            _takeoffTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_takeoffTimer / takeoffTime);

            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            _currentFlightHeight = Mathf.Lerp(_originalHeight, _targetFlightHeight, easedProgress);

            _hoverTimer += Time.deltaTime * hoverOscillationSpeed;
            float hoverOffset = Mathf.Sin(_hoverTimer) * hoverOscillationAmount * progress;

            Vector3 newPosition = transform.position;
            newPosition.y = _currentFlightHeight + hoverOffset;
            transform.position = newPosition;

            if (progress >= 1f)
            {
                _isTakingOff = false;
                Debug.Log("Взлет завершен!");
            }
        }
        else
        {
            _currentFlightHeight = Mathf.Lerp(_currentFlightHeight, _targetFlightHeight, Time.deltaTime * 3f);

            _hoverTimer += Time.deltaTime * hoverOscillationSpeed;
            float hoverOffset = Mathf.Sin(_hoverTimer) * hoverOscillationAmount;

            Vector3 newPosition = transform.position;
            newPosition.y = _currentFlightHeight + hoverOffset;
            transform.position = newPosition;

            if (IsNearGround() && _targetFlightHeight <= transform.position.y + 1f)
            {
                StartLanding();
            }
        }
    }

    void UpdateFlightBanking()
    {
        if (!_isFlying || _isTakingOff) return;

        float targetBankAngle = -_flightInput.x * flightBankingAmount * 0.7f;

        _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBankAngle, Time.deltaTime * 8f);

        if (_characterModel != null)
        {
            Quaternion currentRotation = _characterModel.rotation;
            Vector3 euler = currentRotation.eulerAngles;
            euler.z = _currentBankAngle;
            _characterModel.rotation = Quaternion.Euler(euler);
        }
    }

    bool IsNearGround()
    {
        RaycastHit hit;
        float checkDistance = 3f;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, checkDistance))
        {
            return hit.distance < 2f;
        }

        return false;
    }

    void StartLanding()
    {
        _isFlying = false;
        _flightTransitionTimer = 0f;
        _targetFlightHeight = transform.position.y;

        Debug.Log("Начинаем посадку...");
    }

    void UpdateFlightTransition()
    {
        if (!_isFlying && _flightTransitionTimer < flightTransitionTime)
        {
            _flightTransitionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_flightTransitionTimer / flightTransitionTime);

            if (IsNearGround())
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, 10f))
                {
                    float groundHeight = hit.point.y;
                    float currentHeight = transform.position.y;

                    float newHeight = Mathf.Lerp(currentHeight, groundHeight, progress * 2f);
                    Vector3 newPosition = transform.position;
                    newPosition.y = newHeight;
                    transform.position = newPosition;

                    if (Mathf.Abs(currentHeight - groundHeight) < 0.2f)
                    {
                        _characterController.enabled = true;
                        _flightTransitionTimer = flightTransitionTime;
                        Debug.Log("Посадка завершена!");
                    }
                }
            }
        }
    }

    void HandleCamera()
    {
        if (_mainCamera == null || _cameraPivot == null) return;

        float mouseX = Input.GetAxis("Mouse X") * cameraSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * cameraSensitivity;

        if (invertLookX) mouseX = -mouseX;
        if (invertLookY) mouseY = -mouseY;

        _horizontalRotation += mouseX;
        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, minCameraAngle, maxCameraAngle);

        Quaternion cameraRotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);
        Vector3 cameraOffset = cameraRotation * new Vector3(0, 0, -cameraDistance);
        Vector3 cameraPosition = transform.position + new Vector3(0, cameraHeight, 0) + cameraOffset;

        _mainCamera.transform.position = cameraPosition;
        _mainCamera.transform.rotation = cameraRotation;
    }

    void HandleMovement()
    {
        if (_isTakingOff || _flightTransitionTimer < flightTransitionTime)
        {
            _currentVelocity = Vector3.zero;
            return;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector2 inputDirection = new Vector2(horizontal, vertical);
        bool hasInput = inputDirection.magnitude > 0.1f;

        if (hasInput)
        {
            if (inputDirection.magnitude > 1f) inputDirection.Normalize();

            _flightInput = new Vector3(horizontal, 0, vertical);

            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            _moveDirection = (forward * inputDirection.y + right * inputDirection.x).normalized;
            _lastMoveDirection = _moveDirection;

            if (_isFlying)
            {
                Vector3 modelForward = _characterModel.forward;
                Vector3 modelRight = _characterModel.right;

                modelForward.y = 0f;
                modelRight.y = 0f;
                modelForward.Normalize();
                modelRight.Normalize();

                float forwardAmount = Mathf.Clamp(inputDirection.y, -1f, 1f);
                float rightAmount = Mathf.Clamp(inputDirection.x, -1f, 1f);

                float currentForwardSpeed = flightNormalSpeed;

                if (_isFlightBoosting && forwardAmount > 0.1f)
                {
                    currentForwardSpeed = flightBoostSpeed;
                }

                Vector3 targetFlightDirection = Vector3.zero;

                if (forwardAmount > 0)
                {
                    targetFlightDirection += modelForward * forwardAmount * currentForwardSpeed;
                }
                else if (forwardAmount < 0)
                {
                    targetFlightDirection += modelForward * forwardAmount * flightBackwardSpeed;
                }

                if (Mathf.Abs(rightAmount) > 0.1f)
                {
                    targetFlightDirection += modelRight * rightAmount * flightSideSpeed;
                }

                float responsiveness = forwardResponsiveness;

                if (forwardAmount < 0)
                {
                    responsiveness = backwardResponsiveness;
                }
                else if (Mathf.Abs(rightAmount) > 0.1f && Mathf.Abs(forwardAmount) < 0.1f)
                {
                    responsiveness = sideResponsiveness;
                }

                float inertiaFactor = flightInertia;

                if (Mathf.Abs(rightAmount) > 0.1f)
                {
                    inertiaFactor *= 0.7f;
                }

                _currentVelocity = Vector3.Lerp(
                    _currentVelocity,
                    targetFlightDirection,
                    responsiveness * Time.deltaTime * (1f / inertiaFactor) * 10f
                );

                _moveDirection = targetFlightDirection.normalized;

                if (_characterModel != null)
                {
                    Vector3 cameraForward = _mainCamera.transform.forward;
                    cameraForward.y = 0f;
                    cameraForward.Normalize();

                    float targetAngle = Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
                    _characterRotationY = Mathf.LerpAngle(_characterRotationY, targetAngle,
                        characterRotationSpeed * Time.deltaTime * 2f);
                    _characterModel.rotation = Quaternion.Euler(0f, _characterRotationY, 0f);
                }
            }
            else
            {
                float targetSpeed = _isRunning ? runSpeed : walkSpeed;

                if (_moveDirection.magnitude > 0.1f && rotateWhenMoving && _characterModel != null)
                {
                    float targetAngle = Mathf.Atan2(_moveDirection.x, _moveDirection.z) * Mathf.Rad2Deg;
                    _characterRotationY = Mathf.LerpAngle(_characterRotationY, targetAngle,
                        characterRotationSpeed * Time.deltaTime);
                    _characterModel.rotation = Quaternion.Euler(0f, _characterRotationY, 0f);
                }

                float currentSpeed = _currentVelocity.magnitude;

                if (currentSpeed < targetSpeed - 0.1f)
                {
                    _currentVelocity = Vector3.Lerp(
                        _currentVelocity,
                        _moveDirection * targetSpeed,
                        acceleration * Time.deltaTime
                    );
                }
                else
                {
                    _currentVelocity = _moveDirection * targetSpeed;
                }
            }
        }
        else
        {
            _moveDirection = Vector3.zero;
            _flightInput = Vector3.zero;

            if (_isFlying)
            {
                if (_currentVelocity.magnitude > 0.1f)
                {
                    _currentVelocity = Vector3.Lerp(
                        _currentVelocity,
                        Vector3.zero,
                        flightStopSpeed * Time.deltaTime
                    );
                }
                else
                {
                    _currentVelocity = Vector3.zero;
                }

                _currentBankAngle = Mathf.Lerp(_currentBankAngle, 0f, Time.deltaTime * 10f);
                if (_characterModel != null)
                {
                    Quaternion currentRotation = _characterModel.rotation;
                    Vector3 euler = currentRotation.eulerAngles;
                    euler.z = _currentBankAngle;
                    _characterModel.rotation = Quaternion.Euler(euler);
                }
            }
            else
            {
                if (_currentVelocity.magnitude > stopThreshold)
                {
                    _currentVelocity = Vector3.Lerp(
                        _currentVelocity,
                        Vector3.zero,
                        deceleration * Time.deltaTime
                    );
                }
                else
                {
                    _currentVelocity = Vector3.zero;
                }
            }
        }
    }

    void ApplyMovement()
    {
        if (_characterController == null) return;

        if (_isFlying)
        {
            _characterController.enabled = false;

            Vector3 newPosition = transform.position + _currentVelocity * Time.deltaTime;

            newPosition.y = Mathf.Clamp(newPosition.y, 0f, maxFlightHeight);

            transform.position = newPosition;
        }
        else
        {
            if (!_characterController.enabled)
                _characterController.enabled = true;

            if (_characterController.enabled)
            {
                _characterController.Move((_currentVelocity + _velocity) * Time.deltaTime);
            }
        }
    }

    void ApplyGravity()
    {
        if (_isFlying) return;

        if (_characterController == null) return;

        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y += gravity * Time.deltaTime;
    }

    void HandleMouseWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            cameraDistance = Mathf.Clamp(cameraDistance - scroll * 2f, 2f, 20f);
        }
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

    public bool IsFlying()
    {
        return _isFlying;
    }

    public bool IsTakingOff()
    {
        return _isTakingOff;
    }

    public bool IsFlightBoosting()
    {
        return _isFlightBoosting;
    }

    public float GetFlightHeight()
    {
        return _currentFlightHeight;
    }

    public float GetTargetFlightHeight()
    {
        return _targetFlightHeight;
    }

    public Vector3 GetCameraForward()
    {
        return _mainCamera != null ? _mainCamera.transform.forward : Vector3.forward;
    }

    public Vector3 GetCharacterForward()
    {
        if (_characterModel != null)
        {
            return _characterModel.forward;
        }
        return transform.forward;
    }

    public void RotateCharacter(float angle)
    {
        if (_characterModel != null)
        {
            _characterRotationY = angle;
            _characterModel.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    public void SetCameraDistance(float distance)
    {
        cameraDistance = Mathf.Clamp(distance, 2f, 20f);
    }

    public void SetCameraHeight(float height)
    {
        cameraHeight = height;
        if (_cameraPivot != null)
        {
            _cameraPivot.localPosition = new Vector3(0, cameraHeight, 0);
        }
    }

    public void ResetCharacterRotation()
    {
        if (_characterModel != null && _mainCamera != null)
        {
            Vector3 cameraForward = _mainCamera.transform.forward;
            cameraForward.y = 0;
            float angle = Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
            RotateCharacter(angle);
        }
    }

    public float GetCameraHorizontalRotation()
    {
        return _horizontalRotation;
    }

    private static ThirdPersonController _instance;
    public static ThirdPersonController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ThirdPersonController>();
            }
            return _instance;
        }
    }
}