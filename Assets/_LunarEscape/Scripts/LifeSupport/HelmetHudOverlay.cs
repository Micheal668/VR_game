using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // Visor-projected information stays readable when a hand crosses it.
    // Clone each UI material; never alter the shared font or other world panels.
    public sealed class HelmetHudOverlay : MonoBehaviour
    {
        private readonly List<Material> owned=new();
        [SerializeField] private Shader imageShader, textShader;
        public void Configure(Shader image, Shader text) { imageShader=image;textShader=text; }
        private void Start()
        {
            var canvas=GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=100;
            foreach(var graphic in GetComponentsInChildren<Graphic>(true))
            {
                var source=graphic is TMP_Text text ? text.fontSharedMaterial : graphic.material;
                var material=new Material(source);material.name="Visor / "+source.name;
                material.shader=graphic is TMP_Text ? textShader : imageShader;
                material.renderQueue=4000;owned.Add(material);
                if(graphic is TMP_Text label) label.fontSharedMaterial=material;else graphic.material=material;
            }
        }
        private void OnDestroy() { foreach(var material in owned)if(material!=null)Destroy(material); }
    }
}
