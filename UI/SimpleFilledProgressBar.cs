using UnityEngine.UI;

namespace UI
{
    public class SimpleFilledProgressBar : SimpleProgressBar
    {
        private Image image;
        protected override void OnEnable()
        {
            base.OnEnable();
            image = target.GetComponent<Image>();
        }
        protected override void UpdateScale()
        {
            image.fillAmount = Progress;
        }
    }

}