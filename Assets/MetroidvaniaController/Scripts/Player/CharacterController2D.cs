using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;
using System;

public class CharacterController2D : MonoBehaviour
{
	[SerializeField] private float m_JumpForce = 400f;							// Amount of force added when the player jumps.
	[Range(0, .3f)] [SerializeField] private float m_MovementSmoothing = .05f;	// How much to smooth out the movement
	[SerializeField] private bool m_AirControl = false;							// Whether or not a player can steer while jumping;
	[SerializeField] private LayerMask m_WhatIsGround;							// A mask determining what is ground to the character
	[SerializeField] private Transform m_GroundCheck;							// A position marking where to check if the player is grounded.
	[SerializeField] private Transform m_WallCheck;								//Posicion que controla si el personaje toca una pared

	const float k_GroundedRadius = .2f; // Radius of the overlap circle to determine if grounded
	private bool m_Grounded;            // Whether or not the player is grounded.
	private Rigidbody2D rigidbody2DRef;
	private bool m_FacingRight = true;  // For determining which way the player is currently facing.
	private Vector3 velocity = Vector3.zero;
	private float limitFallSpeed = 25f; // Limit fall speed

	public bool canDoubleJump = true; //If player can double jump
	[SerializeField] private float m_DashForce = 25f;
	private bool canDash = true;
	private bool isDashing = false; //If player is dashing
	private bool isWall = false; //If there is a wall in front of the player
	private bool isWallSliding = false; //If player is sliding in a wall
	private bool oldWallSlidding = false; //If player is sliding in a wall in the previous frame
	private float prevVelocityX = 0f;
	private bool canCheck = false; //For check if player is wallsliding

	public float life = 10f; //Life of the player
	public bool invincible = false; //If player can die
	private bool canMove = true; //If player can move

	private Animator animator;
	public ParticleSystem particleJumpUp; //Trail particles
	public ParticleSystem particleJumpDown; //Explosion particles

	private float jumpWallStartX = 0;
	private float jumpWallDistX = 0; //Distance between player and wall
	private bool limitVelOnWallJump = false; //For limit wall jump distance with low fps

	[Header("Grapple Controls")]
	[Space]

	// The minimum Y distance a grapple point must be above for you to attach to it 
	[SerializeField] private float minHeightToGrapple = 5.0f;
	// The minimum Y distance a grapple point can be above for you to be able to attach to it 
	[SerializeField] private float maxHeightToGrapple = 15.0f;
	// The maximum X distance an anchor point can be from you to grapple
	[SerializeField] private float maxXToGrapple = 5.0f;
	private SpringJoint2D m_grappleSpringJoint = null;
	private bool m_isGrappling = false;
	private GrappleRope m_grappleRope = null;
	private Rigidbody2D[] allAnchorPoints;
	private Rigidbody2D m_currentGrappleAnchor;

	[Header("Events")]
	[Space]

	public UnityEvent OnFallEvent;
	public UnityEvent OnLandEvent;


	[System.Serializable]
	public class BoolEvent : UnityEvent<bool> { }

	private void Awake()
	{
		rigidbody2DRef = GetComponent<Rigidbody2D>();
		animator = GetComponent<Animator>();
		m_grappleSpringJoint = GetComponent<SpringJoint2D>();
		m_grappleRope = GetComponentInChildren<GrappleRope>();
		
		var anchorPointObjs = GameObject.FindGameObjectsWithTag("Grappleable");
		allAnchorPoints = new Rigidbody2D[anchorPointObjs.Length];
		for (int i = 0; i < anchorPointObjs.Length; i++)
        {
			allAnchorPoints[i] = anchorPointObjs[i].GetComponent<Rigidbody2D>();
        }
		if (OnFallEvent == null)
			OnFallEvent = new UnityEvent();

		if (OnLandEvent == null)
			OnLandEvent = new UnityEvent();
	}


	private void FixedUpdate()
	{
		bool wasGrounded = m_Grounded;
		m_Grounded = false;

		// The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
		// This can be done using layers instead but Sample Assets will not overwrite your project settings.
		Collider2D[] colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);
		for (int i = 0; i < colliders.Length; i++)
		{
			if (colliders[i].gameObject != gameObject)
				m_Grounded = true;
				if (!wasGrounded )
				{
					OnLandEvent.Invoke();
					if (!isWall && !isDashing) 
						particleJumpDown.Play();
					canDoubleJump = true;
					if (rigidbody2DRef.velocity.y < 0f)
						limitVelOnWallJump = false;
				}
		}

		isWall = false;

		if (!m_Grounded)
		{
			OnFallEvent.Invoke();
			Collider2D[] collidersWall = Physics2D.OverlapCircleAll(m_WallCheck.position, k_GroundedRadius, m_WhatIsGround);
			for (int i = 0; i < collidersWall.Length; i++)
			{
				if (collidersWall[i].gameObject != null)
				{
					isDashing = false;
					isWall = true;
				}
			}
			prevVelocityX = rigidbody2DRef.velocity.x;
		}

		if (limitVelOnWallJump)
		{
			if (rigidbody2DRef.velocity.y < -0.5f)
				limitVelOnWallJump = false;
			jumpWallDistX = (jumpWallStartX - transform.position.x) * transform.localScale.x;
			if (jumpWallDistX < -0.5f && jumpWallDistX > -1f) 
			{
				canMove = true;
			}
			else if (jumpWallDistX < -1f && jumpWallDistX >= -2f) 
			{
				canMove = true;
				rigidbody2DRef.velocity = new Vector2(10f * transform.localScale.x, rigidbody2DRef.velocity.y);
			}
			else if (jumpWallDistX < -2f) 
			{
				limitVelOnWallJump = false;
				rigidbody2DRef.velocity = new Vector2(0, rigidbody2DRef.velocity.y);
			}
			else if (jumpWallDistX > 0) 
			{
				limitVelOnWallJump = false;
				rigidbody2DRef.velocity = new Vector2(0, rigidbody2DRef.velocity.y);
			}
		}
	}


	public void Move(float move, bool jump, bool dash, bool launchGrapple, bool releaseGrapple)
	{
		if (canMove) {
			if (launchGrapple)
            {
				TryToLaunchGrapple();
            }
			if (releaseGrapple)
            {
				TryToReleaseGrapple();
            }
			if (m_isGrappling)
            {
				return;
            }
			if (dash && canDash && !isWallSliding)
			{
				//m_Rigidbody2D.AddForce(new Vector2(transform.localScale.x * m_DashForce, 0f));
				StartCoroutine(DashCooldown());
			}
			// If crouching, check to see if the character can stand up
			if (isDashing)
			{
				rigidbody2DRef.velocity = new Vector2(transform.localScale.x * m_DashForce, 0);
			}
			//only control the player if grounded or airControl is turned on
			else if (m_Grounded || m_AirControl)
			{
				if (rigidbody2DRef.velocity.y < -limitFallSpeed)
					rigidbody2DRef.velocity = new Vector2(rigidbody2DRef.velocity.x, -limitFallSpeed);
				// Move the character by finding the target velocity
				Vector3 targetVelocity = new Vector2(move * 10f, rigidbody2DRef.velocity.y);
				// And then smoothing it out and applying it to the character
				rigidbody2DRef.velocity = Vector3.SmoothDamp(rigidbody2DRef.velocity, targetVelocity, ref velocity, m_MovementSmoothing);

				// If the input is moving the player right and the player is facing left...
				if (move > 0 && !m_FacingRight && !isWallSliding)
				{
					// ... flip the player.
					Flip();
				}
				// Otherwise if the input is moving the player left and the player is facing right...
				else if (move < 0 && m_FacingRight && !isWallSliding)
				{
					// ... flip the player.
					Flip();
				}
			}
			// If the player should jump...
			if (m_Grounded && jump)
			{
				// Add a vertical force to the player.
				animator.SetBool("IsJumping", true);
				animator.SetBool("JumpUp", true);
				m_Grounded = false;
				rigidbody2DRef.AddForce(new Vector2(0f, m_JumpForce));
				canDoubleJump = true;
				particleJumpDown.Play();
				particleJumpUp.Play();
			}
			else if (!m_Grounded && jump && canDoubleJump && !isWallSliding)
			{
				canDoubleJump = false;
				rigidbody2DRef.velocity = new Vector2(rigidbody2DRef.velocity.x, 0);
				rigidbody2DRef.AddForce(new Vector2(0f, m_JumpForce / 1.2f));
				animator.SetBool("IsDoubleJumping", true);
			}

			else if (isWall && !m_Grounded)
			{
				if (!oldWallSlidding && rigidbody2DRef.velocity.y < 0 || isDashing)
				{
					isWallSliding = true;
					m_WallCheck.localPosition = new Vector3(-m_WallCheck.localPosition.x, m_WallCheck.localPosition.y, 0);
					Flip();
					StartCoroutine(WaitToCheck(0.1f));
					canDoubleJump = true;
					animator.SetBool("IsWallSliding", true);
				}
				isDashing = false;

				if (isWallSliding)
				{
					if (move * transform.localScale.x > 0.1f)
					{
						StartCoroutine(WaitToEndSliding());
					}
					else 
					{
						oldWallSlidding = true;
						rigidbody2DRef.velocity = new Vector2(-transform.localScale.x * 2, -5);
					}
				}

				if (jump && isWallSliding)
				{
					animator.SetBool("IsJumping", true);
					animator.SetBool("JumpUp", true); 
					rigidbody2DRef.velocity = new Vector2(0f, 0f);
					rigidbody2DRef.AddForce(new Vector2(transform.localScale.x * m_JumpForce *1.2f, m_JumpForce));
					jumpWallStartX = transform.position.x;
					limitVelOnWallJump = true;
					canDoubleJump = true;
					isWallSliding = false;
					animator.SetBool("IsWallSliding", false);
					oldWallSlidding = false;
					m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
					canMove = false;
				}
				else if (dash && canDash)
				{
					isWallSliding = false;
					animator.SetBool("IsWallSliding", false);
					oldWallSlidding = false;
					m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
					canDoubleJump = true;
					StartCoroutine(DashCooldown());
				}
			}
			else if (isWallSliding && !isWall && canCheck) 
			{
				isWallSliding = false;
				animator.SetBool("IsWallSliding", false);
				oldWallSlidding = false;
				m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
				canDoubleJump = true;
			}
		}
	}

    private void TryToLaunchGrapple()
    {
		if (m_isGrappling || m_Grounded) { return; }

		// loop through anchor points and see if any satisfy the distance constraints. If any are
		// found, attach the grapple to the closest one.
		float closest = Mathf.Infinity;
		float yDiff, distance;
		Vector2 currAnchorPt;
		Rigidbody2D closestAnchorPt = null;
		bool anchorPointWasFound = false;
		for (int i = 0; i < allAnchorPoints.Length; i++)
        {
			currAnchorPt = allAnchorPoints[i].transform.position;
			// check x distance
			if (Mathf.Abs(currAnchorPt.x - transform.position.x) <= maxXToGrapple)
            {
				// check y distance
				yDiff = Mathf.Abs(currAnchorPt.y - transform.position.y);
				if (yDiff >= minHeightToGrapple && yDiff <= maxHeightToGrapple)
                {
					// check total distance
					distance = Vector2.Distance(transform.position, currAnchorPt);
					if (distance < closest)
                    {
						closest = distance;
						closestAnchorPt = allAnchorPoints[i];
						anchorPointWasFound = true;
                    }
                }
            }

        }
		if (anchorPointWasFound)
		{
			m_currentGrappleAnchor = closestAnchorPt;
			m_isGrappling = true;

			// Initialize the grapple joint, setting the closest anchor point as its connected body
			m_grappleSpringJoint.enabled = true;
			m_grappleSpringJoint.connectedBody = m_currentGrappleAnchor;
			m_grappleSpringJoint.distance = closest;
			
			// Draw the grapple rope
			m_grappleRope.StartDrawingRope(m_grappleSpringJoint.connectedBody.transform.position);
		}
	}

	private void TryToReleaseGrapple()
    {
		if (!m_isGrappling) { return; }

		m_currentGrappleAnchor = null;
		m_isGrappling = false;

		m_grappleSpringJoint.connectedBody = null;
		m_grappleSpringJoint.enabled = false;
		
		canDoubleJump = true;

		m_grappleRope.HideRope();
	}

    private void Flip()
	{
		// Switch the way the player is labelled as facing.
		m_FacingRight = !m_FacingRight;

		// Multiply the player's x local scale by -1.
		Vector3 theScale = transform.localScale;
		theScale.x *= -1;
		transform.localScale = theScale;
	}

	public void ApplyDamage(float damage, Vector3 position) 
	{
		if (!invincible)
		{
			animator.SetBool("Hit", true);
			life -= damage;
			Vector2 damageDir = Vector3.Normalize(transform.position - position) * 40f ;
			rigidbody2DRef.velocity = Vector2.zero;
			rigidbody2DRef.AddForce(damageDir * 10);
			if (life <= 0)
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
		animator.SetBool("IsDashing", true);
		isDashing = true;
		canDash = false;
		yield return new WaitForSeconds(0.1f);
		isDashing = false;
		yield return new WaitForSeconds(0.5f);
		canDash = true;
	}

	IEnumerator Stun(float time) 
	{
		canMove = false;
		yield return new WaitForSeconds(time);
		canMove = true;
	}
	IEnumerator MakeInvincible(float time) 
	{
		invincible = true;
		yield return new WaitForSeconds(time);
		invincible = false;
	}
	IEnumerator WaitToMove(float time)
	{
		canMove = false;
		yield return new WaitForSeconds(time);
		canMove = true;
	}

	IEnumerator WaitToCheck(float time)
	{
		canCheck = false;
		yield return new WaitForSeconds(time);
		canCheck = true;
	}

	IEnumerator WaitToEndSliding()
	{
		yield return new WaitForSeconds(0.1f);
		canDoubleJump = true;
		isWallSliding = false;
		animator.SetBool("IsWallSliding", false);
		oldWallSlidding = false;
		m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
	}

	IEnumerator WaitToDead()
	{
		animator.SetBool("IsDead", true);
		canMove = false;
		invincible = true;
		GetComponent<Attack>().enabled = false;
		yield return new WaitForSeconds(0.4f);
		rigidbody2DRef.velocity = new Vector2(0, rigidbody2DRef.velocity.y);
		yield return new WaitForSeconds(1.1f);
		SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
	}
}
