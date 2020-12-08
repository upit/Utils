using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Utils.DecalSystem
{
	/// <summary> Класс для построения декалей на геометрии и коллайдерах.</summary>
	[RequireComponent( typeof(MeshFilter) )]
	[RequireComponent( typeof(MeshRenderer) )]
	public class Decal : MonoBehaviour
	{
	    [SerializeField] private float 	   maxAngle = 	90.0f;	// Угол между полигонами. Больше - декаль не проецируется.
	    [SerializeField] private float	   offset = 	0.009f;	// Смещение от геометрии, чтобы избежать Z-файтинга.
	    [SerializeField] private LayerMask layerMask = -1;		// Маска коллизий.
	    [SerializeField] private Animation anim;
    
	    // ReSharper disable once RedundantDefaultMemberInitializer
	    [SerializeField] private Sprite[] 	sprites =  default;	// Набор спрайтов. Спрайт выбирается рандомно из набора.
	    
	    private readonly List<Vector3> bufVertices = new List<Vector3>();	// Буффер вершин для построения декаля.
	    private readonly List<Vector3> normals = new List<Vector3>();		// ... нормалей.
	    private readonly List<Vector2> texCoords = new List<Vector2>();		// ... координат текстур.
	    private readonly List<int> 	   indices = new List<int>();			// ... индексов вершин полигонов.

	    private Transform decalTransform;		// Кэшируем transfrorm для частого обращения.
	    private List<Collider> affectedColliders;	// Коллайдеры, на которые могут проецироваться декали.
	    private bool hasAnim;					// Имеет ли декаль анимацию.

	    private const int MIN_QUALITY_LEVEL = 3;

	    private void Awake(){CheckAndDestroy(this);}

	    /// <summary> MonoBehaviour event. Вызывается, когда скрипт активен перед всеми Update методами. </summary>
	    private void Start()
	    {
		    decalTransform = transform;	// Кэшируем transform.
    
		    // Отбираем подходящие коллайдеры (нужный слой, активный коллайдер и т.д.)
		    Collider[] colliders = FindObjectsOfType<Collider>();
		    affectedColliders = new List<Collider>();
		    for (int i = 0; i < colliders.Length; i++)
		    {
			    if ((layerMask.value & 1 << colliders[i].gameObject.layer) != 0 && colliders[i].enabled)
			    {
				    // Если это mesh collider - смотрим, есть ли доступ к его данным (чекбокс read/write при импорте).
				    if (colliders[i].GetType() == typeof(MeshCollider))
				    {
					    Mesh mesh;
					    if ((mesh = ((MeshCollider)colliders[i]).sharedMesh) == null || !mesh.isReadable)
							continue;
				    }

				    affectedColliders.Add(colliders[i]);
			    }
		    }

		    hasAnim = anim != null;
	    }

	    /// <summary> Проверяет соответствует ли минимальным настройкам графики, в противном случае удаляет </summary>
	    public static bool CheckAndDestroy(Decal decal)
	    {
		    if (QualitySettings.GetQualityLevel() >= MIN_QUALITY_LEVEL)
			    return true;

		    decal.gameObject.SetActive(false);
		    Destroy(decal);
		    return false;
	    } 

	    /// <summary> Возвращает AABB контейнер для transform. </summary>
	    private static Bounds GetBounds(Transform trans) {
		    
		    // Задаем 8 вершин габаритного контейнера трансформа.
		    Vector3 size = trans.lossyScale;
		    Vector3 min = -size * 0.5f;
		    Vector3 max =  size * 0.5f;
		    
		    Vector3[] vts = 
		    {
			    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
			    new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
			    
			    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
			    new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
		    };

		    // Переводим координаты вершин из локальных в мировые.
		    for (int i = 0; i < vts.Length; i++) 
				vts[i] = trans.TransformDirection(vts[i]);
		    
		    // Вычисляем минимальные и максимальные значения координат. Разница между ними и будет размер AABB.
		    min = max = vts[0];
		    for (int i = 0; i < vts.Length; i++)
		    {
			    min = Vector3.Min(min, vts[i]);
			    max = Vector3.Max(max, vts[i]);
		    }

		    return new Bounds(trans.position, max - min);
	    }

	    /// <summary> Строит декаль и возвращает её gameobject. </summary>
	    public GameObject Build()
	    {
		    // Вычисляем какие коллайдеры находятся внутри AABB габаритов контейнера декали.
		    // Строим меш для каждого такого коллайдера.
		    Collider[] colliders = GetCollidersInBounds(affectedColliders, GetBounds(decalTransform));
		    for (int i = 0; i < colliders.Length; i++)
			    BuildForCollider(colliders[i]);
			
		    // Смещаем вершины по нормали, чтобы не "проваливались" в геометрию и не было Z-файтинга. 
		    for(int i=0; i<bufVertices.Count; i++)
			    bufVertices[i] += normals[i] * offset;

		    // Если при проекцировании не построилось ни одного полигона - прерываем.
		    if (indices.Count == 0)
			    return null;
		    
		    // Строим меш и возвращаем его gameobject.
		    GameObject go = Instantiate(gameObject, decalTransform.position, decalTransform.rotation);
		    go.GetComponent<MeshFilter>().mesh = CreateMesh();
		    return go;
	    }
	    /// <summary> Строит декаль в заданной позиции и под заданным углом. Возвращает её gameobject. </summary>
	    public GameObject Build(Vector3 pos, Vector3 dir)
	    {
// В редакторе не отрабатывается Start() поэтому воспроизводим его.
#if UNITY_EDITOR
		    if (!Application.isPlaying)	// Инициализация старта в редакторе, если не в режиме игры.
			    Start();
#endif
		    decalTransform.position = pos;	// Выставляем контейнер декали.
		    decalTransform.forward = dir;
		    return Build();
	    }

	    /// <summary> Проигрывает анимацию декали и возвращает callback. </summary>
	    public void PlayAnimation(float delay, Action callback = null)
	    {
		    StartCoroutine(AnimationRoutine(delay, callback));
	    }

	    /// <summary> Корутина для проигрывания анимации декали. </summary>
	    private IEnumerator AnimationRoutine(float delay, Action callback)
	    {
		    yield return new WaitForSeconds(delay);    // Ждем перед началом анимации.
		    
		    // Если у декали есть анимация - проигрываем ее, после чего вызываем callback.
		    if (hasAnim)
		    {
			    anim.Play();
			    yield return new WaitForSeconds(anim.clip.length);
		    }
		    
		    callback?.Invoke();   // Если есть callback - выполняем.
	    }
	    
	    /// <summary> Создает и возвращает меш по данным вершин, нормалей и т.д. </summary>
	    private Mesh CreateMesh()
		{
			Mesh mesh = new Mesh
			{
				vertices = bufVertices.ToArray(),
				normals = normals.ToArray(),
				uv = texCoords.ToArray(),
				triangles = indices.ToArray()
			};

			// Очищаем буферы.
			bufVertices.Clear();
			normals.Clear();
			texCoords.Clear();
			indices.Clear();

			return mesh;
		}

		/// <summary> Возвращает все коллайдеры внутри заданного габаритного контейнера. </summary>
	    private static Collider[] GetCollidersInBounds(IList<Collider> colliders, Bounds bounds)
		{
			List<Collider> result = new List<Collider>();
			for (int i = 0; i < colliders.Count; i++)
			{
				if (colliders[i] == null)	// Если коллайдер вдруг перестал существовать - удаляем его из списка.
				{
					colliders.RemoveAt(i);
					continue;
				}

				if (bounds.Intersects(colliders[i].bounds))
					result.Add(colliders[i]);
			}

			return result.ToArray();
		}

		/// <summary> Строит декаль для коллайдера. </summary>
		public void BuildForCollider(Collider affectedCollider)
		{
			// Если меш коллайдер - строим декаль по его мешу.
			if (affectedCollider.GetType() == typeof(MeshCollider))
				BuildForMesh(affectedCollider.gameObject, ((MeshCollider)affectedCollider).sharedMesh);
			// Если бокс - создаем бокс и строим декаль по его геометрии, потом удаляем :)
			// TODO@Upit Подумать над более грамотной реализацией.
			else if(affectedCollider.GetType() == typeof(BoxCollider))
			{
				Transform colliderTransform = affectedCollider.transform;
				Transform cubeTransform = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
				cubeTransform.position = ((BoxCollider) affectedCollider).bounds.center;

				Quaternion rotation = colliderTransform.rotation;
				colliderTransform.rotation = Quaternion.identity;

				cubeTransform.localScale = ((BoxCollider) affectedCollider).bounds.size;

				// ReSharper disable once Unity.InefficientPropertyAccess
				colliderTransform.rotation = rotation;
				cubeTransform.rotation = rotation;

				BuildForMesh(cubeTransform.gameObject, cubeTransform.GetComponent<MeshFilter>().sharedMesh);
#if UNITY_EDITOR
				if (UnityEditor.EditorApplication.isPlaying)
					Destroy(cubeTransform.gameObject);
				else
					DestroyImmediate(cubeTransform.gameObject);
#else
				Destroy(cubeTransform.gameObject);
#endif
			}
		}

		/// <summary> Строит декаль по мешу заданного gameobject. </summary>
		private void BuildForMesh(GameObject affectedObject, Mesh affectedMesh)
		{
			// Берем меш gameobject и его компонененты (вершины, полигоны).
			//Mesh affectedMesh = affectedObject.GetComponent<MeshFilter>().sharedMesh;
			
			Vector3[] vertices = affectedMesh.vertices;
			int[] triangles = affectedMesh.triangles;
			int startVertexCount = bufVertices.Count;

			// Задаем 6 плоскостей для "обрезания" вершин.
			Vector3 halfRight = Vector3.right * 0.5f;
			Vector3 halfUp = Vector3.up * 0.5f;
			Vector3 halfForward = Vector3.forward * 0.5f;
			Plane[] planes =
			{
				new Plane(Vector3.right, halfRight),		// Право
				new Plane(-Vector3.right, -halfRight),		// Лево
				new Plane(Vector3.up, halfUp),				// Верх
				new Plane(-Vector3.up, -halfUp),			// Низ
				new Plane(Vector3.forward, halfForward),	// Перед
				new Plane(-Vector3.forward, -halfForward)	// Зад
			};

			Matrix4x4 matrix = decalTransform.worldToLocalMatrix * affectedObject.transform.localToWorldMatrix;
			for (int i = 0; i < triangles.Length; i += 3)
			{
				// Берем 3 вершины грани, считаем по ним нормаль грани.
				Vector3[] verts =
				{
					matrix.MultiplyPoint(vertices[triangles[i]]),
					matrix.MultiplyPoint(vertices[triangles[i + 1]]),
					matrix.MultiplyPoint(vertices[triangles[i + 2]]),
				};
				Vector3 normal = Vector3.Cross(verts[1] - verts[0], verts[2] - verts[1]).normalized;
				
				// Если угол между нормалью и направлением проецирования больше заданного угла - идем к след грани.
				if (Vector3.Angle(-Vector3.forward, normal) >= maxAngle)
					continue;

				// Обрезаем вершины 6ю плоскостями и проверяем осталась хоть одна вершина или нет.
				// Если нет - значит полигон целиком за границами габаритного контейнера. Игнорируем его.
				bool polyOutOfRange = false;
				for (int j = 0; j < planes.Length; j++)
				{
					if ((verts = ClipVertices(planes[j], verts)) == null)
					{
						polyOutOfRange = true;
						break;
					}
				}

				if (polyOutOfRange)
					continue;

				// Строим полигон по результатам обюрезания вершин.
				AddPolygon(verts, normal);
			}

			GenerateTexCoords(startVertexCount, sprites[Random.Range (0, sprites.Length)]);	// Координаты текстур.
		}

		/// <summary> Строит полигон по заданным вершинам и нормали. </summary>
		private void AddPolygon(IReadOnlyList<Vector3> verts, Vector3 normal)
		{
			int[] ind = {AddVertex(verts[0], normal), 0, 0};	// Строим от 0й вершины.
			for (int i = 1; i < verts.Count - 1; i++)
			{
				ind[1] = AddVertex(verts[i], normal);
				ind[2] = AddVertex(verts[i + 1], normal);

				for (int j = 0; j < ind.Length; j++)	// Индексы вершин в полигоне.
					indices.Add(ind[j]);
			}
		}

		/// <summary> Добавляет вершину и возвращает ее индекс. Если уже существует - просто возвращает индекс. </summary>
		private int AddVertex(Vector3 vertex, Vector3 normal)
		{
			int index = FindVertex(vertex);
			if (index == -1)	// Если вершина не найдена - добавляем и возвращаем индекс добавленной (последний).
			{
				bufVertices.Add(vertex);
				normals.Add(normal);
				return bufVertices.Count - 1;
			}

			normals[index] = (normals[index] + normal).normalized; // Вершина уже существует, просто отклоняем нормаль.
			
			return index;
		}

		/// <summary> Находит индекс вершины по ее координатам. Возвращает -1, если ничего не найдено. </summary>
		private int FindVertex(Vector3 vertex)
		{
			for (int i = 0; i < bufVertices.Count; i++)
				if (Vector3.Distance(bufVertices[i], vertex) < 0.01f)
					return i;

			return -1;
		}

		/// <summary> Обрезает вершины, если они за плоскостью отсечения и добавляет новые вершины в местах обрезания. </summary>
		private static Vector3[] ClipVertices(Plane plane, Vector3[] vertices)
		{
			bool[] positive = new bool[9];
			int positiveCount = 0;	// Кол-во вершин перед плоскостью.

			for (int i = 0; i < vertices.Length; i++)
			{
				positive[i] = !plane.GetSide(vertices[i]);
				if (positive[i])
					positiveCount++;
			}

			if (positiveCount == 0) // Все вершины полностью за плоскостью обрезания. Нечего возвращать.
				return null;

			if (positiveCount == vertices.Length) // Все вершины перед плоскостью. 
				return vertices;

			List<Vector3> tmpVertices = new List<Vector3>();
			for (int i = 0; i < vertices.Length; i++)
			{
				int next = i + 1;
				next %= vertices.Length;

				if (positive[i])
				{
					tmpVertices.Add(vertices[i]);
				}

				if (positive[i] != positive[next])
				{
					Vector3 v1 = vertices[next];
					Vector3 v2 = vertices[i];

					Ray ray = new Ray(v1, v2 - v1);
					plane.Raycast(ray, out float dis);
					tmpVertices.Add(ray.GetPoint(dis));
				}
			}

			return tmpVertices.ToArray();
		}

		/// <summary> Вычисляет координаты текстур, в зависимости от Recta спрайта. </summary>
		/// <param name="start">индекс вершины с которой начинать  вычислять.</param>
		/// <param name="sprite"></param>
		private void GenerateTexCoords(int start, Sprite sprite)
		{
			Rect rect = sprite.rect;
			rect.x /= sprite.texture.width;
			rect.y /= sprite.texture.height;
			rect.width /= sprite.texture.width;
			rect.height /= sprite.texture.height;

			for (int i = start; i < bufVertices.Count; i++)
			{
				Vector3 vertex = bufVertices[i];

				Vector2 uv = new Vector2(vertex.x + 0.5f, vertex.y + 0.5f);
				uv.x = Mathf.Lerp(rect.xMin, rect.xMax, uv.x);
				uv.y = Mathf.Lerp(rect.yMin, rect.yMax, uv.y);

				texCoords.Add(uv);
			}
		}
		
#if UNITY_EDITOR
		/// <summary> Рисовать габаритный контейнер в редакторе. </summary>
		private void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube( Vector3.zero, Vector3.one );
		}

		public int GetLayerMask(){return layerMask;}
#endif
	}
}

