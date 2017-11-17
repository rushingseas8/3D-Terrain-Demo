using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour {

	public KeyCode[] forward { get; set; } 
	public KeyCode[] backward { get; set; }
	public KeyCode[] left { get; set; }
	public KeyCode[] right { get; set; }
	public KeyCode[] up { get; set; }
	public KeyCode[] down { get; set; }
    public KeyCode slow;
    public KeyCode cameraLock;

	public Camera mainCamera;

	public static float thirdPersonDistance = 0;

	public static bool flyingMode = true;
    public static bool cameraKeyLock = true;

	private static float movementScale = 0.3f;
	private static float rotationScale = 5f;

	// Use this for initialization
	void Start () {
		forward	= new KeyCode[]{ KeyCode.W, KeyCode.UpArrow };
		backward = new KeyCode[]{ KeyCode.S, KeyCode.DownArrow };
		left = new KeyCode[]{ KeyCode.A, KeyCode.LeftArrow };
		right = new KeyCode[]{ KeyCode.D, KeyCode.RightArrow };
		up = new KeyCode[]{ KeyCode.Q, KeyCode.LeftShift, KeyCode.Space };
		down = new KeyCode[]{ KeyCode.E, KeyCode.LeftControl, KeyCode.LeftAlt };

		slow = KeyCode.Z;
		cameraLock = KeyCode.Escape;

		mainCamera = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;

		/*
		if (flyingMode)
			movementScale = 2.5f;
		else
			movementScale = 0.2f;*/
	}

	bool keycodePressed(KeyCode[] arr) {
		for(int i = 0; i < arr.Length; i++) {
			if(Input.GetKey(arr[i])) {
				return true;
			}
		}
		return false;
	}

	bool keycodeDown(KeyCode[] arr) {
		for(int i = 0; i < arr.Length; i++) {
			if(Input.GetKeyDown(arr[i])) {
				return true;
			}
		}
		return false;
	}

	// Update is called once per frame
	void Update () {
		Vector3 oldPosition = this.gameObject.transform.position;
		Quaternion oldRotation = mainCamera.transform.rotation;

		Vector3 newPosition = oldPosition;
		Quaternion newRotation = oldRotation;

		if (Input.GetKeyDown (KeyCode.Escape)) {
			Cursor.lockState = CursorLockMode.None;
		}

		float planeRotation = mainCamera.transform.rotation.eulerAngles.y;
		Quaternion quat = Quaternion.Euler (new Vector3 (0, planeRotation, 0));

		Quaternion ang = Quaternion.identity;

		bool onGround = false;

        float newMovementScale = movementScale;

        if (Input.GetKeyUp(cameraLock))
        {
            cameraKeyLock = !cameraKeyLock;
            Debug.Log("Smashed that mfing escape key dawg.");
        }
        if (Input.GetKey(slow))
        {
            newMovementScale *= 0.5f;
        }
		if (keycodePressed (forward)) {
			newPosition += (ang * quat * Vector3.forward * newMovementScale);
		}
		if (keycodePressed (backward)) {
			newPosition += (ang * quat * Vector3.back * newMovementScale);
		}
		if (keycodePressed (left)) {
			newPosition += (ang * quat * Vector3.left * newMovementScale);
		}
		if (keycodePressed (right)) {
			newPosition += (ang * quat * Vector3.right * newMovementScale);
		}

        if (cameraKeyLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        } else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

		if (flyingMode) {
			if (keycodePressed (up)) {
				newPosition += (Vector3.up * movementScale);
			}
		} else {
			//Only jump if we're on the ground (or very close)
			if (keycodeDown (up)) {	
				if (onGround) {
					this.gameObject.GetComponent<Rigidbody> ().velocity = new Vector3 (0, 5, 0);
				}
			}
		}
			
		if (flyingMode) {
			if (keycodePressed (down)) {
				newPosition += (Vector3.down * movementScale);
			}
		}

		float mouseX = Input.GetAxis ("Mouse X");
		float mouseY = -Input.GetAxis ("Mouse Y");
		if (mouseX != 0 || mouseY != 0) {
			Vector3 rot = oldRotation.eulerAngles;

			float xRot = rot.x;

			float tent = rot.x + (rotationScale * mouseY);

			if (xRot > 270 && tent < 270) {
				xRot = -89;
			} else if (xRot < 90 && tent > 90) {
				xRot = 89;
			} else {
				xRot += rotationScale * mouseY;
			}
            

            float newYRot = rot.y + (rotationScale * mouseX);

            newRotation = Quaternion.Euler (
				new Vector3 (
					xRot,
					newYRot,
					0));
		}

		thirdPersonDistance -= Input.GetAxis ("Mouse ScrollWheel");
		if (thirdPersonDistance < 0)
			thirdPersonDistance = 0;

		this.gameObject.transform.position = newPosition;
		this.gameObject.transform.rotation = ang;

		mainCamera.transform.position = newPosition + (newRotation * new Vector3 (0, 0, -thirdPersonDistance));
		mainCamera.transform.rotation = newRotation;
    }
}
