using UnityEngine;

namespace UI
{
    public interface IProgressBar
    {
        float Progress { get; set; }
    }
    [ExecuteAlways]
    public class SimpleProgressBar : MonoBehaviour, IProgressBar
    {
        [SerializeField] protected Transform target;
        private float progress = 1.0f;
        public float Progress
        {
            get => progress;
            set
            {
                progress = Mathf.Clamp01(value);
                UpdateScale();
            }
        }

        protected virtual void OnEnable()
        {
            if (target == null)
                target = transform;
        }

        protected virtual void UpdateScale()
        {
            Vector3 scale = target.localScale;
            scale.x = progress;
            target.localScale = scale;
        }
    }

}