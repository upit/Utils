using UnityEngine;
using UnityEngine.UI;

namespace Utils.UI
{
    /// <summary>
    /// @Upit: Класс для рендера системы частиц в screenspace.
    /// Идея отсюда https://forum.unity.com/threads/free-script-particle-systems-in-ui-screen-space-overlay.406862/
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(ParticleSystem))]
    public class UIParticleSystem : MaskableGraphic
    {
        [SerializeField] private Texture particleTexture;
        [SerializeField] private Sprite particleSprite;
        
        private ParticleSystem partSystem;
        protected ParticleSystem.Particle[] particles;
        protected UIVertex[] quad;
        private bool alignAlongVelocity;
        protected int particlesCount;
        private bool enableAnim;
        private int[] currentSprite;
        private float[] timer; 

        public override Texture mainTexture
        {
            get { return particleSprite ? particleSprite.texture : particleTexture; }
        }

        protected virtual void Initialize()
        {
            partSystem = GetComponent<ParticleSystem>();

            ParticleSystemRenderer partRenderer = partSystem.GetComponent<ParticleSystemRenderer>();
            alignAlongVelocity = partRenderer.renderMode != ParticleSystemRenderMode.Billboard;
            partRenderer.sharedMaterial = null;
            partRenderer.enabled = false;

            particles = new ParticleSystem.Particle[partSystem.main.maxParticles];

            quad = new UIVertex[4];
            for (int i = 0; i < 4; i++)
                quad[i] = UIVertex.simpleVert;

            enableAnim = partSystem.textureSheetAnimation.enabled;
            if (enableAnim)
            {
                int count = particles.Length;
                currentSprite = new int[count];
                timer = new float[count];
            }
            else
            {
                UpdateQuadUV(particleSprite ? UnityEngine.Sprites.DataUtility.GetOuterUV(particleSprite) : new Vector4(0, 0, 1, 1));
            }
                
            SetAllDirty();
        }

        protected void UpdateQuadUV(Vector4 uv)
        {
            quad[0].uv0 = new Vector2(uv.x, uv.y);
            quad[1].uv0 = new Vector2(uv.x, uv.w);
            quad[2].uv0 = new Vector2(uv.z, uv.w);
            quad[3].uv0 = new Vector2(uv.z, uv.y);
        }

        // // TODO@Upit обновление UV по Grid.
        // protected void UpdateQuadUV(int index, int cols, int rows)
        // {
        //      
        //     int size = cols * rows;
        //     int x = index % cols;
        //     int y = index / rows;
        //     
        //     Debug.LogError(index + ": "+x+","+y);
        // }

        protected override void OnEnable()
        {
            base.OnEnable();
            Initialize();
        }

        /// <summary> Callback function when a UI element needs to generate vertices. </summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            const float halfPi = Mathf.PI / 2; 
            particlesCount = partSystem.GetParticles(particles);
            for (int i = 0; i < particlesCount; ++i)
            {
                ParticleSystem.Particle particle = particles[i];


                float rotation = -(alignAlongVelocity? Vector2.SignedAngle(particle.totalVelocity, Vector2.up)
                                     : particle.rotation) * Mathf.Deg2Rad;
                
                float rotation90 = rotation + halfPi;

                Vector3 size = particle.GetCurrentSize3D(partSystem);
                
                Vector2 width = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * size.x;
                Vector2 height = new Vector2(Mathf.Cos(rotation90), Mathf.Sin(rotation90)) * size.y;
                
                Vector2[] pos = new Vector2[4];
                pos[0] = (Vector2) particle.position - width * 0.5f - height * 0.5f;
                pos[1] = pos[0] + height;
                pos[2] = pos[1] + width;
                pos[3] = pos[0] + width;
                
                Color32 partColor = particle.GetCurrentColor(partSystem);
                var particleQuad = PrepareQuad(i, partColor, pos);
                vh.AddUIVertexQuad(particleQuad);
            }
        }

        protected virtual UIVertex[] PrepareQuad(int index, Color32 partColor, Vector2[] pos)
        {
            for (int i = 0; i < quad.Length; i++)
            {
                quad[i].color = partColor;
                quad[i].position = pos[i];
            }

            if (enableAnim)
                UpdateQuadUV(UnityEngine.Sprites.DataUtility.GetOuterUV(partSystem.textureSheetAnimation.GetSprite(currentSprite[index])));

            return quad;
        }

        private void Update()
        {
            SetVerticesDirty();

            if (!enableAnim)
                return;

            ParticleSystem.TextureSheetAnimationModule anim = partSystem.textureSheetAnimation;

            float step = Time.deltaTime;
            float initialTimer = 1.0f / anim.fps;
            int spriteCount = anim.spriteCount;

            for (int i = 0; i < particlesCount; i++)
            {
                timer[i] -= step;
                if (timer[i] <= 0.0f)
                {
                    timer[i] = initialTimer;
                    currentSprite[i]++;
                    if (currentSprite[i] >= spriteCount)
                        currentSprite[i] = 0;
                }
            }
        }
    }
}