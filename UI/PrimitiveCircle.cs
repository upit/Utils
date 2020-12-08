using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>  Класс для рисования окружностей и многоугольников в Unity UI.  </summary>
    [ExecuteAlways]
    public class PrimitiveCircle : Graphic
    {
        private const float DEG_TO_RAD = 0.01745329f;
        
        [Range(0, 1)]
        [SerializeField] private float fillAmount;
        [SerializeField] private bool fill = true;
        [SerializeField] private int thickness = 5;

        [Range(3, 360)]
        [SerializeField] private int segments = 360;
        
        [SerializeField] private Sprite sprite;
        private UIVertex[] vertex;

        
        public override Texture mainTexture
        {
            get
            {
                return sprite != null ? sprite.texture : s_WhiteTexture;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            vertex = new UIVertex[4];
            for (int i = 0; i < 4; i++)
                vertex[i].uv0 = sprite.uv[i];
        }

        protected override void OnPopulateMesh(VertexHelper vertHelper)
        {
            vertHelper.Clear();

            RectTransform rect = rectTransform;
            Vector2 size = rect.rect.size;
            float outer = rect.pivot.x * -Mathf.Min(size.x, size.y);
            float inner = outer + thickness;

            vertex[0].position = new Vector2(outer, 0);
            vertex[2].position = fill ? Vector2.zero : new Vector2(inner, 0);

            for (int i = 0; i < vertex.Length; i++)
                vertex[i].color = color;

            float degStep = 360f / segments * DEG_TO_RAD;
            float angle = 0.0f;

            int fillSegments = (int) ((segments + 1) * fillAmount);
            for (int i = 1; i < fillSegments; i++)
            {
                angle += degStep;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertex[1].position = new Vector2(outer * cos, outer * sin);

                if (fill)
                    vertex[2].position = vertex[3].position = Vector2.zero;
                else
                {
                    vertex[3].position = vertex[2].position;
                    vertex[2].position = new Vector2(inner * cos, inner * sin);
                }

                int vCount = vertHelper.currentVertCount;
                vertHelper.AddVert(vertex[0]);
                vertHelper.AddVert(vertex[1]);
                vertHelper.AddVert(vertex[2]);
                vertHelper.AddTriangle(vCount, vCount + 2, vCount + 1);

                if (!fill)
                {
                    vertHelper.AddVert(vertex[3]);
                    vertHelper.AddTriangle(vCount, vCount + 3, vCount + 2);
                }

                vertex[0].position = vertex[1].position;
            }
        }
    }
}