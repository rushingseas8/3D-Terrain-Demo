using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk {

	public Vector3 position;
	public GameObject obj;
	public float[] data;

	public Chunk(Vector3 position, GameObject obj, float[] data=null) {
		this.position = position;
		this.obj = obj;
		this.data = data;
	}
}
