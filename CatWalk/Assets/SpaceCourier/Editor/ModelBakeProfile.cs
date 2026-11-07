using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepField.Editor
{
    [CreateAssetMenu(menuName = "Deep Field/Art/Model bake profile", fileName = "ModelBake")]
    public sealed class ModelBakeProfile : ScriptableObject
    {
        public GameObject Source;
        public string PrefabPath;
        public string MeshDirectory = "Assets/SpaceCourier/Content/Meshes";
        public string MaterialDirectory = "Assets/SpaceCourier/Content/Materials";
        public bool Character;
        public Vector3 ImportRotation = new Vector3(0, 180, 0);
        [Min(0)] public float ShoulderHeight = 1.14f, HipHeight = .63f;
        [Serializable] public sealed class MaterialBinding { public string SourceName; public Material Material; }
        public List<MaterialBinding> Materials = new List<MaterialBinding>();
        [HideInInspector] public List<string> GeneratedParts = new List<string>();
        [HideInInspector] public string SourceHash;
    }
}
