using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Physics
{
    public class CrashedBodiesHandler
    {
        private const float DISAPPEAR_SPEED = 1f; // Скорость исчезновения.
        private const float SQR_DISAPPEAR_DISTANCE = 9.0f; // Квадрат расстояния после которого объект исчез под землей.
        private const float FORCE_DISAPPEAR_AFTER = 3.0f; // Принудительное исчезновение, если кусок не "засыпает".

        #region Crashed Bodies

        private class CrashedBody
        {
            private readonly Rigidbody rigidbody;
            private readonly Collider[] colliders;
            private readonly Transform cachedTransform;
            private readonly Vector3 initialLocalPosition;
            private readonly Quaternion initialLocalRotation;

            public CrashedBody(Rigidbody body)
            {
                rigidbody = body;
                colliders = rigidbody.GetComponentsInChildren<Collider>(true);
                cachedTransform = rigidbody.transform;
                initialLocalPosition = cachedTransform.localPosition;
                initialLocalRotation = cachedTransform.localRotation;
                
                if (body.collisionDetectionMode != CollisionDetectionMode.Discrete)
                    Debug.LogWarning(body.name + " CollisionDetectionMode != Discrete. Possible big impact on physics performance.", body);
            }

            public void Reset()
            {
                rigidbody.velocity = rigidbody.angularVelocity = Vector3.zero;
                cachedTransform.localPosition = initialLocalPosition;
                cachedTransform.localRotation = initialLocalRotation;
            }

            public void Explode(Vector3 pos, float force, float radius)
            {
                EnableColliders(true);
                Show(true);
                rigidbody.AddExplosionForce(force, pos, radius, 1.0f);
                rigidbody.AddTorque(Random.insideUnitSphere, ForceMode.VelocityChange);
            }

            public bool IsSleeping()
            {
                return rigidbody.IsSleeping();
            }

            public void EnableColliders(bool enable)
            {
                rigidbody.isKinematic = !enable;
                rigidbody.detectCollisions = enable;
                // for (int i = 0; i < colliders.Length; i++)
                //     colliders[i].enabled = enable;
            }

            public void Move(Vector3 dir)
            {
                rigidbody.MovePosition(rigidbody.position + dir);
            }

            public Vector3 GetPos()
            {
                return rigidbody.position;
            }

            public void Show(bool show)
            {
                rigidbody.gameObject.SetActive(show);
            }

            public void Destroy()
            {
                Object.Destroy(rigidbody.gameObject);
            }
        }

        #endregion

        private readonly CrashedBody[] crashedBodies;
        private readonly bool hasCrashedBodies;
        private readonly Vector3 disappearDir;
        private CompositeDisposable disposables;

        public CrashedBodiesHandler(Rigidbody[] rigidBodies)
        {
            if (rigidBodies != null)
            {
                hasCrashedBodies = true;
                crashedBodies = new CrashedBody[rigidBodies.Length];
                for (int i = 0; i < crashedBodies.Length; i++)
                    crashedBodies[i] = new CrashedBody(rigidBodies[i]);
            }

            disappearDir = new Vector3(0.0f, -DISAPPEAR_SPEED, 0.0f);
        }

        ~CrashedBodiesHandler()
        {
            disposables?.Dispose();
        }

        public void Explode(Vector3 pos, float force, float radius)
        {
            if (!hasCrashedBodies)
                return;

            for (int i = 0; i < crashedBodies.Length; i++)
            {
                crashedBodies[i].Reset();
                crashedBodies[i].Explode(pos, force, radius);
            }
        }

        public void Disappear(bool destroy = false)
        {
            if (!hasCrashedBodies)
                return;

            // Capacity = кол-во crashed body + 1 на HandleDisappearing.
            if (disposables == null)
                disposables = new CompositeDisposable(crashedBodies.Length + 1);
            else
                disposables.Clear();

            Observable.FromCoroutine(_ => HandleDisappearing(destroy)).Subscribe().AddTo(disposables);
        }

        private IEnumerator HandleDisappearing(bool destroy)
        {
            yield return new WaitForSeconds(3.0f);
            
            float startTime = Time.time;
            var bodies = new List<CrashedBody>(crashedBodies);
            while (bodies.Count > 0)
            {
                bool forceDisappear = Time.time - startTime > FORCE_DISAPPEAR_AFTER;
                for (int i = 0; i < bodies.Count; i++)
                {
                    if (forceDisappear || bodies[i].IsSleeping())
                    {
                        bodies[i].EnableColliders(false);
                        var index = i;
                        Observable.FromCoroutine(() => MoveFragment(bodies[index], destroy)).Subscribe()
                            .AddTo(disposables);
                        bodies.RemoveAt(i);
                        i--;
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator MoveFragment(CrashedBody body, bool destroy)
        {
            Vector3 startPos = body.GetPos();
            while ((body.GetPos() - startPos).sqrMagnitude < SQR_DISAPPEAR_DISTANCE)
            {
                body.Move(disappearDir * Time.deltaTime);
                yield return new WaitForEndOfFrame();
            }

            if (destroy)
                body.Destroy();
            else
                body.Show(false);
        }
    }
}