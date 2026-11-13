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
        private UnityEngine.UI.Image placeholder;

        /// <summary>An optional creator thumbnail icon, shown only while its bound face is unavailable.</summary>
        public void SetPlaceholder(UnityEngine.UI.Image value)
        {
            placeholder = value;
            RefreshPlaceholder();
        }

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
            if (request == null || image == null) { RefreshPlaceholder(); return; }
            var texture = CharacterPortraits.Get(request);
            refreshAt = Time.unscaledTime + (texture == null ? .1f : .5f);
            if (texture == null || (texture is RenderTexture pending && !pending.IsCreated()))
            {
                if (image.texture == null || (image.texture is RenderTexture previous && !previous.IsCreated())) image.enabled = false;
                RefreshPlaceholder();
                return;
            }
            image.texture = texture;
            image.enabled = true;
            RefreshPlaceholder();
        }

        private void RefreshPlaceholder()
        {
            if (placeholder != null) placeholder.enabled = placeholder.sprite != null && (image == null || !image.enabled);
        }
    }
}
