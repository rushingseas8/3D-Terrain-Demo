using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Runtime.InteropServices;

using LibNoise.Generator;

public class GameManager : MonoBehaviour {

	public static bool twoDMode = true;

	//private bool[,,] data;
	private bool[] data;
	private Vector3[] dataVec;

	// Use this for initialization
	void Start () {

		/*
		int length = 10;
		float scale = 5.0f;
		//data = new bool[length, length, length];
		data = new bool[length * length * length];

		for (int i = 0; i < length; i++) {
			for (int j = 0; j < length; j++) {
				for (int k = 0; k < length; k++) {
					double val = perlin.GetValue (scale * i / length, scale * j / length, scale * k / length);
					val = (val + 1.0) / 2.0;


					//data [i, j, k] = val > 0.6;
					data[(i * length * length) + (j * length) + k] = val > 0.6;


					//Debug.Log (val);
					GameObject newObj = GameObject.CreatePrimitive (PrimitiveType.Cube);

					newObj.GetComponent<MeshRenderer> ().receiveShadows = false;
					newObj.GetComponent<MeshRenderer> ().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
					newObj.GetComponent<MeshRenderer> ().lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
					newObj.GetComponent<Collider> ().enabled = false;
					newObj.isStatic = true;

					newObj.transform.localScale = new Vector3 ((float)val, (float)val, (float)val);
					newObj.transform.position = new Vector3 (i, j, k);
				}
			}
		}
		*/

		/*
		int length = 10;
		float scale = 1.0f;
		data = new bool[length * length * length];
		dataVec = new Vector3[length * length * length];

		for (int i = 0; i < length; i++) {
			for (int j = 0; j < length; j++) {
				for (int k = 0; k < length; k++) {
					double val = perlin.GetValue (scale * i / length, scale * j / length, scale * k / length);
					val = (val + 1.0) / 2.0;

					dataVec [(i * length * length) + (j * length) + k] = new Vector3 (i, j, k);
					data[(i * length * length) + (j * length) + k] = val > 0.6;
				}
			}
		}
		*/

		if (twoDMode) {
			//GenerateTerrain.generate ();
			Generator2D.generate();
		} else {
			GameObject.Destroy (GameObject.Find ("Sun"));
			GeneratorCave.generate ();
		}
	}

	/*
	private IEnumerator Generate() {
		//WaitForSeconds wait = new WaitForSeconds(0.001f);

		int length = 10;
		float scale = 1.0f;
		data = new bool[length * length * length];
		dataVec = new Vector3[length * length * length];

		for (int i = 0; i < length; i++) {
			for (int j = 0; j < length; j++) {
				for (int k = 0; k < length; k++) {
					double val = perlin.GetValue (scale * i / length, scale * j / length, scale * k / length);
					val = (val + 1.0) / 2.0;

					dataVec [(i * length * length) + (j * length) + k] = new Vector3 (i, j, k);
					data[(i * length * length) + (j * length) + k] = val > 0.6;
					yield return null;
				}
			}
		}
	}
	*/
	
	// Update is called once per frame
	void Update () {
		
	}
}
