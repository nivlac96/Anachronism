using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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
	bool jump = false;
	bool dash = false;
	bool launchGrapple = false;
	bool releaseGrapple = false;

    //bool dashAxis = false;
    private void Awake()
    {
		currentRunSpeed = runSpeedBase;
		rigidbody = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update () {

		DetermineCurrentRunSpeed();

        horizontalMove = Input.GetAxisRaw("Horizontal") * currentRunSpeed;

		animator.SetFloat("Speed", Mathf.Abs(horizontalMove));

		//if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
		if (Input.GetButtonDown("Jump"))
		{
			jump = true;
		}

		//if (Input.GetKeyDown(KeyCode.C))
		if (Input.GetButtonDown("Fire2"))
		{
			dash = true;
		}

		//if (Input.GetKeyDown(KeyCode.Space))
		if (Input.GetButtonDown("Fire1"))
		{
			launchGrapple = true;
		}

		//if (Input.GetKeyUp(KeyCode.Space))
		if (Input.GetButtonUp("Fire1"))
		{
			releaseGrapple = true;
		}

		/*if (Input.GetAxisRaw("Dash") == 1 || Input.GetAxisRaw("Dash") == -1) //RT in Unity 2017 = -1, RT in Unity 2019 = 1
		{
			if (dashAxis == false)
			{
				dashAxis = true;
				dash = true;
			}
		}
		else
		{
			dashAxis = false;
		}
		*/

	}

	public void DetermineCurrentRunSpeed()
    {
		// Rigidbody speed from slopes, swings, etc. can boost running speed
		currentRunSpeed = Mathf.Max(rigidbody.velocity.magnitude, currentRunSpeed);
		print(rigidbody.velocity.magnitude);

		// Return to base speed if player holds still for a moment
		if (Input.GetAxisRaw("Horizontal") == 0 && currentRunSpeed >= runSpeedBase)
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
