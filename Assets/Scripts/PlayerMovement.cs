using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rb;
    private CapsuleCollider col;
    public CameraController cameraCon;
    private Collider ground;
    
    Vector3 dir = Vector3.zero;
    
    // States
    private bool running;
    private bool jump;
    private bool crouched;
    private bool grounded;
    
    // Cooldown
    private bool canJump = true;
    private bool canDoubleJump = true;
    private float wallBan = 0f;
    private float wrTimer = 0f;
    private float wallStickTimer = 0f;
    
    // Ground
    private float groundSpeed = 5f;
    private float runSpeed = 7.5f;
    private float grAccel = 20f;
    
    // Air
    private float airSpeed = 3f;
    private float airAccel = 20f;
    
    // Jump
    private float jumpUpSpeed = 9.2f;
    private float dashSpeed = 6f;
    
    // Wall
    private float wallFloorBoundary = 40f;
    private float wallSpeed = 7.5f;
    private float wallClimbSpeed = 4f;
    private float wallAccel = 20f;
    private float wallRunTime = 1.5f;
    private float wallStickiness = 20f;
    private float wallStickDistance = 1f;
    private float wallBanTime = 4f;
    
    Vector3 groundNormal = Vector3.up;
    Vector3 bannedGroundNormal;
    
    enum Mode
    {
        Walk,
        Fly,
        Wallrun,
    }

    private Mode mode = Mode.Fly;
    
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        cameraCon = GetComponentInChildren<CameraController>();
    }

    private void OnGUI()
    {
        GUILayout.Label("Planar Speed: " + new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude);
        GUILayout.Label("Vertical Speed: " + rb.velocity.y);
    }

    // Update is called once per frame
    void Update()
    {
        col.material.dynamicFriction = 0f;
        dir = Direction();

        running = (Input.GetKey(KeyCode.LeftShift) && Input.GetAxisRaw("Vertical") > 0.9f);
        crouched = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jump = true;
        }
    }

    private void FixedUpdate()
    {
        if (crouched)
        {
            // Mathf.Max(a, b)   b until a > b
            col.height = Mathf.Max(0.6f, col.height - Time.deltaTime * 10f);
        }
        else
        {
            col.height = Mathf.Min(2f, col.height + Time.deltaTime * 10f);
        }

        if (wallStickTimer == 0f && wallBan > 0f)
        {
            bannedGroundNormal = groundNormal;
        }
        else
        {
            bannedGroundNormal = Vector3.zero;
        }

        // 怠けるものじゃないこれ、時間あれば書き直すわ
        wallStickTimer = Mathf.Max(wallStickTimer - Time.deltaTime, 0f);
        wallBan = Mathf.Max(wallBan - Time.deltaTime, 0f);

        switch (mode)
        {
            case Mode.Wallrun:
                cameraCon.SetTilt(WallrunCameraAngle());
                Wallrun(dir, wallSpeed, wallClimbSpeed, wallAccel);
                if (ground.tag != "InfiniteWallrun") wrTimer = Mathf.Max(wrTimer - Time.deltaTime, 0f);
                break;
            
            case Mode.Walk:
                cameraCon.SetTilt(0);
                Walk(dir, running ? runSpeed : groundSpeed, grAccel);
                break;
            
            case Mode.Fly:
                cameraCon.SetTilt(0);
                AirMove(dir, airSpeed, airAccel);
                break;
        }

        jump = false;
    }

    private Vector3 Direction()
    {
        float hAxis = Input.GetAxisRaw("Horizontal");
        float vAxis = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(hAxis, 0, vAxis);
        // Transforms direction from local space to world space
        return rb.transform.TransformDirection(direction);
    }

    #region - Collision -

    // Called once per frame for every Collider or Rigidbody that touches another Collider or Rigidbody
    private void OnCollisionStay(Collision collisionInfo)
    {
        if (collisionInfo.contactCount > 0)
        {
            float angle;

            foreach (ContactPoint contact in collisionInfo.contacts)
            {
                // Angle between standing point normal and Vector3.up
                angle = Vector3.Angle(contact.normal, Vector3.up);
                if (angle < wallFloorBoundary)
                {
                    EnterWalk();
                    grounded = true;
                    groundNormal = contact.normal;
                    ground = contact.otherCollider;
                    return;
                }
            }

            // Ground checker
            if (VectorToGround().magnitude > 0.2f)
            {
                grounded = false;
            }

            if (grounded == false)
            {
                foreach (ContactPoint contact in collisionInfo.contacts)
                {
                    if (contact.otherCollider.tag != "NoWallrun" && contact.otherCollider.tag != "Player" &&
                        mode != Mode.Walk)
                    {
                        // Angle between standing point normal and Vector3.up
                        angle = Vector3.Angle(contact.normal, Vector3.up);
                        if (angle > wallFloorBoundary && angle < 120f)
                        {
                            grounded = true;
                            groundNormal = contact.normal;
                            ground = contact.otherCollider;
                            EnterWallrun();
                            return;
                        }
                    }
                }
            }
        }
    }

    private void OnCollisionExit(Collision collisionInfo)
    {
        if (collisionInfo.contactCount == 0)
        {
            EnterFly();
        }
    }
    
    #endregion

    #region - Enter States -

    void EnterWalk()
    {
        if (mode != Mode.Walk && canJump)
        {
            if (mode == Mode.Fly && crouched)
            {
                rb.AddForce(-rb.velocity.normalized, ForceMode.VelocityChange);
            }

            if (rb.velocity.y < -1.2f)
            {
                cameraCon.Punch(new Vector2(0, -3f));
            }
            //StartCoroutine(bHopCoroutine(bhopLeniency));
            // Calls the method named methodName on every MonoBehaviour in this game object
            //gameObject.SendMessage("onWalk");
            mode = Mode.Walk;
        }
    }

    void EnterFly(bool wishFly = false) // Introduced bool wishFly will always be false when this function is called
    {
        grounded = false;
        if (mode == Mode.Wallrun && VectorToWall().magnitude < wallStickDistance && !wishFly)
        {
            return;
        }
        else if (mode != Mode.Fly)
        {
            wallBan = wallBanTime;
            canDoubleJump = true;
            mode = Mode.Fly;
        }
    }

    void EnterWallrun()
    {
        if (mode != Mode.Wallrun)
        {
            if (VectorToGround().magnitude > 0.2f && CanRunOnThisWall(bannedGroundNormal) && wallStickTimer == 0f)
            {
                //gameObject.SendMessage("OnStartWallrunning");
                wrTimer = wallRunTime;
                canDoubleJump = true;
                mode = Mode.Wallrun;
            }
            else
            {
                EnterFly(true);
            }
        }
    }
    
    #endregion
    
    #region - Movement Types -
    
    void Walk(Vector3 wishDir, float maxSpeed, float acceleration)
    {
        if (jump && canJump)
        {
            //gameObject.SendMessage("OnJump");
            Jump();
        }
        else
        {
            wishDir = wishDir.normalized;
            Vector3 planarSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (planarSpeed.magnitude > maxSpeed) acceleration *= planarSpeed.magnitude / maxSpeed;
            Vector3 direction = wishDir * maxSpeed - planarSpeed;

            if (direction.magnitude < 0.5f)
            {
                // Acceleration added faster when dir.magnitude close to 0.5f
                acceleration *= direction.magnitude / 0.5f;
            }

            direction = direction.normalized * acceleration;
            float magnitude = direction.magnitude;
            direction = direction.normalized;
            direction *= magnitude;

            Vector3 slopeCorrection = groundNormal * Physics.gravity.y / groundNormal.y;
            slopeCorrection.y = 0f;

            direction += slopeCorrection;
            
            // Add a continuous acceleration to the rigidbody, ignoring its mass
            // https://docs.unity3d.com/ScriptReference/ForceMode.html
            rb.AddForce(direction, ForceMode.Acceleration);
        }
    }

    void AirMove(Vector3 wishDir, float maxSpeed, float acceleration)
    {
        if (jump && !crouched)
        {
            //gameObject.SendMessage("OnDoubleJump");
            DoubleJump(wishDir);
        }

        if (crouched && rb.velocity.y > -10f && Input.GetKey(KeyCode.Space))
        {
            rb.AddForce(Vector3.down * 20f, ForceMode.Acceleration);
        }

        // Vector projection of current planar velocity onto accelDir
        float projVel = Vector3.Dot(new Vector3(rb.velocity.x, 0f, rb.velocity.z), wishDir);
        // Accelerated velocity in direction of movement
        float accelVel = acceleration * Time.deltaTime;
        
        // If necessary, cut the accelerated velocity so the vector projection does not exceed max_velocity
        if (projVel + accelVel > maxSpeed)
        {
            accelVel = Mathf.Max(0f, maxSpeed - projVel);
        }
        
        // Add an instant velocity change to the rigidbody, ignoring its mass
        rb.AddForce(wishDir.normalized * accelVel, ForceMode.VelocityChange);
    }

    void Wallrun(Vector3 wishDir, float maxSpeed, float climbSpeed, float acceleration)
    {
        if (jump)
        {
            // Vertical
            float upForce = Mathf.Clamp(jumpUpSpeed - rb.velocity.y, 0f, Mathf.Infinity);
            rb.AddForce(new Vector3(0, upForce, 0), ForceMode.VelocityChange);
            
            // Horizontal
            Vector3 jumpOffWall = groundNormal.normalized;
            jumpOffWall *= dashSpeed;
            jumpOffWall.y = 0f;
            rb.AddForce(jumpOffWall, ForceMode.VelocityChange);
            wrTimer = 0f;
            EnterFly(true);
        }
        else if (wrTimer == 0f || crouched) // Exit from wallrunning
        {
            rb.AddForce(groundNormal * 3f, ForceMode.VelocityChange);
            EnterFly(true);
        }
        else
        {
            // Horizontal
            Vector3 distance = VectorToWall();
            wishDir = RotateToPlane(wishDir, -distance.normalized);
            wishDir *= maxSpeed;
            wishDir.y = Mathf.Clamp(wishDir.y, -climbSpeed, climbSpeed);
            Vector3 wallrunForce = wishDir - rb.velocity;
            if (wallrunForce.magnitude > 0.2f)
            {
                wallrunForce = wallrunForce.normalized * acceleration;
                
            }
            
            // Vertical
            if (rb.velocity.y < 0f && wishDir.y > 0f)
            {
                wallrunForce.y = 2f * acceleration;
            }
            
            //Anti-gravity force
            Vector3 antiGravityForce = -Physics.gravity;
            if (wrTimer < 0.33 * wallRunTime)
            {
                antiGravityForce *= wrTimer / wallRunTime;
                wallrunForce += (Physics.gravity + antiGravityForce);
            }
            
            //Forces
            rb.AddForce(wallrunForce, ForceMode.Acceleration);
            rb.AddForce(antiGravityForce, ForceMode.Acceleration);
            if (distance.magnitude > wallStickDistance) distance = Vector3.zero;
            rb.AddForce(distance * wallStickiness, ForceMode.Acceleration);
        }

        if (!grounded)
        {
            wallStickTimer = 0.2f;
            EnterFly();
        }
    }

    void Jump()
    {
        if (mode == Mode.Walk && canJump)
        {
            float upForce = Mathf.Clamp(jumpUpSpeed - rb.velocity.y, 0, Mathf.Infinity);
            rb.AddForce(new Vector3(0, upForce, 0), ForceMode.VelocityChange);
            StartCoroutine(jumpCooldownCoroutine(0.2f));
            EnterFly(true);
        }
    }

    void DoubleJump(Vector3 wishDir)
    {
        if (canDoubleJump)
        {
            //Vertical
            float upForce = Mathf.Clamp(jumpUpSpeed - rb.velocity.y, 0, Mathf.Infinity);

            rb.AddForce(new Vector3(0, upForce, 0), ForceMode.VelocityChange);

            //Horizontal
            if (wishDir != Vector3.zero)
            {
                Vector3 horSpid = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                Vector3 newSpid = wishDir.normalized;
                float newSpidMagnitude = dashSpeed;

                if (horSpid.magnitude > dashSpeed)
                {
                    float dot = Vector3.Dot(wishDir.normalized, horSpid.normalized);
                    if (dot > 0)
                    {
                        newSpidMagnitude = dashSpeed + (horSpid.magnitude - dashSpeed) * dot;
                    }
                    else
                    {
                        newSpidMagnitude = Mathf.Clamp(dashSpeed * (1 + dot),
                            dashSpeed * (dashSpeed / horSpid.magnitude), dashSpeed);
                    }
                }

                newSpid *= newSpidMagnitude;

                rb.AddForce(newSpid - horSpid, ForceMode.VelocityChange);
            }

            canDoubleJump = false;
        }

    }

    #endregion

    #region - Mathematical calculations -
    
    Vector2 ClampedAdditionVector(Vector2 a, Vector2 b)
    {
        float k, x, y;
        k = // 原点到 a 的距离
            Mathf.Sqrt(Mathf.Pow(a.x, 2f) + Mathf.Pow(a.y, 2f)) /
            // 原点到 a + b 的距离
            Mathf.Sqrt(Mathf.Pow(a.x + b.x, 2f) + Mathf.Pow(a.y + b.y, 2f));
        // a - b = b 指向 a, 这里是指向 a + b 的运算
        x = k * (a.x + b.x) - a.x;
        y = k * (a.y + b.y) - a.y;
        return new Vector2(x, y);
    }

    Vector3 RotateToPlane(Vector3 vect, Vector3 normal)
    {
        Vector3 rotDir = Vector3.ProjectOnPlane(normal, Vector3.up);
        Quaternion rotation = Quaternion.AngleAxis(-90f, Vector3.up);
        rotDir = rotation * rotDir;
        float angle = -Vector3.Angle(Vector3.up, normal);
        rotation = Quaternion.AngleAxis(angle, rotDir);
        vect = rotation * vect;
        return vect;
    }

    float WallrunCameraAngle()
    {
        Vector3 rotDir = Vector3.ProjectOnPlane(groundNormal, Vector3.up);
        Quaternion rotation = Quaternion.AngleAxis(-90f, Vector3.up);
        rotDir = rotation * rotDir;
        float angle = Vector3.SignedAngle(Vector3.up, groundNormal, Quaternion.AngleAxis(90f, rotDir) * groundNormal);
        angle -= 90;
        angle /= 180;
        Vector3 playerDir = transform.forward;
        Vector3 normal = new Vector3(groundNormal.x, 0, groundNormal.z);

        return Vector3.Cross(playerDir, normal).y * angle;
    }

    bool CanRunOnThisWall(Vector3 normal)
    {
        if (Vector3.Angle(normal, groundNormal) > 10 || wallBan == 0f)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    Vector3 VectorToWall()
    {
        Vector3 direction;
        Vector3 position = transform.position + Vector3.up * col.height / 2f;
        RaycastHit hit;
        if (Physics.Raycast(position, -groundNormal, out hit, wallStickDistance) &&
            Vector3.Angle(groundNormal, hit.normal) < 70)
        {
            groundNormal = hit.normal;
            direction = hit.point - position;
            return direction;
        }
        else
        {
            return Vector3.positiveInfinity;
        }
    }

    Vector3 VectorToGround()
    {
        Vector3 position = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(position, Vector3.down, out hit, wallStickDistance))
        {
            return hit.point - position;
        }
        else
        {
            return Vector3.positiveInfinity;
        }
    }
    
    #endregion

    #region - Coroutines -

    IEnumerator jumpCooldownCoroutine(float time)
    {
        canJump = false;
        yield return new WaitForSeconds(time);
        canJump = true;
    }

    #endregion
}
