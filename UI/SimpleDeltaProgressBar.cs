using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Класс для плавного движения прогресс баров. После задания значений выполняется Update, анимируя полоски.
    /// При достижения разницы параметра меньше чем число знаков после запятой (1-0.1, 2-0.01, 3-0.001 и т.д.) анимация
    /// завершается и Update перестает дергаться. Дополнительно есть возможность показать эффект после завершения анимации. 
    /// </summary>
    public class SimpleDeltaProgressBar : SimpleRectProgressBar
    {
        [SerializeField] private RectTransform positiveRect;
        [SerializeField] private RectTransform negativeRect;
        [SerializeField] private Text lblDelta;
        [SerializeField] private Text lblValue;
        
        [SerializeField] private Color lblPositiveColor;
        [SerializeField] private Color lblNegativeColor;
        [SerializeField] private int decimalDigits;
        [SerializeField] private string valueSuffix;
        [SerializeField] private float transitionSpeed = 5.0f;
        [SerializeField] private GameObject[] finishEffects;    // Эффекты показываются после завершения анимации.
        
        private RectTransform activeRect;
        private Image activeImage;
        private Color activeColor;
        private string signValue;

        private float deltaSize;
        private float deltaValue;

        private float targetDeltaValue;
        private float targetDeltaSize;

        private float minDelta;    // Дельта, после которой останавливать анимацию. 

        private void Awake()
        {
            OnEnable();
            enabled = false;
        }

        private void Update()
        {
            if (Mathf.Abs(targetDeltaValue - deltaValue) < minDelta)
                Finish();

            float step = transitionSpeed * Time.deltaTime;
            deltaValue += (targetDeltaValue - deltaValue) * step;
            lblDelta.text = signValue + Math.Round(Mathf.Abs(deltaValue), decimalDigits).ToString("N" + decimalDigits);

            activeColor.a += (1.0f - activeColor.a) * step;
            activeImage.color = activeColor;
            
            float x1 = Progress;
            deltaSize += (targetDeltaSize - deltaSize) * step;
            float x2 = x1 + deltaSize;
            
            Vector2 rectMin = activeRect.anchorMin;
            Vector2 rectMax = activeRect.anchorMax;
            rectMin.x = Mathf.Min(x1, x2);
            rectMax.x = Mathf.Max(x1, x2);
            activeRect.anchorMin = rectMin;
            activeRect.anchorMax = rectMax;
        }

        public void SetValues(float primaryValue, float secondaryValue, float primaryMax, float secondaryMax)
        {
            lblValue.text = Math.Round(secondaryValue, decimalDigits) + valueSuffix;

            targetDeltaValue = secondaryValue - primaryValue;
            bool hasDelta = !Mathf.Approximately(targetDeltaValue, 0.0f);
            lblDelta.gameObject.SetActive(hasDelta);
            positiveRect.gameObject.SetActive(false);
            negativeRect.gameObject.SetActive(false);

            if (!hasDelta)
                return;

            bool positive = targetDeltaValue > 0;
            lblDelta.color = positive ? lblPositiveColor : lblNegativeColor;
            signValue = positive ? "+ " : "- ";
            
            activeRect = positive ? positiveRect : negativeRect;
            activeRect.gameObject.SetActive(true);
            activeImage = activeRect.GetComponent<Image>();
            activeColor = activeImage.color;
            
            float maxValue = Mathf.Max(primaryMax, secondaryMax); 
            Progress = Mathf.Approximately(maxValue, 0)? 0 : primaryValue / maxValue;
            
            targetDeltaSize = targetDeltaValue / maxValue;
            minDelta = (float) (1.0 / Math.Pow(10, decimalDigits)); // Минимальное значение после запятой.
            
            Begin();
        }

        private void Begin()
        {
            activeColor.a = deltaValue = deltaSize = 0.0f;
            activeImage.color = activeColor;
            ShowEffect(false);
            enabled = true;
        }
        
        private void Finish()
        {
            activeColor.a = 1.0f;
            deltaValue = targetDeltaValue;
            deltaSize = targetDeltaSize;
            ShowEffect(true);
            enabled = false;
        }

        private void ShowEffect(bool show)
        {
            if (finishEffects == null)
                return;

            for (int i = 0; i < finishEffects.Length; i++)
                finishEffects[i].SetActive(show);
        }

    }
}