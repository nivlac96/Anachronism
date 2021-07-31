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
	[SerializeField] private bool AirControl = false;							// Whether or not a player can steer while jumping;
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


	public void Move(float move, bool jump, bool dash, bool launchGrapple, bool releaseGrapple)
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
			else if (_grounded || AirControl)
			{
                ControlLateralMovement(move);
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
					if (move * transform.localScale.x > 0.1f)
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

    private void ControlLateralMovement(float move)
    {
        if (_rigidbody2DRef.velocity.y < -_limitFallSpeed)
        {
            _rigidbody2DRef.velocity = new Vector2(_rigidbody2DRef.velocity.x, -_limitFallSpeed);
        }

        // Move the character by finding the target velocity
        Vector3 targetVelocity = new Vector2(move * 10f, _rigidbody2DRef.velocity.y);

        // And then smoothing it out and applying it to the character
        _rigidbody2DRef.velocity = Vector3.SmoothDamp(_rigidbody2DRef.velocity, targetVelocity, ref _velocity, MovementSmoothing);

        // If the input is moving the player right and the player is facing left...
        if (move > 0 && !_facingRight && !_isWallSliding)
        {
            // ... flip the player.
            Flip();
        }
        // Otherwise if the input is moving the player left and the player is facing right...
        else if (move < 0 && _facingRight && !_isWallSliding)
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
