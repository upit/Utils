using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SimpleRectProgressBar : SimpleProgressBar
    {
        [SerializeField] private bool useColorInterpolation;
        [SerializeField] private Color emptyBarColor;
        private Color fullBarColor;
        private Image image;
        
        protected RectTransform rectTransform;

        private void Awake()
        {
            if (!useColorInterpolation)
                return;
            
            image = target.GetComponent<Image>();
            if (image == null)
            {
                useColorInterpolation = false;
                return;
            }
                
            fullBarColor = image.color;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            rectTransform = (RectTransform) target;
        }
        protected override void UpdateScale()
        {
            Vector2 max = rectTransform.anchorMax;
            max.x = Progress;
            rectTransform.anchorMax = max;

            if (useColorInterpolation)
                image.color = Color.Lerp(emptyBarColor, fullBarColor, Progress);
        }
    }

}