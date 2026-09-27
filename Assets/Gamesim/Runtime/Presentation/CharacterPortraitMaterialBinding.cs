using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Refreshes a memory-wall portrait without changing its eviction tint.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class CharacterPortraitMaterialBinding : MonoBehaviour
    {
        private Renderer target;
        private CharacterPortraits.PortraitRequest request;
        private Texture completed;
        private float refreshAt;
        public void Set(ContestantState contestant)
        {
            target = GetComponent<Renderer>();
            var next = CharacterPortraits.Prepare(contestant);
            if (next?.Key != request?.Key)
            {
                completed = null;
                var material = target.material;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            }
            request = next;
            enabled = true; Refresh();
        }
        private void Update()
        {
            if (Time.unscaledTime >= refreshAt) Refresh();
        }
        private void Refresh()
        {
            if (request == null || target == null) return;
            var texture = CharacterPortraits.Get(request);
            refreshAt = Time.unscaledTime + (texture == null ? .1f : .5f);
            if (texture == null || completed == texture) return;
            var material = target.material;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            completed = texture;
        }
    }
}
