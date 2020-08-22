using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour {
	
	public KeyCode[] up { get; set; }
	public KeyCode[] down { get; set; }
	public KeyCode cursorLockKey = KeyCode.Escape;

	// How far away is the camera from the user in third person mode? Default 10.0f.
	public float thirdPersonDistance = 10.0f;

	// How fast do we jump? Default 5.0f. TODO: make this a target height
	public float jumpVelocity = 5.0f;

	// How quickly do we move using WASD? Default 3.0f.
	public float movementScale = 3f;

	// How sensitive is the mouse movement? Default 5.0f.
	public float rotationScale = 5f;

	// When does fog begin to creep in below ground? Default 0.0f.
	public float fogMin = 0.0f;

	// When is fog a maximum value? Default -10.0f.
	public float fogMax = -10.0f;

	// Are we flying? If true, we also ignore collision and gravity.
	public bool flyingMode = false;

	// Is the mouse cursor locked to the screen and hidden? (Default parameter)
	public bool cursorLocked = true;

	// A reference to the camera we're controlling.
	private Camera mainCamera;

	// These represent the camera rotation parameters. We sum up the input multiplied
	// by the rotation scale, and clamp it to prevent jittering found using the naiive
	// methods.
	private static float xRot = 0;
	private static float yRot = 0;

	// Keeps track of our global chunk position. This is used to generate and store the
	// terrain around the player, and apply reloading logic as necessary. 
	public static int xChunk = 0;
	public static int yChunk = 0;
	public static int zChunk = 0;

	// What is the initial position of the player? Used to zero out chunk coordinates.
	private Vector3 initialPosition;

	// The default background color used for the camera. "caveColor" is black so that
	// we can cleanly hide the unloaded chunks in the distance underground.
	private static Color skyColor = new Color (49 / 255f, 77 / 255f, 121 / 255f);
	private static Color caveColor = new Color (0, 0, 0);

	void Start () {
		up = new KeyCode[]{ KeyCode.Q, KeyCode.LeftShift, KeyCode.Space };
		down = new KeyCode[]{ KeyCode.E, KeyCode.LeftControl, KeyCode.LeftAlt };

		mainCamera = Camera.main;
		initialPosition = transform.position;

		if (cursorLocked) {
			Cursor.lockState = CursorLockMode.Locked;
		}
		
		if (flyingMode) {
			GetComponent<Rigidbody> ().useGravity = false;
			Destroy (GetComponent<CapsuleCollider> ());
			movementScale = 10f;
		}
	}

	// True if any of the keycodes in the provided array are pressed or held.
	private bool keycodePressed(KeyCode[] arr) {
		for(int i = 0; i < arr.Length; i++) {
			if(Input.GetKey(arr[i])) {
				return true;
			}
		}
		return false;
	}

	// True if any of the keycodes in the provided array held down this frame.
	private bool keycodeDown(KeyCode[] arr) {
		for(int i = 0; i < arr.Length; i++) {
			if(Input.GetKeyDown(arr[i])) {
				return true;
			}
		}
		return false;
	}

	void Update () {
		// The old and new rotations of the player. Calculated in the Mouse Movement section.
		Quaternion oldRotation = mainCamera.transform.rotation;
		Quaternion newRotation = oldRotation;

		// The angle we are facing, calculated facing downwards.
		Quaternion angle = Quaternion.AngleAxis (oldRotation.eulerAngles.y, Vector3.up);
		transform.rotation = angle; // Update the player model to face this angle

		// The new velocity of the player. Calculated in the Vertical and Horizontal movement sections.
		Vector3 newVelocity;

		// The rigidbody of the player. We set this body's velocity at the end.
		Rigidbody body = GetComponent<Rigidbody> ();

		// If we are flying, then ignore gravity and stop moving when no keys are pressed.
		if (flyingMode) {
			body.velocity = Vector3.zero;
		}

		#region Cursor locking
		if (Input.GetKeyDown(cursorLockKey)) {
			cursorLocked = !cursorLocked;
			if (cursorLocked) {
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			} else {
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
		}
		#endregion

		#region Horizontal movement
		// Grab the horizontal and vertical (forward/backward) movement and rotate it based on where we're looking
		newVelocity = angle * new Vector3 (Input.GetAxis ("Horizontal"), 0.0f, Input.GetAxis ("Vertical")) * movementScale;

		// We need to preserve the original Y velocity, or else gravity won't work right.
		newVelocity.y = body.velocity.y;
		#endregion

		#region Vertical movement
		// If we are flying, then up and down work the same as WASD.
		if (flyingMode) {
			if (keycodePressed (up)) {
				newVelocity.y = movementScale;
			}
			if (keycodePressed (down)) {
				newVelocity.y = -movementScale;
			}
		} else {
			// If we're not flying, then we check if we're on the ground before jumping.
			if (keycodeDown (up)) {	
				// We raycast down to determine this status.
				if (Physics.Raycast (transform.position, Vector3.down, 3.0f)) {
					newVelocity.y = jumpVelocity;
				}
			}
		}
		#endregion

		#region Torch throwing
        if (!GameManager.twoDMode && Input.GetKeyDown (KeyCode.Q)) {
			GameObject newTorch = GameObject.Instantiate(Resources.Load ("Prefabs/Torch") as GameObject);
			newTorch.transform.position = this.transform.position + (angle * Vector3.forward * 0.5f);
			newTorch.GetComponent<Rigidbody>().AddForceAtPosition(mainCamera.transform.rotation * Vector3.forward * 100f, (Vector3.up));
		}
		#endregion

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

		if (GameManager.twoDMode) {
			int newXChunk = (int)((transform.position.x - initialPosition.x) / Generator.Size);
			int newYChunk = (int)((transform.position.y - initialPosition.y) / Generator.Size);
			int newZChunk = (int)((transform.position.z -initialPosition.z) / Generator.Size);

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
				Generator.Chunks.shift (movementDir);
				int[] regenerateIndices = CubeBuffer<Chunk>.faceIndices [(int)movementDir];
                //Debug.Log("Regenerating " + regenerateIndices.Length + " indices!");
				for (int i = 0; i < regenerateIndices.Length; i++) {
					Vector3Int pos = Helper.indexToCoords (Generator.RenderDiameter, regenerateIndices [i]);

					int newX = -Generator.RenderRadius + xChunk + pos.x;
					int newY = -Generator.RenderRadius + yChunk + pos.y;
					int newZ = -Generator.RenderRadius + zChunk + pos.z;

					Vector3Int genPos = new Vector3Int (newZ, newY, newX);

					// Try to find the given cave chunk in the cache, first.
					if (Generator.ChunkCache.ContainsKey (genPos)) {
						// If we find it, then "generate" that chunk in the cube buffer, and activate it.
						Chunk cachedChunk = Generator.ChunkCache [genPos];

						Generator.Chunks [regenerateIndices [i]] = cachedChunk;
                        cachedChunk.obj?.SetActive(true);
                    } else {
                        // If we don't find it, then actually regenerate the mesh asyncronously. 
						GameObject shell = Generator.generateEmpty ();
						Chunk chunkShell = new Chunk (genPos, shell);
						Generator.Chunks [regenerateIndices [i]] = chunkShell;
						Generator.ChunkCache [genPos] = chunkShell;
                        // TODO starting a lot of coroutines is expensive. Maybe make another work queue for 
                        // eventually generating the chunks?
						StartCoroutine (Generator.Generate2DAsync (genPos, shell));
                    }
				}
			}
        } else {
			#region Generate Terrain

			// TODO: make the range (-Size, 0) count as chunk -1, rather than its current 0
            int newXChunk = (int)(transform.position.x / Generator.Size);
			int newYChunk = (int)(transform.position.y / Generator.Size);
			int newZChunk = (int)(transform.position.z / Generator.Size);

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
			} 
			else if (newZChunk < zChunk) {
				zChunk = newZChunk;
				movementDir = Direction.BACK;
			} else if (newZChunk > zChunk) {
				zChunk = newZChunk;
				movementDir = Direction.FRONT;
			}

            if (movementDir != Direction.NONE)
            {
                Generator.Chunks.shift(movementDir);
                int[] regenerateIndices = CubeBuffer<Chunk>.faceIndices[(int)movementDir];
                for (int i = 0; i < regenerateIndices.Length; i++)
                {
                    Vector3Int pos = Helper.indexToCoords(Generator.RenderDiameter, regenerateIndices[i]);

                    int newX = -Generator.RenderRadius + xChunk + pos.x;
                    int newY = -Generator.RenderRadius + yChunk + pos.y;
                    int newZ = -Generator.RenderRadius + zChunk + pos.z;

                    Vector3Int genPos = new Vector3Int(newZ, newY, newX);

                    // Try to find the given cave chunk in the cache, first.
                    if (Generator.ChunkCache.ContainsKey(genPos))
                    {
                        // If we find it, then "generate" that chunk in the cube buffer, and activate it.
                        Chunk cachedChunk = Generator.ChunkCache[genPos];

                        Generator.Chunks[regenerateIndices[i]] = cachedChunk;
                        cachedChunk.obj.SetActive(true);
                    }
                    else
                    {
                        // If we don't find it, then actually regenerate the mesh asyncronously. 
                        GameObject shell = Generator.generateEmpty();
                        Chunk chunkShell = new Chunk(genPos, shell);
                        Generator.Chunks[regenerateIndices[i]] = chunkShell;
                        Generator.ChunkCache[genPos] = chunkShell;
                        StartCoroutine(Generator.GenerateCaveAsync(genPos, shell));
                    }
                }

            }

			#endregion

			#region Underground fog
			// Above / underground transition
			if (mainCamera.transform.position.y > fogMin) {
				mainCamera.backgroundColor = skyColor;
				RenderSettings.fog = false;
			} else {
				float fogFactor = mainCamera.transform.position.y / fogMax;

				RenderSettings.fog = true;
                RenderSettings.fogDensity = fogFactor * Generator.FogStrength;
				mainCamera.backgroundColor = Color.Lerp(skyColor, caveColor, fogFactor);
			}
			#endregion
		}

		// Update velocity
		body.velocity = newVelocity;

		// Update camera position (which may differ due to the third person view)
		mainCamera.transform.position = transform.position + (newRotation * new Vector3 (0, 0, -thirdPersonDistance));
		mainCamera.transform.rotation = newRotation;
	}
}
