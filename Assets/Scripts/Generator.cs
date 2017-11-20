using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

using LibNoise.Generator;
using MarchingCubesProject;

public class Generator : MonoBehaviour {

	private static RidgedMultifractal noiseGen;

	public static int size = 8;
	public static float scale = 16f;
	//public static float[] data;

	private static Vector3 meshOffset;

	public static float resolution = 2.0f;

	private static Material defaultMaterial;
	private static PhysicMaterial defaultPhysics;

	public static GameObject[,,] chunks;
	public static int renderDiameter = 3;
	public static int renderRadius = renderDiameter / 2;

	static Generator() {
		noiseGen = new RidgedMultifractal ();

		meshOffset = new Vector3 (size / 2, size / 2, size / 2);

		defaultPhysics = new PhysicMaterial ();
		defaultPhysics.bounciness = 0.0f;
		defaultPhysics.dynamicFriction = 1.0f;
		defaultPhysics.staticFriction = 1.0f;

		chunks = new GameObject[renderDiameter, renderDiameter, renderDiameter];
		for (int i = 0; i < renderDiameter * renderDiameter * renderDiameter; i++) {
			Vector3 pos = indexToCoords (renderDiameter, i);
			chunks [(int)pos.x, (int)pos.y, (int)pos.z] = null;
		}
	}

	public static int coordsToIndex(int size, int x, int y, int z) {
		return (x * size * size) + (y * size) + z;
	}

	public static Vector3 indexToCoords(int size, int i) {
		return new Vector3 ((i / size / size) % size, (i / size) % size, i % size);
	}

	public static void generate() {
		noiseGen = new RidgedMultifractal ();
		//noiseGen.OctaveCount = 10;

		//int count = 3;

		for (int i = -renderRadius; i <= renderRadius; i++) {
			for (int j = -renderRadius; j <= renderRadius; j++) {
				for (int k = -renderRadius; k <= renderRadius; k++) {
					chunks[i + renderRadius, j + renderRadius, k + renderRadius] = generateObj (new Vector3 (k, j, i));
				}
			}
		}

		/*
		// Testing with just cubes
		float offsetScale = size / scale;
		for (int i = -renderRadius; i <= renderRadius; i++) {
			for (int j = -renderRadius; j <= renderRadius; j++) {
				for (int k = -renderRadius; k <= renderRadius; k++) {
					GameObject newCube = GameObject.CreatePrimitive (PrimitiveType.Cube);
					newCube.transform.position = new Vector3 (i, j, k);
					newCube.name = "(" + i + ", " + j + ", " + k + ")";
					chunks [i + renderRadius, j + renderRadius, k + renderRadius] = newCube;
				}
			}
		}
		*/
	}

	private static float[] generateData(int size, Vector3 position) {
		// We generate an extra vertex on each end to allow for seamless transitions.
		int sp1 = size + 1;
		float[] data = new float[sp1 * sp1 * sp1];

		// This scale value transforms "position" (in integer chunk coords) to actual
		// world coords, using "size" (# points per mesh per axis) over "scale" (perlin offset).
		// When size == scale, offsetScale == 1, so world coords == chunk coords.
		float offsetScale = size / scale;
		Vector3 offset = position * offsetScale;

		// We negate the value because the inverse looks better for RidgedMultifractal. 
		// Switch to positive for Perlin noise.
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					data [coordsToIndex (sp1, i, j, k)] = (float) -noiseGen.GetValue(
						offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
				}
			}
		}

		return data;
	}

	private static GameObject generateObj(Vector3 position, bool doubleSided = false) {
		Profiler.BeginSample("Vertex generation");
		float[] data = generateData (size, position);
		Profiler.EndSample ();

		List<Vector3> verts = new List<Vector3> ();
		List<int> tris = new List<int> ();


		Profiler.BeginSample("Marching cubes");
		Marching marching = new MarchingCubes ();
		marching.Surface = 0f;

		marching.Generate(data, size + 1, size + 1, size + 1, verts, tris);
		Profiler.EndSample ();

		//Debug.Log (verts.Count);

		//Profiler.BeginSample ("Mesh generation");
		GameObject newObj = generateEmpty ();
		assignMesh (newObj, verts.ToArray (), tris.ToArray ());
		newObj.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) - meshOffset;

		//newObj.transform.localScale = new Vector3 (resolution, resolution, resolution);

		//Profiler.EndSample ();
		return newObj;

		/*
		if (doubleSided) {
			List<int> reverseTris = new List<int>();
			for (int i = 0; i < tris.Count; i += 3) {
				reverseTris.Add (tris [i]);
				reverseTris.Add (tris [i + 2]);
				reverseTris.Add (tris [i + 1]);
			}

			Mesh mesh2 = new Mesh ();
			mesh2.vertices = verts.ToArray ();
			mesh2.triangles = reverseTris.ToArray();
			mesh2.RecalculateNormals();

			GameObject newObj2 = new GameObject ();
			newObj2.AddComponent<MeshFilter> ();
			newObj2.AddComponent<MeshRenderer> ();
			newObj2.AddComponent<MeshCollider> ();

			newObj2.GetComponent<MeshFilter> ().mesh = mesh2;
			newObj2.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));
			newObj2.GetComponent<MeshCollider>().sharedMesh = mesh2; 

			newObj2.transform.position = new Vector3(position.z * size, position.y * size, position.x * size) -
				new Vector3(size / 2, size / 2, size / 2);
			//newObj2.transform.localScale = new Vector3 (resolution, resolution, resolution);
		}
		*/
	}

	/**
	 * Return immediately if "unfinishedObj" is destroyed before this method can finish.
	 */
	private static void assignMesh(GameObject unfinishedObj, Vector3[] vertices, int[] triangles) {
		if (unfinishedObj == null) { return; }
		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.RecalculateNormals ();

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshFilter> ().mesh = mesh;

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh; 
	}

	public static IEnumerator generateAsync(Vector3 position, GameObject unfinishedObj, bool doubleSided=false) {
		
		#region Create data

		int sp1 = size + 1;
		float[] data = new float[sp1 * sp1 * sp1];
		float offsetScale = size / scale;
		Vector3 offset = position * offsetScale;

		// The yield return is placed there to best optimize runtime.
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					data [coordsToIndex (sp1, i, j, k)] = (float) -noiseGen.GetValue(
						offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
				}
			}
			yield return null;
		}

		#endregion

		#region Perform Marching Cubes

		List<Vector3> verts = new List<Vector3> ();
		List<int> tris = new List<int> ();

		Marching marching = new MarchingCubes ();
		marching.Surface = 0f;

		marching.Generate(data, sp1, sp1, sp1, verts, tris);
		yield return null;

		#endregion

		assignMesh (unfinishedObj, verts.ToArray (), tris.ToArray ());
		yield return null;

		if (unfinishedObj != null) {
			unfinishedObj.transform.position = new Vector3 (position.z * size, position.y * size, position.x * size) - meshOffset;
		}
	}

	public static GameObject generateEmpty() {
		GameObject newObj = new GameObject ();

		newObj.AddComponent<MeshFilter> ();
		newObj.AddComponent<MeshRenderer> ();
		newObj.AddComponent<MeshCollider> ();

		newObj.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));
		return newObj;
	}

	public static void shiftArray(int x, int y, int z) {
		Debug.Log("Shift array called "  + x + ", " + y + ", " + z);

		// Moving left
		if (x < 0) {
			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = renderDiameter + x; k < renderDiameter; k++) {
						GameObject.Destroy (chunks [i, j, k]);
						chunks [i, j, k] = null;
					}
				}
			}

			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = renderDiameter - 1 + x; k >= 0; k--) {
						chunks [i, j, k - x] = chunks [i, j, k];
					}
				}
			}

			// Now chunks[0, i, j] can be set to null; they carry what is now in chunks[1, i, j].
		}

		// Moving right
		/*
		if (x > 0) {
			for (int i = 0; i < x; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = 0; k < renderDiameter; k++) {
						GameObject.Destroy (chunks [i, j, k]);
						chunks [i, j, k] = null;
					}
				}
			}

			for (int i = 0; i < renderDiameter - x; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = 0; k < renderDiameter; k++) {
						chunks [i, j, k] = chunks [i + x, j, k];
					}
				}
			}
		}
		*/
		// Moving down
		if (y < 0) {
			for (int i = 0; i < renderDiameter; i++) {
				for (int j = renderDiameter + y; j < renderDiameter; j++) {
					for (int k = 0; k < renderDiameter; k++) {
						GameObject.Destroy (chunks [i, j, k]);
						chunks [i, j, k] = null;
					}
				}
			}

			for (int i = 0; i < renderDiameter; i++) {
				for (int j = renderDiameter - 1 + y; j >= 0; j--) {
					for (int k = 0; k < renderDiameter; k++) {
						chunks [i, j - y, k] = chunks [i, j, k];
					}
				}
			}

			// Now chunks[0, i, j] can be set to null; they carry what is now in chunks[1, i, j].
		}

		#region Moving up
		if (y > 0) {
			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < y; j++) {
					for (int k = 0; k < renderDiameter; k++) {
						GameObject.Destroy (chunks [i, j, k]);
						chunks [i, j, k] = null;
					}
				}
			}

			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter - y; j++) {
					for (int k = 0; k < renderDiameter; k++) {
						chunks [i, j, k] = chunks [i, j + y, k];
					}
				}
			}
		}
		#endregion

		// Moving backward
		if (z < 0) {
			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = renderDiameter + z; k < renderDiameter; k++) {
						GameObject.Destroy (chunks [i, j, k]);
						chunks [i, j, k] = null;
					}
				}
			}

			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = renderDiameter - 1 + z; k >= 0; k--) {
						chunks [i, j, k - z] = chunks [i, j, k];
					}
				}
			}

			// Now chunks[0, i, j] can be set to null; they carry what is now in chunks[1, i, j].
		}

		// Moving forward
		if (z > 0) {
			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = 0; k < z; k++) {
						GameObject.Destroy (chunks [i, j, k]);
						chunks [i, j, k] = null;
					}
				}
			}

			for (int i = 0; i < renderDiameter; i++) {
				for (int j = 0; j < renderDiameter; j++) {
					for (int k = 0; k < renderDiameter - z; k++) {
						chunks [i, j, k] = chunks [i, j, k + z];
					}
				}
			}
		}
	}

	void Start() {
	}

	void Update() {
		
	}
}
