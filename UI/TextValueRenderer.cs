using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Text))]
    public class TextValueRenderer : MonoBehaviour
    {
        [SerializeField] private int multiplier = 100;
        [SerializeField] private int digits;
        [SerializeField] private string prefix;

        private bool isInit;
        private Text label;

        private void Init()
        {
            label = GetComponent<Text>();
            isInit = true;
        }

        public void SetValue(float value)
        {
            if (!isInit)
                Init();
            
            label.text = Math.Round(Mathf.Abs(value * multiplier), digits).ToString("N" + digits) + prefix;
        }
    }
}