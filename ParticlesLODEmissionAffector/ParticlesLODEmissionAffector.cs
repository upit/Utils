using UnityEngine;

namespace Util.FX
{
    /// <summary> @Upit Класс для плавного прекращения/возобновления генерации частиц при смене LOD. </summary>
    public class ParticlesLODEmissionAffector : MonoBehaviour
    {
        private ParticleSystem.EmissionModule[] emmisions;

        private void Awake()
        {
            var pSystem = GetComponentsInChildren<ParticleSystem>();
            emmisions = new ParticleSystem.EmissionModule[pSystem.Length];
            for (int i = 0; i < pSystem.Length; i++)
                emmisions[i] = pSystem[i].emission;
        }

        private void OnBecameInvisible()
        {
            EnableEmmision(false);
        }

        private void OnBecameVisible()
        {
            EnableEmmision(true);
        }

        private void EnableEmmision(bool active)
        {
            for (int i = 0; i < emmisions.Length; i++)
                emmisions[i].enabled = active;
        }
    }
}