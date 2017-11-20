using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeStorage {

	public static Dictionary<Vector3Int, GameObject> storage;

	public CubeStorage() {
		storage = new Dictionary<Vector3Int, GameObject> ();
	}
}
