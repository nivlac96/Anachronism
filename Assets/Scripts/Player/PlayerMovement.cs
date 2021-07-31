using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour {

	public CharacterController2D Controller;
	public Animator Animator;

    [Tooltip("The starting speed when you move from standing still")]
	public float RunSpeedBase = 30f;
	[Tooltip("The max speed from running. If you gain extra speed from maneuvers, you will decrease back to this plateau if all you do is run.")]
	public float RunSpeedStandard = 40f;
	[Tooltip("How many units the speed increases by per second while running until reaching runSpeedStandard")]
	public float SpeedRampUpPerSec = 7f;
	[Tooltip("How many units the speed decreases by per second while running faster than standard")]
	public float SpeedDecayPerSec = 5f;
    [Tooltip("How quickly the character can switch directions")]
    public float TurnAroundSpeedPerSec = 70f;
    [Tooltip("How much time in seconds can a player hold still for before their speed resets to Base.")]
	public float ResetSpeedAfter = 0.2f;
    [Tooltip("The amount of time the jump button must be held before capping out jump height")]
    public float TimeToFullJump = 0.3f;


	private Rigidbody2D _rigidbodyRef;
	private float _currentAllowedRunSpeed;
	private float _speedResetTimer = 0;
	private float _horizontalRaw = 0f;
	private float _horizontalMove = 0f;
    private float _lastDirection = 0f;
    private bool _jumpIsHeld = false;
	private bool _dash = false;
	private bool _launchGrapple = false;
	private bool _releaseGrapple = false;
	private bool _startSlide = false;
	private bool _endSlide = false;
    private float _jumpButtonHeldFor = 0;


    //bool dashAxis = false;
    private void Awake()
    {
		_currentAllowedRunSpeed = RunSpeedBase;
        _rigidbodyRef = GetComponent<Rigidbody2D>();

		// Most input handlers are triggered by SendMessage, but this was the best way I could find to handle a "button release" event.
		InputAction grappleInput = GetComponent<PlayerInput>().currentActionMap["Grapple"];
		InputAction slideInput = GetComponent<PlayerInput>().currentActionMap["Slide"];
		InputAction jumpInput = GetComponent<PlayerInput>().currentActionMap["Jump"];
		
		grappleInput.canceled += OnReleaseGrapple;
		slideInput.canceled += OnReleaseSlide;
        jumpInput.canceled += OnReleaseJump;

	}

	// Update is called once per frame
	void Update () 
	{
		DetermineCurrentRunSpeed();
        if (_jumpIsHeld)
        {
            _jumpButtonHeldFor += Time.deltaTime;
            if (_jumpButtonHeldFor > TimeToFullJump)
            {
                OnReleaseJump(new InputAction.CallbackContext());
            }
        }
    }

    // These Input handlers are triggered by the SendMessage system of PlayerInput when the player presses a button.
	public void OnMove(InputValue input)
    {
		_horizontalRaw = input.Get<float>();
	}

    public void OnJump()
    {
		_jumpIsHeld = true;
	}

    public void OnReleaseJump(InputAction.CallbackContext c)
    {
        // If the player holds the jump button longer than TimeToFulJump seconds, _jumpIsHeld will be false,
        // and though this function is called, this if statement will be false.
        if (_jumpIsHeld is true)
        {
            _jumpIsHeld = false;
            _jumpButtonHeldFor = 0;
        }
    }

	public void OnDash()
    {
		_dash = true;
    }

	public void OnGrapple()
    {
		_launchGrapple = true;
    }

	// This Input handler is not called by the SendMessage system like the others
	// See how it is initialized in Awake()
	public void OnReleaseGrapple(InputAction.CallbackContext c)
    {
		_releaseGrapple = true;
    }

	public void OnStartSlide() { }
	public void OnReleaseSlide(InputAction.CallbackContext c) { }

	public void DetermineCurrentRunSpeed()
    {
        // Rigidbody speed from slopes, swings, etc. can boost running speed
        float actualMovementSpeed = _rigidbodyRef.velocity.magnitude;
        _currentAllowedRunSpeed = Mathf.Max(actualMovementSpeed, _currentAllowedRunSpeed);
        //print(rigidbody.velocity.magnitude);

        // If player is not giving directional input
		if (_horizontalRaw == 0)
		{
            if (Mathf.Abs(_horizontalMove) > RunSpeedBase)
            {
                _currentAllowedRunSpeed = Mathf.Max(RunSpeedBase, _currentAllowedRunSpeed - SpeedDecayPerSec * Time.deltaTime);
                _horizontalMove = _lastDirection * _currentAllowedRunSpeed;
            }
            else
            {
                _horizontalMove = 0;
            }
		}

        // If player indicates they want to go in a direction they are not going
        else if (_horizontalMove != 0 && Mathf.Sign(_horizontalRaw) != Mathf.Sign(_horizontalMove))
        {
            Debug.Log("Switch: move: " + _horizontalMove + " raw: " + _horizontalRaw);
            _horizontalMove = _horizontalMove + (_horizontalRaw * TurnAroundSpeedPerSec * Time.deltaTime);
        }

		else
        {
			_speedResetTimer = 0;
			// Decay speed if player is going above the standard
			if (_currentAllowedRunSpeed > RunSpeedStandard)
			{
				_currentAllowedRunSpeed = Mathf.Max(RunSpeedStandard, _currentAllowedRunSpeed - SpeedDecayPerSec * Time.deltaTime);
			}
			// Ramp up speed if the player is moving slower than standard
			else if (_currentAllowedRunSpeed < RunSpeedStandard)
			{
				_currentAllowedRunSpeed = Mathf.Min(RunSpeedStandard, _currentAllowedRunSpeed + SpeedRampUpPerSec * Time.deltaTime);
			}
            _horizontalMove = _horizontalRaw * _currentAllowedRunSpeed;
            _lastDirection = Mathf.Sign(_horizontalRaw);
        }
		Animator.SetFloat("Speed", Mathf.Abs(_horizontalMove));

	}

	public void OnFall()
	{
        Animator.SetBool("IsJumping", true);
    }

    public void OnLanding()
	{
        Animator.SetBool("IsJumping", false);
    }

    void FixedUpdate ()
	{
		// Move our character
		Controller.Move(_horizontalMove * Time.fixedDeltaTime, _jumpIsHeld, _dash, _launchGrapple, _releaseGrapple);
		_dash = false;
		_launchGrapple = false;
		_releaseGrapple = false;
	}
}
