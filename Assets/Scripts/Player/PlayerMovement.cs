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
	[Tooltip("How much time in seconds can a player hold still for before their speed resets to Base.")]
	public float ResetSpeedAfter = 0.2f;

	private Rigidbody2D _rigidbodyRef;
	private float _currentRunSpeed;
	private float _speedResetTimer = 0;
	private float _horizontalRaw = 0f;
	private float _horizontalMove = 0f;
	private bool _jump = false;
	private bool _dash = false;
	private bool _launchGrapple = false;
	private bool _releaseGrapple = false;
	private bool _startSlide = false;
	private bool _endSlide = false;


    //bool dashAxis = false;
    private void Awake()
    {
		_currentRunSpeed = RunSpeedBase;
        _rigidbodyRef = GetComponent<Rigidbody2D>();

		// Most input handlers are triggered by SendMessage, but this was the best way I could find to handle a "button release" event.
		InputAction grappleInput = GetComponent<PlayerInput>().currentActionMap["Grapple"];
		InputAction slideInput = GetComponent<PlayerInput>().currentActionMap["Slide"];
		
		grappleInput.canceled += OnReleaseGrapple;
		slideInput.canceled += OnReleaseSlide;

	}

	// Update is called once per frame
	void Update () 
	{
		DetermineCurrentRunSpeed();
    }

    // These Input handlers are triggered by the SendMessage system of PlayerInput when the player presses a button.
	public void OnMove(InputValue input)
    {
		_horizontalRaw = input.Get<float>();
	}

    public void OnJump()
    {
		_jump = true;
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
		_currentRunSpeed = Mathf.Max(_rigidbodyRef.velocity.magnitude, _currentRunSpeed);
		//print(rigidbody.velocity.magnitude);

		// Return to base speed if player holds still for a moment
		if (_horizontalRaw == 0 && _currentRunSpeed >= RunSpeedBase)
		{
			_speedResetTimer += Time.deltaTime;
			if(_speedResetTimer > ResetSpeedAfter)
            {
				_currentRunSpeed = RunSpeedBase;
			}
		}
		else
        {
			_speedResetTimer = 0;
			// Decay speed if player is going above the standard
			if (_currentRunSpeed > RunSpeedStandard)
			{
				_currentRunSpeed = Mathf.Max(RunSpeedStandard, _currentRunSpeed - SpeedDecayPerSec * Time.deltaTime);
			}
			// Ramp up speed if the player is moving slower than standard
			else if (_currentRunSpeed < RunSpeedStandard)
			{
				_currentRunSpeed = Mathf.Min(RunSpeedStandard, _currentRunSpeed + SpeedRampUpPerSec * Time.deltaTime);
			}
		}
		_horizontalMove = _horizontalRaw * _currentRunSpeed;
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
		Controller.Move(_horizontalMove * Time.fixedDeltaTime, _jump, _dash, _launchGrapple, _releaseGrapple);
        _jump = false;
		_dash = false;
		_launchGrapple = false;
		_releaseGrapple = false;
	}
}
