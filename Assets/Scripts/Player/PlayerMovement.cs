using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour {

	public CharacterController2D controller;
	public Animator animator;
    private Rigidbody2D rigidbody;

    [Tooltip("The starting speed when you move from standing still")]
	public float runSpeedBase = 30f;
	[Tooltip("The max speed from running. If you gain extra speed from maneuvers, you will decrease back to this plateau if all you do is run.")]
	public float runSpeedStandard = 40f;
	[Tooltip("How many units the speed increases by per second while running until reaching runSpeedStandard")]
	public float speedRampUpPerSec = 7f;
	[Tooltip("How many units the speed decreases by per second while running faster than standard")]
	public float speedDecayPerSec = 5f;
	[Tooltip("How much time in seconds can a player hold still for before their speed resets to Base.")]
	public float resetSpeedAfter = 0.2f;
	
	private float currentRunSpeed;
	private float speedResetTimer = 0;

	float horizontalMove = 0f;
	float horizontalRaw = 0f;
	bool jump = false;
	bool dash = false;
	bool launchGrapple = false;
	bool releaseGrapple = false;


    //bool dashAxis = false;
    private void Awake()
    {
		currentRunSpeed = runSpeedBase;
        rigidbody = GetComponent<Rigidbody2D>();

		// Most input handlers are triggered by SendMessage, but this was the best way I could find to handle a "button release" event.
		InputAction grappleInput = GetComponent<PlayerInput>().currentActionMap["Grapple"];
		grappleInput.canceled += OnReleaseGrapple;

	}

    // Update is called once per frame
    void Update () 
	{
		DetermineCurrentRunSpeed();
    }

    // These Input handlers are triggered by the SendMessage system of PlayerInput when the player presses a button.
	public void OnMove(InputValue input)
    {
		horizontalRaw = input.Get<float>();
	}

    public void OnJump()
    {
		jump = true;
	}

	public void OnDash()
    {
		dash = true;
    }

	public void OnGrapple()
    {
		launchGrapple = true;
    }

	// This Input handler is not called by the SendMessage system like the others
	// See how it is initialized in Awake()
	public void OnReleaseGrapple(InputAction.CallbackContext c)
    {
		releaseGrapple = true;
    }

	public void DetermineCurrentRunSpeed()
    {
		// Rigidbody speed from slopes, swings, etc. can boost running speed
		currentRunSpeed = Mathf.Max(rigidbody.velocity.magnitude, currentRunSpeed);
		//print(rigidbody.velocity.magnitude);

		// Return to base speed if player holds still for a moment
		if (horizontalRaw == 0 && currentRunSpeed >= runSpeedBase)
		{
			speedResetTimer += Time.deltaTime;
			if(speedResetTimer > resetSpeedAfter)
            {
				currentRunSpeed = runSpeedBase;
			}
		}
		else
        {
			speedResetTimer = 0;
			// Decay speed if player is going above the standard
			if (currentRunSpeed > runSpeedStandard)
			{
				currentRunSpeed = Mathf.Max(runSpeedStandard, currentRunSpeed - speedDecayPerSec * Time.deltaTime);
			}
			// Ramp up speed if the player is moving slower than standard
			else if (currentRunSpeed < runSpeedStandard)
			{
				currentRunSpeed = Mathf.Min(runSpeedStandard, currentRunSpeed + speedRampUpPerSec * Time.deltaTime);
			}
		}
		horizontalMove = horizontalRaw * currentRunSpeed;
		animator.SetFloat("Speed", Mathf.Abs(horizontalMove));

	}

	public void OnFall()
	{
        animator.SetBool("IsJumping", true);
    }

    public void OnLanding()
	{
        animator.SetBool("IsJumping", false);
    }

    void FixedUpdate ()
	{
		// Move our character
		controller.Move(horizontalMove * Time.fixedDeltaTime, jump, dash, launchGrapple, releaseGrapple);
        jump = false;
		dash = false;
		launchGrapple = false;
		releaseGrapple = false;
	}
}
