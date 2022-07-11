using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Camera worldCamera;
    public Camera viewModelCamera;
    public Rigidbody rb;
    
    public float sensX = 1f;
    public float sensY = 1f;
    public float curTilt = 0;
    public float wishTilt = 0;
    public float wallRunTilt = 60f;

    public float fov;
    public float baseFov = 86.0879f;
    public float maxFov = 123.2862f;
    
    private Vector2 currentLook;
    private Vector2 sway = Vector3.zero;
    
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        curTilt = transform.localEulerAngles.z;
        
        // Lock and hide cursor
        /*Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;*/
    }

    // Update is called once per frame
    void Update()
    {
        RotateWorldCamera();
    }

    void FixedUpdate()
    {
        float addedFov = rb.velocity.magnitude - 3.44f;
        fov = Mathf.Lerp(fov, baseFov + addedFov, 0.5f);
        fov = Mathf.Clamp(fov, baseFov, maxFov);
        //worldCamera.fieldOfView = fov;
        //viewModelCamera.fieldOfView = fov;

        currentLook = Vector2.Lerp(currentLook, currentLook + sway, 0.8f);
        curTilt = Mathf.LerpAngle(curTilt, wishTilt * wallRunTilt, 0.05f);

        sway = Vector2.Lerp(sway, Vector2.zero, 0.2f);
    }

    void RotateWorldCamera()
    {
        Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        mouseInput.x *= sensX;
        mouseInput.y *= sensY;

        currentLook.x += mouseInput.x;
        currentLook.y = Mathf.Clamp(currentLook.y += mouseInput.y, -90, 90);
        
        // World camera pitch axis rotation
        transform.localRotation = Quaternion.AngleAxis(-currentLook.y, Vector3.right);
        // Break world camera local euler angles and add roll axis rotation
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, curTilt);
        // World camera root yaw axis rotation
        transform.root.transform.localRotation = Quaternion.Euler(0, currentLook.x, 0);
    }
    
    #region - Setters -
    public void SetTilt(float newVal)
    {
        wishTilt = newVal;
    }

    public void SetXSens(float newVal)
    {
        sensX = newVal;
    }

    public void SetYSens(float newVal)
    {
        sensY = newVal;
    }

    public void Punch(Vector2 dir)
    {
        sway += dir;
    }

    #endregion
}
