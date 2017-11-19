using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using LibNoise.Generator;
using MarchingCubesProject;

public class Generator : MonoBehaviour {

	private static Perlin perlin;

	public static int size = 32;
	public static float scale = 16f;
	public static float[] data;

	private static Material defaultMaterial;
	private static PhysicMaterial defaultPhysics;

	private static GameObject[,,] chunks;

	public static int coordsToIndex(int size, int x, int y, int z) {
		return (x * size * size) + (y * size) + z;
	}

	public static Vector3 indexToCoords(int size, int i) {
		return new Vector3 ((i / size / size) % size, (i / size) % size, i % size);
	}

	public static void generate() {
		perlin = new Perlin ();
		perlin.OctaveCount = 10;

		int count = 3;

		for (int i = 0; i < count; i++) {
			for (int j = 0; j < count; j++) {
				for (int k = 0; k < count; k++) {
					generateObj (new Vector3 (i, j, k));
				}
			}
		}
	}

	private static void generateObj(Vector3 position) {
		int sp1 = size + 1;
		data = new float[sp1 * sp1 * sp1];

		float offsetScale = size / scale;
		Vector3 offset = new Vector3 (position.x * offsetScale, position.y * offsetScale, position.z * offsetScale);

		// Vertex array
		for (int i = 0; i < sp1; i++) {
			for (int j = 0; j < sp1; j++) {
				for (int k = 0; k < sp1; k++) {
					data [coordsToIndex (sp1, i, j, k)] = (float)perlin.GetValue(
						offset.x + (i / scale), offset.y + (j / scale), offset.z + (k / scale));
				}
			}
		}

		List<Vector3> verts = new List<Vector3> ();
		List<int> tris = new List<int> ();

		Marching marching = new MarchingCubes ();
		marching.Surface = 0f;

		marching.Generate(data, sp1, sp1, sp1, verts, tris);

		List<int> reverseTris = new List<int>();
		for (int i = 0; i < tris.Count; i += 3) {
			reverseTris.Add (tris [i]);
			reverseTris.Add (tris [i + 2]);
			reverseTris.Add (tris [i + 1]);
		}


		//Debug.Log (verts.Count);

		Mesh mesh = new Mesh ();
		mesh.vertices = verts.ToArray();
		mesh.triangles = tris.ToArray();
		mesh.RecalculateNormals();

		Mesh mesh2 = new Mesh ();
		mesh2.vertices = verts.ToArray ();
		mesh2.triangles = reverseTris.ToArray();
		mesh2.RecalculateNormals();

		GameObject newObj = new GameObject ();
		newObj.AddComponent<MeshFilter> ();
		newObj.AddComponent<MeshRenderer> ();
		newObj.AddComponent<MeshCollider> ();

		newObj.GetComponent<MeshFilter> ().mesh = mesh;
		newObj.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));
		newObj.GetComponent<MeshCollider>().sharedMesh = mesh; 

		newObj.transform.position = new Vector3(position.z * size, position.y * size, position.x * size);

		GameObject newObj2 = new GameObject ();
		newObj2.AddComponent<MeshFilter> ();
		newObj2.AddComponent<MeshRenderer> ();
		newObj2.AddComponent<MeshCollider> ();

		newObj2.GetComponent<MeshFilter> ().mesh = mesh2;
		newObj2.GetComponent<MeshRenderer> ().material = new Material(Shader.Find("Diffuse"));
		newObj2.GetComponent<MeshCollider>().sharedMesh = mesh2; 

		newObj2.transform.position = new Vector3(position.z * size, position.y * size, position.x * size);
	}

	private IEnumerator generateAsync(Vector3 position, GameObject unfinishedObj) {
		yield return null;
	}

	void Start() {
		defaultPhysics = new PhysicMaterial ();
		defaultPhysics.bounciness = 0.0f;
		defaultPhysics.dynamicFriction = 0.4f;
		defaultPhysics.staticFriction = 0.4f;
	}

	void Update() {
		
	}
}
