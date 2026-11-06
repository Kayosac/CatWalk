using UnityEngine;

namespace CatWalk.Rendering
{
    /// <summary>Identifies the native-resolution presentation camera, which never renders 3D geometry.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldResolutionOutput : MonoBehaviour
    {
        [System.NonSerialized] public WorldResolution Owner;
    }
}
