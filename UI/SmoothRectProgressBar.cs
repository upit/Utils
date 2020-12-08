using UnityEngine;

namespace UI
{
    public class SmoothRectProgressBar : SimpleRectProgressBar
    {
        [SerializeField] private float transitionSpeed = 5.0f;
        [SerializeField] private GameObject[] finishEffects;    // Эффекты показываются после завершения анимации.
        
        private float animProgress;

        private void Awake()
        {
            enabled = false;
        }

        private void Update()
        {
            if (animProgress >= Progress)
            {
                if (!Mathf.Approximately(Progress,0.0f))
                    ShowEffect(true);
                enabled = false;
                animProgress = Progress;
            }

            Vector2 max = rectTransform.anchorMax;
            max.x = animProgress;
            rectTransform.anchorMax = max;

            animProgress += (Progress - animProgress) * transitionSpeed * Time.deltaTime;
        }

        protected override void UpdateScale()
        {
            bool visible = Progress > 0.0f;
            target.gameObject.SetActive(visible);
            
            if (!visible)
                return;
            
            animProgress = 0.0f;
            ShowEffect(false);
            enabled = true;
        }
        
        private void ShowEffect(bool show)
        {
            for (int i = 0; i < finishEffects.Length; i++)
                finishEffects[i].SetActive(show);
        }
        
    }

}