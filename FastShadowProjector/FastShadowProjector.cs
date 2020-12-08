using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Utils.FastShadows
{
	/// <summary> Класс для быстрого создания теней. Отдельно выбираются источники теней и получатели. </summary>
	public class FastShadowProjector : MonoBehaviour
	{
		// TODO@Upit заменить рейкаст для проверки видимости на стандартные события (OnBecameVisible/Invisible).
#pragma warning disable 0649
		[SerializeField] private LayerMask cullingMask;		// Маска, по которой будут рендериться тени.
		[SerializeField] private LayerMask raycastMask;		// Маска для рейкаста теней на видимость.
		[SerializeField] private Shader shader;				// Шейдер для проецирования на геометрию.
		[SerializeField] [Range(16, 2048)] private int shadowResolution = 64;	// Разрешение тени.
		[SerializeField] private FilterMode textureFilterMode = FilterMode.Bilinear;	// Фильтрация текстуры тени.
		[SerializeField] [Range(10,500)] private float shadowCutOffDistance = 40.0f;	// Дальность видимости теней.
		[SerializeField] [Range(10,500)] private float shadowFadeDistance = 30.0f;		// Дальность начала "исчезания".
#pragma warning restore 0649

		private float shadowCutOffSqrDistance;	// Квадрат дальности для быстрых расчетов.
		private float shadowFadeSqrDistance;
		
		private Camera projectCamera;			// Камера для рендеринга теней.
		private Transform projectTransform;		// Кэш трансформа камеры.
		private Camera mainCamera;
		private Transform mainCameraTransform;
		
		private Material projectorMaterialShadow; // Материал для проецирования, создается из назначенного шейдера.

		// Кэш свойств для быстрого доступа.
		private static readonly int idGlobalProjector = Shader.PropertyToID("_GlobalProjector");
		private static readonly int idGlobalProjectorClip = Shader.PropertyToID("_GlobalProjectorClip");
		private static readonly int idShadowTex = Shader.PropertyToID("_ShadowTex");
		private static readonly int unity5BugFix = Shader.PropertyToID("_Unity5BugFix");
		
		private static readonly List<FastShadowSource> shadowSources = new List<FastShadowSource>();	// Список всех добавленных источников теней.
		private static readonly List<FastShadowReceiver> shadowReceivers = new List<FastShadowReceiver>();	// Все добавленные получатели теней.

		private static FastShadowProjector instance;
		

		private void Awake()
		{
			if (instance != null)
			{
				Destroy(gameObject);
				return;
			}
			// Добавляем камеру.
			projectCamera = gameObject.AddComponent<Camera>();
			projectCamera.clearFlags = CameraClearFlags.SolidColor;
			projectCamera.backgroundColor = Color.white;
			projectCamera.orthographic = true;
			projectCamera.nearClipPlane = -1;
			projectCamera.farClipPlane = 100;
			projectCamera.depth = -100f;
			projectCamera.aspect = 1.0f;
			projectCamera.enabled = false;
			
			projectTransform = transform;	// Кэшируем трансформ камеры.
			
			SceneManager.sceneLoaded += OnSceneLoaded;
			Init();
			
			instance = this;
			DontDestroyOnLoad(this);
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode){Init();}
		
		private void Init()
		{
			mainCamera = Camera.main;
			if (mainCamera == null)
			{
				Debug.LogError($"{nameof(FastShadowProjector)}: Ошибка камеры! Убедитесь что выставлен тег MainCamera");
				Destroy(gameObject);
				return;
			}
			mainCameraTransform = mainCamera.transform;
			
			shadowReceivers.Clear();
			shadowSources.Clear();
			
			InitProjector();
		}
		
		/// <summary> Инициализирует прожектор </summary>
		private void InitProjector()
		{
			shadowCutOffSqrDistance = shadowCutOffDistance * shadowCutOffDistance;	// пересчитываем квадрат расстояния.
			shadowFadeSqrDistance = shadowFadeDistance * shadowFadeDistance;
			
			projectorMaterialShadow = new Material(shader);
			RenderTexture renderTexture = new RenderTexture(shadowResolution, shadowResolution, 0,
				RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default)
			{
				useMipMap = false,
				anisoLevel = 0,
				filterMode = textureFilterMode
			};
			projectorMaterialShadow.SetTexture(idShadowTex, renderTexture);
			
			projectCamera.cullingMask = cullingMask;
			projectCamera.targetTexture = renderTexture;
		}

		/// <summary> Высчитывает весь объем теней и скрывает невидимые тени. </summary>
		private void CalculateShadowBounds() 
		{
			Vector2 min = Vector2.positiveInfinity;
			Vector2 max = Vector2.negativeInfinity;
			
			float maxShadowSize = 0.0f;
			int sourceInd = 0;
			
			Plane[] mainCameraPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

			Vector3 mainCamPos = mainCameraTransform.position;
			Bounds projBounds = new Bounds();

			for (int n = 0; n < shadowSources.Count; n++) {
				FastShadowSource fastShadowSource = shadowSources[n];
				fastShadowSource.SetVisible(false); // Сначала скрываем все источники теней.
				
				Vector3 shadowPos = fastShadowSource.GetShadowPos();

				float sqrDistance = (shadowPos - mainCamPos).sqrMagnitude; 
				if (sqrDistance > shadowCutOffSqrDistance ||
				    !GeometryUtility.TestPlanesAABB(mainCameraPlanes, fastShadowSource.GetBounds()) ||
				    Physics.Linecast(mainCamPos, shadowPos, raycastMask))
					continue;

				// Показываем только те, которые в габаритах проектора или не отсекаются по расстоянию или не рейкастятся.
				fastShadowSource.SetVisible(true);
				
				// Интенсивность (прозрачность) тени.
				float a = (shadowCutOffSqrDistance - sqrDistance) / (shadowCutOffSqrDistance - shadowFadeSqrDistance);
				fastShadowSource.SetIntensity(Mathf.Clamp01(a));

				if (sourceInd == 0)	// Если это первый показанный источник - ставим центр проецирования на него.
				{
					projBounds.center = shadowPos;
					projBounds.size = Vector3.zero;
				}
				else
					projBounds.Encapsulate(shadowPos);	// Если уже не первый - расширяемся до его границ.

				// Мировые координаты в плоскость проецирования, вычисляем габариты.
				Vector2 shadowViewCoords = projectCamera.WorldToViewportPoint(shadowPos);
				min = Vector2.Min(shadowViewCoords, min);
				max = Vector2.Max(shadowViewCoords, max);

				// Самая большая тень. Все границы будут браться исходя из ее размера.
				maxShadowSize = Mathf.Max(fastShadowSource.GetShadowSize(), maxShadowSize);

				sourceInd++;
			}
			
			if (sourceInd == 0)	// Если все тени вне видимости прерываем.
				return;
			
			// Размер самой большой тени относительно размера проекции камеры проецирования.
			float maxShadowSizeViewport = maxShadowSize / projectCamera.orthographicSize;
			
			// Смещаем камеру по линии проецирования, чтобы вместить все объекты.
			projectTransform.position = projBounds.center - Vector3.Scale(projBounds.extents, projectTransform.forward);
			
			// Вписываем все объекты в камеру.
			float maxRange = Mathf.Max(max.x - min.x + maxShadowSizeViewport , max.y - min.y + maxShadowSizeViewport);
			projectCamera.orthographicSize *= maxRange;
		}


		/// <summary> После рендера теней - скрываем все источники. </summary>
		private void OnPostRender(){HideAllSources();}

		/// <summary> Unity event function. Calls afer all Update() </summary>
		private void LateUpdate()
		{
			// Если нет активных источников теней или нет активных получаетелей - прерываем.
			if (shadowSources.Count == 0 || shadowReceivers.Count == 0)
			{
				projectCamera.enabled = false;
				return;
			}

			projectCamera.enabled = true;
			CalculateShadowBounds();

			// Матрицы проекции и обрезания для рендера принимаемой геометрии.
			float near = projectCamera.nearClipPlane;
			float far = projectCamera.farClipPlane;
			float size = projectCamera.orthographicSize * 2.0f;
			float invSize = 1 / size;
			
			Matrix4x4 projectorMatrix = new Matrix4x4(
				new Vector4(invSize, 0f, 0f, 0f),
				new Vector4(0f, invSize, 0f, 0f),
				new Vector4(0f, 0f, 1 / (near - far), 0f),
				new Vector4(0.5f, 0.5f, 0.5f, 1f));
			
			const float cNear = 0.1f;
			const float cFar = 100.0f;
			float cSize = cNear / size;

			Matrix4x4 projectorClipMatrix = new Matrix4x4(
				new Vector4(cSize, 0f, 0f, 0f),
				new Vector4(0f, cSize, 0f, 0f),
				new Vector4(0f, 0f, -0.5f, -1f),
				new Vector4(0.5f, 0.5f, cFar * cNear / cFar, 0f));
		
			Matrix4x4 viewMatrix = projectTransform.localToWorldMatrix.inverse;

		Render(projectorMatrix * viewMatrix,
				projectorClipMatrix * viewMatrix);
		}

		private float batchBreak;
		/// <summary> Рисует прозрачную геометрию получателей с тенями. </summary>
		private void Render(Matrix4x4 pViewMatrix, Matrix4x4 pViewClipMatrix)
		{
			//Vector3 camPos = mainCameraTransform.position;
			for (int i = 0; i < shadowReceivers.Count; i++) {
				FastShadowReceiver receiver = shadowReceivers[i];

				// Если расстояние до ближайшей точки на получателе больше отрисовываемого - идем к следующему. 
				/*Bounds bounds = receiver.GetMesh().bounds;
				Vector3 closestPoint = bounds.ClosestPoint(camPos);
				if ((camPos - closestPoint).sqrMagnitude > shadowCutOffSqrDistance)
					continue;*/
				
				Matrix4x4 modelMatrix = receiver.transform.localToWorldMatrix;
				
				// WP не поддерживает MaterialPropertyBlock, поэтому вот так.
	#if !UNITY_WP8
				MaterialPropertyBlock mpb = new MaterialPropertyBlock();
// Prevent dynamic batching.
#if UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4 || UNITY_5_5 || UNITY_5_6 || UNITY_5_7 || UNITY_2017 || UNITY_2018 || UNITY_2019 || UNITY_2020
				batchBreak++;
				batchBreak = Mathf.Clamp(batchBreak, 0.0f, float.MaxValue);	
				mpb.SetFloat(unity5BugFix, batchBreak);
#endif
				
				mpb.SetMatrix(idGlobalProjector, pViewMatrix * modelMatrix);
				mpb.SetMatrix(idGlobalProjectorClip, pViewClipMatrix * modelMatrix);

				Mesh mesh = receiver.GetMesh();
				for (int n = 0; n < mesh.subMeshCount; n++)
					Graphics.DrawMesh(mesh, modelMatrix, projectorMaterialShadow,0, null, n, mpb);
	#else
				material.SetMatrix(idGlobalProjector, bpv * modelMatrix);
				material.SetMatrix(idGlobalProjectorClip, bpvClip * modelMatrix);
						
				Graphics.DrawMesh(mesh, modelMatrix, material, 0);
	#endif
			}
		}
		/*/// <summary> Проверяет получатели теней. Если не инициализированы, то инициализирует. </summary>
		public static void CheckRecievers()
		{
			if (shadowReceivers == null)
				shadowReceivers = new List<FastShadowReceiver>();
		}
		/// <summary> Проверяет источники. Если не инициализированы, то инициализирует. </summary>
		public static void CheckSources()
		{
			if (shadowSources == null)
				shadowSources = new List<FastShadowSource>();
		}*/
		
		/// <summary> Скрывает все источники теней. </summary>
		private static void HideAllSources()
		{
			for (int i = 0; i < shadowSources.Count; i++)
				shadowSources[i].SetVisible(false);
		}

		public static void Add(MonoBehaviour item)
		{
			if (item.GetType() == typeof(FastShadowSource))
				shadowSources.Add((FastShadowSource)item);
			else
			{
				FastShadowReceiver receiver = (FastShadowReceiver) item; 
				if (shadowReceivers.Contains(receiver))
					return;
				shadowReceivers.Add(receiver);
			}
		}
		public static void Remove(MonoBehaviour item)
		{
			if (item.GetType() == typeof(FastShadowSource))
				shadowSources.Remove((FastShadowSource)item);
			else
				shadowReceivers.Remove((FastShadowReceiver)item);
		}
		
#if UNITY_EDITOR
		/// <summary> Обновлять размер текстуры проекции можно только в редакторе. </summary>
		public void Refresh()
		{
			InitProjector();
		}

		// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
		public static List<FastShadowSource> ShadowSources{get => shadowSources;}
		public static List<FastShadowReceiver> ShadowReceivers{get => shadowReceivers;}
		// ReSharper restore ConvertToAutoPropertyWithPrivateSetter

#endif
	}
}