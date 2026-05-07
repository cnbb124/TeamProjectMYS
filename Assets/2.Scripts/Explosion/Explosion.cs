using UnityEngine;
using System.Collections;

namespace Imphenzia.SpaceForUnity
{
    public class Explosion : MonoBehaviour
    {
        [Tooltip("Destroy the game object after this amount of seconds.")]
        public float destroyAfterSeconds = 8.0f;

        void Awake()
        {
            // Destroy gameobject after delay
            Destroy(gameObject, destroyAfterSeconds);
        }
    }
}
