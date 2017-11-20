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

	public float thirdPersonDistance = 10.0f;
	public float jumpVelocity = 5.0f;

	public static bool flyingMode = true;
    public static bool cameraKeyLock = true;

	private float movementScale = 6f;
	private float rotationScale = 5f;

	private static int xChunk = 0;
	private static int yChunk = 0;
	private static int zChunk = 0;

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
		
		if (flyingMode) {
			GetComponent<Rigidbody> ().useGravity = false;
		}
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
		//Vector3 oldPosition = this.gameObject.transform.position;
		Quaternion oldRotation = mainCamera.transform.rotation;

		//Vector3 newPosition = oldPosition;
		Quaternion newRotation = oldRotation;

		Rigidbody body = GetComponent<Rigidbody> ();

		if (flyingMode) {
			body.velocity = Vector3.zero;
		}

		Quaternion angle = Quaternion.AngleAxis (mainCamera.transform.rotation.eulerAngles.y, Vector3.up);
		float newMovementScale = movementScale;
		bool onGround = false;

		if (Physics.Raycast (transform.position, Vector3.down, 3.0f)) {
			onGround = true;
		}

		if (Input.GetKeyDown (KeyCode.Escape)) {
			Cursor.lockState = CursorLockMode.None;
		}

		if (Input.GetKeyUp(cameraLock)) {
            cameraKeyLock = !cameraKeyLock;
        }
        if (Input.GetKey(slow)) {
            newMovementScale *= 0.5f;
        }

		Vector3 velocity = angle * new Vector3 (Input.GetAxis ("Horizontal"), 0.0f, Input.GetAxis ("Vertical")) * movementScale;

        if (cameraKeyLock) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        } else {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

		if (flyingMode) {
			if (keycodePressed (up)) {
				//newPosition += (Vector3.up * movementScale);
				velocity.y = movementScale;
			}
			if (keycodePressed (down)) {
				//newPosition += (Vector3.down * movementScale);
				velocity.y = -movementScale;
			}
		} else {
			//Only jump if we're on the ground (or very close)
			if (keycodeDown (up)) {	
				if (onGround) {
					//this.gameObject.GetComponent<Rigidbody> ().velocity = new Vector3 (0, jumpVelocity, 0);
					//Vector3 oldVelocity = GetComponent<Rigidbody> ().velocity;
					//GetComponent<Rigidbody> ().velocity = new Vector3 (oldVelocity.x, jumpVelocity, oldVelocity.z);
					velocity.y = jumpVelocity;
				}
			}
		}

		body.velocity = velocity;

		float mouseX = Input.GetAxis ("Mouse X");
		float mouseY = -Input.GetAxis ("Mouse Y");
		if (mouseX != 0 || mouseY != 0) {
			Vector3 rot = oldRotation.eulerAngles;

			/*
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
            */

			/*
            newRotation = Quaternion.Euler (
				new Vector3 (
					xRot,
					newYRot,
					0));
					*/

			newRotation = Quaternion.Euler (new Vector3 (
				rot.x + (rotationScale * mouseY),
				rot.y + (rotationScale * mouseX),
				0));
		}
			
		//Debug.Log (thirdPersonDistance);
		thirdPersonDistance -= Input.GetAxis ("Mouse ScrollWheel");
		if (thirdPersonDistance < 0)
			thirdPersonDistance = 0;

		//this.gameObject.transform.position = newPosition;
		this.gameObject.transform.rotation = angle;

		mainCamera.transform.position = transform.position + (newRotation * new Vector3 (0, 0, -thirdPersonDistance));
		mainCamera.transform.rotation = newRotation;

		#region Generate Terrain

		//int newXChunk = (int)(transform.position.z / 1);
		//int newYChunk = (int)(transform.position.y / 1);
		//int newZChunk = (int)(transform.position.x / 1);

		int newXChunk = (int)(transform.position.x / Generator.size);
		int newYChunk = (int)(transform.position.y / Generator.size);
		int newZChunk = (int)(transform.position.z / Generator.size);

		Direction movementDir = Direction.NONE;
	
		if (newXChunk < xChunk) {		
			xChunk = newXChunk;
			movementDir = Direction.LEFT;
		} else if (newXChunk > xChunk) {
			xChunk = newXChunk;
			movementDir = Direction.RIGHT;
		} else if (newYChunk < yChunk) {
			yChunk = newYChunk;
			movementDir = Direction.DOWN;
		} else if (newYChunk > yChunk) {
			yChunk = newYChunk;
			movementDir = Direction.UP;
		} else if (newZChunk < zChunk) {
			zChunk = newZChunk;
			movementDir = Direction.BACK;
		} else if (newZChunk > zChunk) {
			zChunk = newZChunk;
			movementDir = Direction.FRONT;
		}

		if (movementDir != Direction.NONE) {
			Generator.shiftArray(movementDir);
			int[] regenerateIndices = CubeBuffer<GameObject>.faceIndices[(int)movementDir];
			for (int i = 0; i < regenerateIndices.Length; i++) {
				Vector3Int pos = Generator.indexToCoords(Generator.renderDiameter, regenerateIndices[i]);

				int newX = -Generator.renderRadius + xChunk + pos.x;
				int newY = -Generator.renderRadius + yChunk + pos.y;
				int newZ = -Generator.renderRadius + zChunk + pos.z;

				GameObject shell = Generator.generateEmpty();
				Generator.chunks[regenerateIndices[i]] = shell;
				StartCoroutine(Generator.generateAsync(new Vector3(newZ, newY, newX), shell));
			}
		}

		#endregion
	}
}
