using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Lights a shot brings with it. A conversation's two-shot brings a soft key (LIGHTING_AUDIT P2, Everyday
    /// Living: in conversation framing, dark hair and dark clothes were harder to pick out than the furniture):
    /// an unshadowed spot on the camera's side of the pair, aimed at their heads (<see cref="ConversationKeyLight"/>).
    /// It comes up once the camera has come in on the pair, follows them while the shot holds, and goes when the
    /// shot does - the conversation ending, a panel or a ceremony taking the camera - easing out and then
    /// destroyed; with the director disabled it goes at once. Presentation only: it reads the camera and the
    /// bodies and touches nothing of the season.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private Light conversationKey;
        private float conversationKeyWeight;

        /// <summary>The conversation's key light while it is up or easing out; null otherwise.</summary>
        public Light ConversationKey => conversationKey;

        /// <summary>Every frame, from the director's Update: the key follows the two-shot of the director's own conversation.</summary>
        private void TickConversationKey()
        {
            var view = cameraRig != null ? cameraRig.ViewCamera : null;
            bool held = cameraRig != null && cameraRig.HoldsTheTwoShot && focusedNpc != null && player != null && view != null;
            if (!held && conversationKey == null) { conversationKeyWeight = 0f; return; }
            conversationKeyWeight = ConversationKeyLight.Step(conversationKeyWeight, held, Time.unscaledDeltaTime,
                cameraRig != null && cameraRig.ReducedMotion);
            if (!held && conversationKeyWeight <= 0f) { EndConversationKey(); return; }
            if (conversationKey == null)
            {
                var lamp = new GameObject(ConversationKeyLight.Name);
                lamp.transform.SetParent(transform, false);
                conversationKey = lamp.AddComponent<Light>();
                conversationKey.type = LightType.Spot;
                conversationKey.color = new Color(ConversationKeyLight.Red, ConversationKeyLight.Green, ConversationKeyLight.Blue);
                conversationKey.shadows = LightShadows.None;
                conversationKey.intensity = 0f;
            }
            // While the shot holds the key follows the pair and the camera; easing out, it stays where the shot left it.
            if (held)
            {
                var placed = ConversationKeyLight.Place(Numeric(ConversationKeyHead(player.transform)),
                    Numeric(ConversationKeyHead(focusedNpc.transform)), Numeric(view.transform.position));
                var at = new Vector3(placed.Position.X, placed.Position.Y, placed.Position.Z);
                var forward = new Vector3(placed.Forward.X, placed.Forward.Y, placed.Forward.Z);
                conversationKey.transform.SetPositionAndRotation(at, Quaternion.LookRotation(forward));
                conversationKey.spotAngle = placed.SpotAngle;
                conversationKey.innerSpotAngle = placed.InnerSpotAngle;
                conversationKey.range = placed.Range;
            }
            conversationKey.intensity = ConversationKeyLight.IntensityAt(conversationKeyWeight);
        }

        /// <summary>Takes the key down at once: on the director's disable, and once it has eased out.</summary>
        private void EndConversationKey()
        {
            conversationKeyWeight = 0f;
            if (conversationKey == null) return;
            conversationKey.enabled = false;
            Destroy(conversationKey.gameObject);
            conversationKey = null;
        }

        /// <summary>Where a body's head is, as the two-shot frames it: the head bone, the stand-in's head, a seat's focus, or head height over the feet.</summary>
        private static Vector3 ConversationKeyHead(Transform subject)
        {
            var presentation = subject.GetComponent<CharacterPresentation>();
            if (presentation != null)
            {
                if (presentation.HeadBone != null) return presentation.HeadBone.position;
                var root = presentation.VisualRoot;
                var primitiveHead = root != null ? root.Find("Chest/Head pivot") : null;
                if (primitiveHead != null) return primitiveHead.position;
            }
            var seat = subject.GetComponent<HouseSeatPresentation>();
            if (seat != null && seat.Active) return seat.VisualFocus;
            return subject.position + Vector3.up * 1.7f;
        }

        private static System.Numerics.Vector3 Numeric(Vector3 value) => new System.Numerics.Vector3(value.x, value.y, value.z);
    }
}
