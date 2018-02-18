using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TextureManager {

	private const int TEXTURE_SIZE = 256;
	private const string ATLAS_PATH = "Assets/Resources/TexturesOut/atlas.png";
	public static Texture2D atlas;

	public static void initialize() {
		Texture2D[] textures = Resources.LoadAll<Texture2D> ("Textures");
		//Debug.Log ("TextureStitcher number of textures: " + textures.Length);

		int length = textures.Length;
		//int rootLength = (int)Mathf.Ceil(Mathf.Sqrt (length));
		int rootLength = (int)Mathf.Pow(Mathf.Ceil(Mathf.Log(Mathf.Sqrt(length), 2)), 2);

		atlas = new Texture2D (rootLength * TEXTURE_SIZE, rootLength * TEXTURE_SIZE);

		int count = 0;
		for (int i = 0; i < length; i++) {
			atlas.SetPixels32 (TEXTURE_SIZE * (count % rootLength), TEXTURE_SIZE * (count / rootLength), TEXTURE_SIZE, TEXTURE_SIZE, textures[i].GetPixels32());
			count++;
		}

		byte[] pngData = atlas.EncodeToPNG ();
		File.WriteAllBytes (ATLAS_PATH, pngData);
	}
}
