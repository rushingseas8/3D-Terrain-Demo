using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * The base class from which Generators inherit.
 * 
 * This contains a lot of helper methods for mesh generation and whatnot.
 */
public class GeneratorBase {
	
	// A diffuse default material we assign to meshes.
	protected static Material defaultMaterial;

	// A default physics material that is somewhat sticky for testing.
	protected static PhysicMaterial defaultPhysics;

	static GeneratorBase() {
		//defaultMaterial = new Material(Shader.Find("Diffuse"));
		defaultMaterial = new Material(Resources.Load("Materials/Grass") as Material);

		defaultPhysics = new PhysicMaterial ();
		defaultPhysics.bounciness = 0.0f;
		defaultPhysics.dynamicFriction = 1.0f;
		defaultPhysics.staticFriction = 1.0f;
	}

	/**
	 * Creates an empty shell GameObject, ready to be passed into "assignMesh".
	 */
	public static GameObject generateEmpty() {
		GameObject newObj = new GameObject ();

		newObj.AddComponent<MeshFilter> ();
		newObj.AddComponent<MeshRenderer> ();
		newObj.AddComponent<MeshCollider> ();

		newObj.GetComponent<MeshRenderer> ().material = defaultMaterial;
		return newObj;
	}

	/**
	 * Creates a new mesh and assigns it to the empty gameobject provided.
	 * Return immediately if "unfinishedObj" is destroyed before this method can finish,
	 * which can happen if we do this asynchronously.
	 */
	public static void assignMesh(GameObject unfinishedObj, Vector3[] vertices, int[] triangles, Vector2[] uvs=null, Vector3[] normals=null) {
		if (unfinishedObj == null) { return; }
		Mesh mesh = new Mesh ();
		mesh.vertices = vertices;
		mesh.triangles = triangles;

		if (uvs == null) {
			uvs = new Vector2[vertices.Length];
			for (int i = 0; i < uvs.Length; i += 3) {
				uvs [i + 0] = new Vector2(0, 0);
				uvs [i + 1] = new Vector2(1, 0);
				uvs [i + 2] = new Vector2(1, 1);
			}
		}
		mesh.uv = uvs;
	
		if (normals == null) {
			mesh.RecalculateNormals ();
		} else {
			mesh.normals = normals;
		}

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshFilter> ().mesh = mesh;

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshRenderer> ().material = defaultMaterial;

		if (unfinishedObj == null) { return; }
		unfinishedObj.GetComponent<MeshCollider>().sharedMesh = mesh; 
	}
}
