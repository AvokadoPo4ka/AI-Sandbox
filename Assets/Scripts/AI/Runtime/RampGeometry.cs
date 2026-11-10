using UnityEngine;
namespace RuntimeCreation {
    // The generated mesh is owned by its object and destroyed with it, including rollback.
    public sealed class RampGeometry : MonoBehaviour {
        Mesh owned;
        public static GameObject Create() {
            var go=new GameObject("ramp",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider),typeof(RampGeometry));
            var owner=go.GetComponent<RampGeometry>(); owner.owned=new Mesh(); owner.owned.name="AI ramp mesh";
            owner.owned.vertices=new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f) };
            owner.owned.triangles=new[] {0,1,2, 1,3,2, 2,3,4, 3,5,4, 0,2,4, 1,5,3, 0,4,1, 1,4,5};
            owner.owned.RecalculateNormals(); owner.owned.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh=owner.owned;
            var collider=go.GetComponent<MeshCollider>(); collider.sharedMesh=owner.owned; collider.convex=true;
            return go;
        }
        void OnDestroy() { if(owned) Destroy(owned); }
    }
}
