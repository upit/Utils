using System.Collections.Generic;
using UnityEngine;

namespace Utils.FastShadows
{
	/// <summary> Класс источников тени. </summary>
	public class FastShadowSource : MonoBehaviour
	{
		private const float SHADOW_BORDER = 0.5f;	// Рамка, чтобы не рендерить в упор к границам текстуры.
		
		// Кэш для быстрого доступа. 
		private MeshRenderer rend;
		private Material mat;
		private Transform shadowTransform;
		private float size;
		private static readonly int idShadowIntensity = Shader.PropertyToID("_ShadowIntensity");
		private void Awake () {
			
			rend = GetComponent<MeshRenderer>();
			rend.receiveShadows = false;
			rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			mat = rend.material;
			
			shadowTransform = transform;
			Vector3 extents = rend.bounds.extents;
			size = Mathf.Max(extents.x, extents.y, extents.z) + SHADOW_BORDER;
			//CheckSources();
		}

		/// <summary> Границы рендера. </summary>
		public Bounds GetBounds() {return rend.bounds;}
		
		public void SetVisible(bool visible) {rend.enabled = visible;}
		
		public void SetIntensity(float intensity){mat.SetFloat(idShadowIntensity,intensity);} 

		public float GetShadowSize(){return size;}

		public Vector3 GetShadowPos() {return shadowTransform.position;}
		
		private void AddToList()
		{
			rend.enabled = true;
			FastShadowProjector.Add(this);
		}

		private void RemoveFromList()
		{
			rend.enabled = false;
			FastShadowProjector.Remove(this);
		}

		private void OnEnable(){AddToList();}
		private void OnBecameVisible() {AddToList();}
		
		private void OnDisable() {RemoveFromList();}
		private void OnDestroy() {RemoveFromList();}
		private void OnBecameInvisible() {RemoveFromList();}
		
		
		
	}
}