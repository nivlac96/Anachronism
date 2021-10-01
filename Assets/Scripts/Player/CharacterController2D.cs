using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;
using System;

public class CharacterController2D : MonoBehaviour
{
    #region Public members

    [SerializeField] private float MinimumJumpForce = 400f;							// Amount of force added when the player jumps.
    [SerializeField] private float AdditionalJumpForcePerSec = 400f;				// Amount of force added per second as the player holds the jump button
    [SerializeField] private float DoubleJumpForce = 400f;				// Amount of force added per second as the player holds the jump button

	[Range(0, .3f)] [SerializeField] private float MovementSmoothing = .05f;	// How much to smooth out the movement
	[SerializeField] private LayerMask WhatIsGround;							// A mask determining what is ground to the character
	[SerializeField] private Transform GroundCheck;							// A position marking where to check if the player is grounded.
	[SerializeField] private Transform WallCheck;								//Posicion que controla si el personaje toca una pared
    [SerializeField] private float DashForce = 25f;

    [Header("Grapple Controls")]
	[Space]

	[Tooltip("The minimum Y distance a grapple point must be above for you to attach to it")]
	[SerializeField] private float MinHeightToGrapple = 5.0f;
	[Tooltip("The minimum Y distance a grapple point can be above for you to be able to attach to it ")]
	[SerializeField] private float MaxHeightToGrapple = 15.0f;
	[Tooltip("The maximum X distance an anchor point can be from you to grapple")]
	[SerializeField] private float MaxXToGrapple = 5.0f;
    [Tooltip("The shortest the grapple rope can be by default.")]
    [SerializeField] private float GrappleRopeMinLength = 7.0f;
    [Tooltip("Should the grapple automatically be released when the player touches ground?")]
	[SerializeField] private bool ReleaseGrappleOnLand = true;
	[Tooltip("Should the grapple automatically be released when the player swings into a wall?")]
	[SerializeField] private bool ReleaseGrappleOnWall = true;

	private DistanceJoint2D _grappleDistanceJoint = null;
	private bool _isGrappling = false;
	private GrappleRope _grappleRope = null;
	private Rigidbody2D[] _allAnchorPoints;
	private Rigidbody2D _currentGrappleAnchor;

    [Header("Speed Controls")]
	[Space]

    [Tooltip("The starting speed when you move from standing still")]
    public float RunSpeedBase = 30f;
    [Tooltip("The max speed from running. If you gain extra speed from maneuvers, you will decrease back to this plateau if all you do is run.")]
    public float RunSpeedStandard = 100f;
    [Tooltip("How many units the speed increases by per second while running until reaching runSpeedStandard")]
    public float SpeedRampUpPerSec = 40f;
    [Tooltip("How many units the speed decreases by per second while running faster than standard")]
    public float SpeedDecayPerSec = 70f;
    [Tooltip("How quickly the character can switch directions")]
    public float TurnAroundSpeedPerSec = 150f;
    [Tooltip("The amount of control the player has over horizontal movement in mid-air compared to on ground. Lower number means less mid-air control.")] [Range(0,1)]
    public float MidAirControlMultiplier = 0.5f;


    /// <summary> The signed amount the character will move this frame</summary>
    private float _horizontalMove = 0f;
    /// <summary> The signed units per sec the character is moving.</summary>
    private float _horizontalSpeed = 0f;
    /// <summary> The sign of the direction in which the player was travelling in the previous frame</summary>
    private float _lastDirection = 0f;
    /// <summary> I think this var is obsolete</summary>
    private float _currentAllowedRunSpeed;

    [Header("Events")]
	[Space]

	public UnityEvent OnFallEvent;
	public UnityEvent OnLandEvent;

    #endregion
    #region Private Members

    const float GROUNDED_RADIUS = .2f; // Radius of the overlap circle to determine if grounded
    private bool _grounded;            // Whether or not the player is grounded.
    private bool _firstJumpStarted = false;
    private Rigidbody2D _rigidbody2DRef;
    private bool _facingRight = true;  // For determining which way the player is currently facing.
    private Vector3 _velocity = Vector3.zero;
    private float _limitFallSpeed = 25f; // Limit fall speed

    public bool AllowDoubleJump = true; // public switch to dis/enable double jumping
    private bool _doubleJumpAvailable = true; // private variable to determine if double jump should be available
    
    private bool _canDash = true;
    private bool _isDashing = false; //If player is dashing
    private bool _isWallInFrontOfPlayer = false; //If there is a wall in front of the player
    private bool _isWallSliding = false; //If player is sliding in a wall
    private bool _oldWallSlidding = false; //If player is sliding in a wall in the previous frame
    private float _prevVelocityX = 0f;
    private bool _canCheckIfWallSliding = false; //For check if player is wallsliding

    public float HitPoints = 10f; //Life of the player
    public bool Invincible = false; //If player can die
    private bool _canMove = true; //If player can move

    private Animator _animator;
    public ParticleSystem ParticleJumpUp; //Trail particles
    public ParticleSystem ParticleJumpDown; //Explosion particles

    private float _jumpWallStartX = 0;
    private float _jumpWallDistX = 0; //Distance between player and wall
    private bool _limitVelOnWallJump = false; //For limit wall jump distance with low fps
    private bool _firstJumpCompleted;

    #endregion

    [System.Serializable]
	public class BoolEvent : UnityEvent<bool> { }

	private void Awake()
	{
		_rigidbody2DRef = GetComponent<Rigidbody2D>();
		_animator = GetComponent<Animator>();
		_grappleDistanceJoint = GetComponent<DistanceJoint2D>();
		_grappleRope = GetComponentInChildren<GrappleRope>();

        _currentAllowedRunSpeed = RunSpeedBase;

        var anchorPointObjs = GameObject.FindGameObjectsWithTag("Grappleable");
		_allAnchorPoints = new Rigidbody2D[anchorPointObjs.Length];
		for (int i = 0; i < anchorPointObjs.Length; i++)
        {
			_allAnchorPoints[i] = anchorPointObjs[i].GetComponent<Rigidbody2D>();
        }
		if (OnFallEvent == null)
			OnFallEvent = new UnityEvent();

		if (OnLandEvent == null)
			OnLandEvent = new UnityEvent();
	}


	private void FixedUpdate()
	{
		bool wasGrounded = _grounded;
		_grounded = false;

		// The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
		// This can be done using layers instead but Sample Assets will not overwrite your project settings.
		Collider2D[] colliders = Physics2D.OverlapCircleAll(GroundCheck.position, GROUNDED_RADIUS, WhatIsGround);
		for (int i = 0; i < colliders.Length; i++)
		{
            if (colliders[i].gameObject != gameObject)
            {
                _grounded = true;
            }
			if (!wasGrounded )
			{
				OnLandEvent.Invoke();
				if (!_isWallInFrontOfPlayer && !_isDashing) 
					ParticleJumpDown.Play();
                if (_rigidbody2DRef.velocity.y < 0f)
					_limitVelOnWallJump = false;
				if (ReleaseGrappleOnLand)
					TryToReleaseGrapple();

                _doubleJumpAvailable = true;
                _firstJumpStarted = false;
                _firstJumpCompleted = false;
            }
		}

        _isWallInFrontOfPlayer = false;

		if (!_grounded)
		{
			OnFallEvent.Invoke();
			Collider2D[] collidersWall = Physics2D.OverlapCircleAll(WallCheck.position, GROUNDED_RADIUS, WhatIsGround);
			for (int i = 0; i < collidersWall.Length; i++)
			{
				if (collidersWall[i].gameObject != null)
				{
					_isDashing = false;
					_isWallInFrontOfPlayer = true;
					if (ReleaseGrappleOnWall)
						TryToReleaseGrapple();
				}
			}
			_prevVelocityX = _rigidbody2DRef.velocity.x;
		}

        if (_limitVelOnWallJump)
        {
            if (_rigidbody2DRef.velocity.y < -0.5f)
                _limitVelOnWallJump = false;
            _jumpWallDistX = (_jumpWallStartX - transform.position.x) * transform.localScale.x;
            if (_jumpWallDistX < -0.5f && _jumpWallDistX > -1f)
            {
                _canMove = true;
            }
            else if (_jumpWallDistX < -1f && _jumpWallDistX >= -2f)
            {
                _canMove = true;
                _rigidbody2DRef.velocity = new Vector2(10f * transform.localScale.x, _rigidbody2DRef.velocity.y);
            }
            else if (_jumpWallDistX < -2f)
            {
                _limitVelOnWallJump = false;
                _rigidbody2DRef.velocity = new Vector2(0, _rigidbody2DRef.velocity.y);
            }
            else if (_jumpWallDistX > 0)
            {
                _limitVelOnWallJump = false;
                _rigidbody2DRef.velocity = new Vector2(0, _rigidbody2DRef.velocity.y);
            }
        }
	}


	public void Move(float lateralInput, bool jump, bool dash, bool launchGrapple, bool releaseGrapple)
	{
		if (_canMove) {
			if (launchGrapple)
            {
				TryToLaunchGrapple();
            }
			if (releaseGrapple)
            {
				TryToReleaseGrapple();
            }
			if (_isGrappling)
            {
				return;
            }
			if (dash && _canDash && !_isWallSliding)
			{
				StartCoroutine(DashCooldown());
			}
			// If crouching, check to see if the character can stand up
			if (_isDashing)
			{
				_rigidbody2DRef.velocity = new Vector2(transform.localScale.x * DashForce, 0);
			}
			// Control the player if grounded or airControl is turned on
			else
			{
                DetermineHorizontalMove(lateralInput);
                ControlLateralMovement();
			}

            // Jumping /////////////////////////////////////////
            if (_firstJumpStarted && !_firstJumpCompleted)
            {
                ContinueJump(jump);
            }

            else if (_grounded && jump)
			{
                StartJumpFromGround();  // Add a vertical force to the player.
            }
			
			else if (!_grounded && jump && _firstJumpCompleted && !_isWallSliding)
			{
                DoubleJumpIfAllowed();
			}
            ///////////////////////////////////////////////////

			else if (_isWallInFrontOfPlayer && !_grounded)
			{
				if (!_oldWallSlidding && _rigidbody2DRef.velocity.y < 0 || _isDashing)
				{
					_isWallSliding = true;
					WallCheck.localPosition = new Vector3(-WallCheck.localPosition.x, WallCheck.localPosition.y, 0);
					Flip();
					StartCoroutine(WaitToCheck(0.1f));
					_doubleJumpAvailable = true;
					_animator.SetBool("IsWallSliding", true);
				}
				_isDashing = false;

				if (_isWallSliding)
				{
					if (_horizontalMove * transform.localScale.x > 0.1f)
					{
						StartCoroutine(WaitToEndSliding());
					}
					else 
					{
						_oldWallSlidding = true;
						_rigidbody2DRef.velocity = new Vector2(-transform.localScale.x * 2, -5);
					}
				}

				if (jump && _isWallSliding)
				{
					_animator.SetBool("IsJumping", true);
					_animator.SetBool("JumpUp", true); 
					_rigidbody2DRef.velocity = new Vector2(0f, 0f);
					_rigidbody2DRef.AddForce(new Vector2(transform.localScale.x * MinimumJumpForce *1.2f, MinimumJumpForce));
					_jumpWallStartX = transform.position.x;
					_limitVelOnWallJump = true;
					_doubleJumpAvailable = true;
					_isWallSliding = false;
					_animator.SetBool("IsWallSliding", false);
					_oldWallSlidding = false;
					WallCheck.localPosition = new Vector3(Mathf.Abs(WallCheck.localPosition.x), WallCheck.localPosition.y, 0);
					_canMove = false;
				}
				else if (dash && _canDash)
				{
					_isWallSliding = false;
					_animator.SetBool("IsWallSliding", false);
					_oldWallSlidding = false;
					WallCheck.localPosition = new Vector3(Mathf.Abs(WallCheck.localPosition.x), WallCheck.localPosition.y, 0);
					_doubleJumpAvailable = true;
					StartCoroutine(DashCooldown());
				}
			}
			else if (_isWallSliding && !_isWallInFrontOfPlayer && _canCheckIfWallSliding) 
			{
				_isWallSliding = false;
				_animator.SetBool("IsWallSliding", false);
				_oldWallSlidding = false;
				WallCheck.localPosition = new Vector3(Mathf.Abs(WallCheck.localPosition.x), WallCheck.localPosition.y, 0);
				_doubleJumpAvailable = true;
			}
		}
	}

    private void TryToLaunchGrapple()
    {
		if (_isGrappling || _grounded) { return; }

		// loop through anchor points and see if any satisfy the distance constraints. If any are
		// found, attach the grapple to the closest one.
		float closest = Mathf.Infinity;
		float yDiff, distance;
		Vector2 currAnchorPt;
		Rigidbody2D closestAnchorPt = null;
		bool anchorPointWasFound = false;
		for (int i = 0; i < _allAnchorPoints.Length; i++)
        {
			currAnchorPt = _allAnchorPoints[i].transform.position;
			// check x distance
			if (Mathf.Abs(currAnchorPt.x - transform.position.x) <= MaxXToGrapple)
            {
				// check y distance
				yDiff = Mathf.Abs(currAnchorPt.y - transform.position.y);
				if (yDiff >= MinHeightToGrapple && yDiff <= MaxHeightToGrapple)
                {
					// check total distance against closest
					distance = Vector2.Distance(transform.position, currAnchorPt);
					if (distance < closest)
                    {
						closest = distance;
						closestAnchorPt = _allAnchorPoints[i];
						anchorPointWasFound = true;
                    }
                }
            }

        }
		if (anchorPointWasFound)
		{
			_currentGrappleAnchor = closestAnchorPt;
			_isGrappling = true;

			// Initialize the grapple joint, setting the closest anchor point as its connected body
			_grappleDistanceJoint.enabled = true;
			_grappleDistanceJoint.connectedBody = _currentGrappleAnchor;
			_grappleDistanceJoint.distance = Math.Max(closest, GrappleRopeMinLength);
			
			// Draw the grapple rope
			_grappleRope.StartDrawingRope(_grappleDistanceJoint.connectedBody.transform.position);
		}
	}

    /// <summary>
    /// Determine the player's horizontal movement based on a number of factors.\n
    /// TODO this funciton is messy as hell and should be streamlined once we have a better idea of what we're doing here
    /// </summary>
    /// <param name="horizontalInput">The raw input, a float from -1 (left) to 1 (right)</param>
    public void DetermineHorizontalMove(float horizontalInput)
    {
        float uInput = Mathf.Abs(horizontalInput);
        float inputSign = Mathf.Sign(horizontalInput);
        float rigBodSpeed = _rigidbody2DRef.velocity.x;
        float uRigBodSpeed = Mathf.Abs(rigBodSpeed);
        float rigBodSpeedSign = Mathf.Sign(rigBodSpeed);

        // Rigidbody speed from slopes, swings, etc. can boost running speed
        _horizontalSpeed = Mathf.Max(uRigBodSpeed, _horizontalSpeed);
        float uHorizontalSpeed = Mathf.Abs(_horizontalSpeed);
        float speedSign = Mathf.Sign(_horizontalSpeed);


        // Reduce the player's ability to control their speed if they are mid-air.
        float airControlScalar = _grounded ? 1 : MidAirControlMultiplier;

        // If player is not giving directional input
        if (horizontalInput == 0)
        {
            
            if (uHorizontalSpeed > RunSpeedBase)
            {
                _currentAllowedRunSpeed = Mathf.Max(RunSpeedBase, _currentAllowedRunSpeed - SpeedDecayPerSec * Time.deltaTime);
                _horizontalSpeed = _lastDirection * _currentAllowedRunSpeed;
            }
            else
            {
                _horizontalSpeed = 0;
            }
        }

        // horizontal input is nonzero and in the opposite direction the player is already moving
        else if (_horizontalSpeed != 0 && Mathf.Sign(horizontalInput) != Mathf.Sign(_horizontalSpeed))
        {
            _horizontalSpeed = _horizontalSpeed + (horizontalInput * TurnAroundSpeedPerSec * airControlScalar * Time.deltaTime);
        }

        // horizontal input is nonzero and in the same direction the player is already moving
        else
        {
            // Decay speed if player is going above the standard
            if (_currentAllowedRunSpeed > RunSpeedStandard)
            {
                _currentAllowedRunSpeed = Mathf.Max(RunSpeedStandard, _currentAllowedRunSpeed - SpeedDecayPerSec * Time.deltaTime);
            }
            // Ramp up speed if the player is moving slower than standard
            else if (_currentAllowedRunSpeed < RunSpeedStandard)
            {
                _currentAllowedRunSpeed = Mathf.Min(RunSpeedStandard, _currentAllowedRunSpeed + (airControlScalar * SpeedRampUpPerSec * Time.deltaTime));
            }
            _horizontalSpeed = horizontalInput * _currentAllowedRunSpeed;
            _lastDirection = Mathf.Sign(horizontalInput);
        }

        _horizontalMove = _horizontalSpeed * Time.fixedDeltaTime;
        _animator.SetFloat("Speed", Mathf.Abs(_horizontalSpeed / 100f));
    }

    private void ControlLateralMovement()
    {   
        if (_rigidbody2DRef.velocity.y < -_limitFallSpeed)
        {
            _rigidbody2DRef.velocity = new Vector2(_rigidbody2DRef.velocity.x, -_limitFallSpeed);
        }

        // Move the character by finding the target velocity
        Vector3 targetVelocity = new Vector2(_horizontalMove * 10f, _rigidbody2DRef.velocity.y);

        // And then smoothing it out and applying it to the character
        _rigidbody2DRef.velocity = Vector3.SmoothDamp(_rigidbody2DRef.velocity, targetVelocity, ref _velocity, MovementSmoothing);

        // If the input is moving the player right and the player is facing left...
        if (_horizontalMove > 0 && !_facingRight && !_isWallSliding)
        {
            // ... flip the player.
            Flip();
        }
        // Otherwise if the input is moving the player left and the player is facing right...
        else if (_horizontalMove < 0 && _facingRight && !_isWallSliding)
        {
            // ... flip the player.
            Flip();
        }
    }

    private void StartJumpFromGround()
    {
        _animator.SetBool("IsJumping", true);
        _animator.SetBool("JumpUp", true);
        _grounded = false;
        _firstJumpStarted = true;
        _rigidbody2DRef.AddForce(new Vector2(0f, MinimumJumpForce));
        _doubleJumpAvailable = true;
        ParticleJumpDown.Play();
        ParticleJumpUp.Play();
    }

    private void ContinueJump(bool jump)
    {
        if (jump)
        {
            _rigidbody2DRef.AddForce(new Vector2(0f, AdditionalJumpForcePerSec * Time.deltaTime));
        }
        else
        {
            _firstJumpCompleted = true;
        }
        
    }

    private void DoubleJumpIfAllowed()
    {
        if (AllowDoubleJump && _doubleJumpAvailable)
        {
            _doubleJumpAvailable = false;
            _rigidbody2DRef.velocity = new Vector2(_rigidbody2DRef.velocity.x, 0);
            _rigidbody2DRef.AddForce(new Vector2(0f, DoubleJumpForce));
            _animator.SetBool("IsDoubleJumping", true);
        }
    }

    private void TryToReleaseGrapple()
    {
		if (!_isGrappling) { return; }

		_currentGrappleAnchor = null;
		_isGrappling = false;

		_grappleDistanceJoint.connectedBody = null;
		_grappleDistanceJoint.enabled = false;
		
		_doubleJumpAvailable = true;

		_grappleRope.HideRope();
	}

    private void Flip()
	{
		// Switch the way the player is labelled as facing.
		_facingRight = !_facingRight;

		// Multiply the player's x local scale by -1.
		Vector3 theScale = transform.localScale;
		theScale.x *= -1;
		transform.localScale = theScale;
	}

	public void ApplyDamage(float damage, Vector3 position) 
	{
		if (!Invincible)
		{
			_animator.SetBool("Hit", true);
			HitPoints -= damage;
			Vector2 damageDir = Vector3.Normalize(transform.position - position) * 40f ;
			_rigidbody2DRef.velocity = Vector2.zero;
			_rigidbody2DRef.AddForce(damageDir * 10);
			if (HitPoints <= 0)
			{
				StartCoroutine(WaitToDead());
			}
			else
			{
				StartCoroutine(Stun(0.25f));
				StartCoroutine(MakeInvincible(1f));
			}
		}
	}

	IEnumerator DashCooldown()
	{
		_animator.SetBool("IsDashing", true);
		_isDashing = true;
		_canDash = false;
		yield return new WaitForSeconds(0.1f);
		_isDashing = false;
		yield return new WaitForSeconds(0.5f);
		_canDash = true;
	}

	IEnumerator Stun(float time) 
	{
		_canMove = false;
		yield return new WaitForSeconds(time);
		_canMove = true;
	}
	IEnumerator MakeInvincible(float time) 
	{
		Invincible = true;
		yield return new WaitForSeconds(time);
		Invincible = false;
	}
	IEnumerator WaitToMove(float time)
	{
		_canMove = false;
		yield return new WaitForSeconds(time);
		_canMove = true;
	}

	IEnumerator WaitToCheck(float time)
	{
		_canCheckIfWallSliding = false;
		yield return new WaitForSeconds(time);
		_canCheckIfWallSliding = true;
	}

	IEnumerator WaitToEndSliding()
	{
		yield return new WaitForSeconds(0.1f);
		_doubleJumpAvailable = true;
		_isWallSliding = false;
		_animator.SetBool("IsWallSliding", false);
		_oldWallSlidding = false;
		WallCheck.localPosition = new Vector3(Mathf.Abs(WallCheck.localPosition.x), WallCheck.localPosition.y, 0);
	}

	IEnumerator WaitToDead()
	{
		_animator.SetBool("IsDead", true);
		_canMove = false;
		Invincible = true;
		GetComponent<Attack>().enabled = false;
		yield return new WaitForSeconds(0.4f);
		_rigidbody2DRef.velocity = new Vector2(0, _rigidbody2DRef.velocity.y);
		yield return new WaitForSeconds(1.1f);
		SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
	}
}
