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

	private static float xRot = 0;
	private static float yRot = 0;

	public float thirdPersonDistance = 10.0f;
	public float jumpVelocity = 5.0f;

	public static bool flyingMode = true;
    public static bool cameraKeyLock = true;

	private float movementScale = 3f;
	private float rotationScale = 5f;

	private static int xChunk = 0;
	private static int yChunk = 0;
	private static int zChunk = 0;

	private static Color skyColor = new Color (49 / 255f, 77 / 255f, 121 / 255f);
	private static Color caveColor = new Color (0, 0, 0);

	// Use this for initialization
	void Start () {
		forward	= new KeyCode[]{ KeyCode.W, KeyCode.UpArrow };
		backward = new KeyCode[]{ KeyCode.S, KeyCode.DownArrow };
		left = new KeyCode[]{ KeyCode.A, KeyCode.LeftArrow };
		right = new KeyCode[]{ KeyCode.D, KeyCode.RightArrow };
		up = new KeyCode[]{ /*KeyCode.Q,*/ KeyCode.LeftShift, KeyCode.Space };
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
			Destroy (GetComponent<CapsuleCollider> ());
			movementScale = 10f;
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
		Quaternion oldRotation = mainCamera.transform.rotation;
		Quaternion newRotation = oldRotation;

		Rigidbody body = GetComponent<Rigidbody> ();

		// If we are flying, then ignore gravity and stop moving when no keys are pressed.
		if (flyingMode) {
			body.velocity = Vector3.zero;
		}

		// The angle we are facing, constrained to ignore the up/down axis.
		Quaternion angle = Quaternion.AngleAxis (mainCamera.transform.rotation.eulerAngles.y, Vector3.up);
		this.gameObject.transform.rotation = angle;
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

		float oldYVelocity = body.velocity.y;
		Vector3 velocity = angle * new Vector3 (Input.GetAxis ("Horizontal"), 0.0f, Input.GetAxis ("Vertical")) * movementScale;
		velocity.y = oldYVelocity;

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
					velocity.y = jumpVelocity;
				}
			}
		}

		if (Input.GetKeyDown (KeyCode.Q)) {
			GameObject newTorch = GameObject.Instantiate(Resources.Load ("Prefabs/Torch") as GameObject);
			newTorch.transform.position = this.transform.position + (angle * Vector3.forward * 0.5f);
			//newTorch.GetComponent<Rigidbody> ().AddForceAtPosition (angle * Vector3.forward * 10f, newTorch.transform.position + Vector3.up * 0.6f);
			newTorch.GetComponent<Rigidbody>().AddForceAtPosition(mainCamera.transform.rotation * Vector3.forward * 100f, (Vector3.up));
		}

		body.velocity = velocity;

		#region Mouse movement
		float mouseX = Input.GetAxis ("Mouse X");
		float mouseY = -Input.GetAxis ("Mouse Y");
		if (mouseX != 0 || mouseY != 0) {
			Vector3 rot = oldRotation.eulerAngles;

			xRot = xRot + (rotationScale * mouseX);
			yRot = Mathf.Clamp(yRot + (rotationScale * mouseY), -90f, 90f);

			newRotation = Quaternion.Euler (new Vector3 (yRot, xRot, 0));
		}
		#endregion
			
		#region Mouse scrolling
		thirdPersonDistance -= Input.GetAxis ("Mouse ScrollWheel");
		if (thirdPersonDistance < 0)
			thirdPersonDistance = 0;
		#endregion


		mainCamera.transform.position = transform.position + (newRotation * new Vector3 (0, 0, -thirdPersonDistance));
		mainCamera.transform.rotation = newRotation;

		// Above / underground transition
		if (mainCamera.transform.position.y > 0) {
			mainCamera.backgroundColor = skyColor;
			RenderSettings.fog = false;
		} else {
			mainCamera.backgroundColor = caveColor;
			RenderSettings.fog = true;
			RenderSettings.fogDensity = (mainCamera.transform.position.y / -10.0f) * 0.1f;
		}

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

		if (zChunk < 0 && movementDir != Direction.NONE) {
			Generator.shiftArray(movementDir);
			int[] regenerateIndices = CubeBuffer.faceIndices[(int)movementDir];
			for (int i = 0; i < regenerateIndices.Length; i++) {
				Vector3Int pos = Helper.indexToCoords(Generator.renderDiameter, regenerateIndices[i]);

				int newX = -Generator.renderRadius + xChunk + pos.x;
				int newY = -Generator.renderRadius + yChunk + pos.y;
				int newZ = -Generator.renderRadius + zChunk + pos.z;

				if (newY >= 0) {
					continue;
				}

				Vector3Int genPos = new Vector3Int(newZ, newY, newX);
				//Vector3Int genPosSwapped = new Vector3Int(newX, newY, newZ);

				// Try to find the given cave chunk in the cache, first.
				if (Generator.chunkCache.ContainsKey(genPos)) {
					// If we find it, then "generate" that chunk in the cube buffer, and activate it.
					GameObject cachedGO = Generator.chunkCache[genPos];

					Generator.chunks[regenerateIndices[i]] = cachedGO;
					cachedGO.SetActive(true);
				} else {
					// If we don't find it, then actually regenerate the mesh asyncronously. 
					GameObject shell = Generator.generateEmpty();
					Generator.chunks[regenerateIndices[i]] = shell;
					Generator.chunkCache[genPos] = shell;
					StartCoroutine(Generator.generateAsync(genPos, shell));
				}
			}
		}

		#endregion
	}
}
