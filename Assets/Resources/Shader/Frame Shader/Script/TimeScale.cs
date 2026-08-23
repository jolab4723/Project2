using UnityEngine;

namespace rdr.timescale
{
    public class TimeScale : MonoBehaviour
    {
        [SerializeField] private Material myMaterial; // Ссылка на материал

        void Update()
        {
            if (myMaterial != null) 
            {
                myMaterial.SetFloat("_UnscaledTime", Time.unscaledTime);
            }
        }
    }
}
