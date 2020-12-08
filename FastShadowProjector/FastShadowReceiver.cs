using System.Collections.Generic;
using UnityEngine;
using Utils.FastShadows;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FastShadowReceiver : MonoBehaviour {
	
	[SerializeField] private Mesh recieverMesh;		// Сериализуем меш, чтобы получать к нему доступ после батчинга.
	
	private void Awake ()
	{
		if (recieverMesh != null)
			return;

		Debug.LogError($"Shadow reciever {name} has no mesh! Reciever destroyed!", gameObject);
		Destroy(this);
	}

	/// <summary>Получить меш приемника, даже если он является частью батча. </summary>
	public Mesh GetMesh(){return recieverMesh;}

	private void AddReceiver(){FastShadowProjector.Add(this);}
	private void RemoveReceiver(){FastShadowProjector.Remove(this);}

	private void OnEnable() {AddReceiver();}
	private void OnBecameVisible() {AddReceiver();}
	
	private void OnDisable() {RemoveReceiver();}
	private void OnBecameInvisible() {RemoveReceiver();}
	private void OnDestroy() {RemoveReceiver();}


#if UNITY_EDITOR
	/// <summary> Для едитор-скрипта, чтобы автоматичнски назначать меши. </summary>
	public void SetMesh(Mesh mesh)
	{
		recieverMesh = mesh;
	}
#endif

	
}