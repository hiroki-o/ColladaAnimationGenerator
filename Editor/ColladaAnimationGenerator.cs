using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AssetGraph;

[System.Serializable]
[CustomAssetGenerator("Export Animation to Collada", "v1.0")]
public class ColladaAnimationGenerator : IAssetGenerator {

    [SerializeField] private GameObjectReference m_targetPrefab;

    public void OnValidate () {
        if (m_targetPrefab == null) {
            m_targetPrefab = new GameObjectReference ();
        }

        if (m_targetPrefab.Object == null) {
            throw new NodeException ("Animation Target Prefab is empty.", "Configure prefab from inspector.");
        }
    }

    public string GetAssetExtension (AssetReference asset) {
        return ".dae";
    }

    public Type GetAssetType(AssetReference asset) {
        return typeof(AnimationClip);
    }

	public bool CanGenerateAsset (AssetReference asset) {
        if (asset.assetType != typeof(AnimationClip)) {
			return false;
		}
		return true;
	}

	/**
	 * Generate asset.
	 */ 
	public bool GenerateAsset (AssetReference asset, string generateAssetPath) {

        var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(asset.importFrom);
        if (animationClip == null) {
			return false;
		}

        string fullPath = FileUtility.PathCombine (Directory.GetParent(Application.dataPath).ToString(), generateAssetPath);
        ColladaExporter collada = new ColladaExporter (fullPath, true);

        collada.AddObjectToScene (m_targetPrefab.Object, false);
        collada.AddAnimationClip (animationClip, m_targetPrefab.Object);

        collada.Save ();

        Resources.UnloadAsset (animationClip);

		return true;
	}

	/**
	 * Draw Inspector GUI for this AssetGenerator.
	 */ 
	public void OnInspectorGUI (Action onValueChanged) {

        var newObj = (GameObject)EditorGUILayout.ObjectField ("Animation Target Prefab", m_targetPrefab.Object, typeof(GameObject), false);
        if (newObj != m_targetPrefab.Object) {
            m_targetPrefab.Object = newObj;
            onValueChanged ();
        }
	}
}