using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A class that handles the input for the player. All calculations are handled in CharacterController2D
/// </summary>
public class PlayerMovement : MonoBehaviour {

	public CharacterController2D Controller;
	public Animator Animator;
    public Camera MainCam;

    [Tooltip("The amount of time the jump button must be held before capping out jump height")]
    public float TimeToFullJump = 0.3f;




	private float _horizontalRaw = 0f;
    private bool _jumpIsHeld = false;
	private bool _dash = false;
	private bool _launchAnchorGrapple = false;
	private bool _launchSurfaceGrapple = false;
	private bool _releaseGrapple = false;
	private bool _startSlide = false;
	private bool _endSlide = false;
    private float _jumpButtonHeldFor = 0;


    //bool dashAxis = false;
    private void Awake()
    {
        

		// Most input handlers are triggered by SendMessage through the Input Actions window, but this was the best way I could find to handle a "button release" event.
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
		//DetermineCurrentMoveSpeed();
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
		_launchAnchorGrapple = true;
    }

    public void OnPrimaryFire()
    {
        //Vector3 mousePosition = mousePositionReference.action.ReadValue<Vector2>()
        Controller.TryToGrappleToSurface(MainCam.Sc(new Vector2(Mouse.current.position.x.value, Mouse.current.position.y)));
    }

	// This Input handler is not called by the SendMessage system like the others
	// See how it is initialized in Awake()
	public void OnReleaseGrapple(InputAction.CallbackContext c)
    {
		_releaseGrapple = true;
    }

	public void OnStartSlide() { }
	public void OnReleaseSlide(InputAction.CallbackContext c) { }



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
		if (_launchAnchorGrapple)
        {
            Controller.TryToGrappleToAnchorPoint();
        }
        else if (_releaseGrapple)
        {
            Controller.TryToReleaseGrapple();
        }
		Controller.Move(_horizontalRaw, _jumpIsHeld, _dash);
		//Controller.Move(_horizontalMove * Time.fixedDeltaTime, _jumpIsHeld, _dash, _launchGrapple, _releaseGrapple);
		_dash = false;
		_launchAnchorGrapple = false;
		_releaseGrapple = false;
	}
}
