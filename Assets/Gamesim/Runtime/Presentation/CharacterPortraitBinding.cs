using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>One-shot panels can bind before UMA has completed their queued portrait.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CharacterPortraitBinding : MonoBehaviour
    {
        private RawImage image;
        private CharacterPortraits.PortraitRequest request;
        private float refreshAt;

        public void Set(ContestantState contestant)
        {
            image = GetComponent<RawImage>();
            var next = CharacterPortraits.Prepare(contestant);
            if (next?.Key != request?.Key) image.texture = null;
            request = next;
            image.enabled = image.texture != null;
            enabled = true;
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime >= refreshAt) Refresh();
        }

        private void Refresh()
        {
            if (request == null || image == null) return;
            var texture = CharacterPortraits.Get(request);
            refreshAt = Time.unscaledTime + (texture == null ? .1f : .5f);
            if (texture == null)
            {
                if (image.texture == null || (image.texture is RenderTexture previous && !previous.IsCreated())) image.enabled = false;
                return;
            }
            image.texture = texture;
            image.enabled = true;
        }
    }
}
