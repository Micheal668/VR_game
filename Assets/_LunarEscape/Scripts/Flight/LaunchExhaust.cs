using System;
using UnityEngine;

namespace LunarEscape
{
    public sealed class LaunchExhaust : MonoBehaviour
    {
        [Serializable] public struct Jet
        {
            public Renderer flame;
            public Vector3 size;
            public float start, stop;
        }
        [SerializeField] private Jet[] jets;
        [SerializeField] private Renderer[] vapour;
        private MaterialPropertyBlock properties;
        public void Configure(Jet[] nozzles, Renderer[] steam) { jets = nozzles; vapour = steam; }

        public void Evaluate(float t)
        {
            properties ??= new MaterialPropertyBlock();
            for (int i=0; i<jets.Length; i++)
            {
                var jet = jets[i];
                float burn = LaunchCinematic.Ease(jet.start, jet.start + 1.2f, t)
                    * (1-LaunchCinematic.Ease(jet.stop-.45f, jet.stop, t));
                jet.flame.enabled = burn > .001f;
                if (!jet.flame.enabled) continue;
                float pulse = 1 + .055f * Mathf.Sin(t*20 + i*2);
                var scale = jet.size; scale.y *= pulse * burn;
                jet.flame.transform.localScale = scale;
                properties.SetFloat("_FilmTime", t + i);
                jet.flame.SetPropertyBlock(properties);
            }
            for (int i=0; i<vapour.Length; i++)
            {
                var puff = vapour[i];
                float age = t - (17.7f + i*.24f);
                puff.enabled = age > 0 && age < 9;
                if (!puff.enabled) continue;
                float angle = i * 2.39996f;
                float radius = 3 + age * (2.9f + i%4*.57f);
                puff.transform.localPosition = new Vector3(Mathf.Cos(angle)*radius, .7f + age*.72f, Mathf.Sin(angle)*radius);
                puff.transform.localScale = Vector3.one * (4 + age*(2.2f+i%3*.4f));
                properties.SetFloat("_Age", age); properties.SetFloat("_Seed", i*1.71f);
                puff.SetPropertyBlock(properties);
            }
        }
    }
}
