using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Confetti thrown across a card: two bursts from the lower corners, as the reference throws them
    /// for its winner (<c>JuryVoteReveal.tsx</c>: angles 60 and 120, gold, orange and tomato), falling
    /// and tumbling for three seconds and then gone.
    ///
    /// <para>Seeded, so the same season throws the same confetti, and on the wall clock, so a sped-up
    /// reveal does not hurry it. Nothing here takes a raycast. Reduced motion never throws it: the
    /// caller does not ask.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConfettiBurst : MonoBehaviour
    {
        /// <summary>How long a throw lasts, in real seconds: the reference's three.</summary>
        public const float Seconds = 3f;

        /// <summary>How many pieces a throw has.</summary>
        public const int Pieces = 120;

        /// <summary>The reference's winner colours, and the house's accent.</summary>
        private static readonly Color[] Colours =
        {
            new Color(1f, 0.843f, 0f), new Color(1f, 0.647f, 0f), new Color(1f, 0.388f, 0.278f), UiTheme.Accent,
        };

        private struct Piece
        {
            public RectTransform rect;
            public Vector2 velocity;
            public float spin, delay;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private float age = -1f;

        /// <summary>Whether a throw is in the air.</summary>
        public bool Throwing => age >= 0f && age < Seconds;

        /// <summary>How many pieces are in the air now.</summary>
        public int PiecesInFlight => Throwing ? pieces.Count : 0;

        /// <summary>A layer for confetti, stretched over <paramref name="parent"/>.</summary>
        public static ConfettiBurst Attach(RectTransform parent)
        {
            var layer = new GameObject("Confetti", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero; layer.offsetMax = Vector2.zero;
            return layer.gameObject.AddComponent<ConfettiBurst>();
        }

        /// <summary>Throws a burst from the lower corners of the layer, the same one every time for the same seed.</summary>
        public void Throw(int seed)
        {
            Clear();
            var random = new System.Random(seed);
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0f || size.y <= 0f) size = new Vector2(1600f, 900f);
            for (int i = 0; i < Pieces; i++)
            {
                bool left = i % 2 == 0;
                // Through the themed fill: a bare-coloured Image with no sprite draws nothing here.
                var rect = HudPrimitives.Fill("Piece", transform, Colours[random.Next(Colours.Length)], 2);
                rect.sizeDelta = new Vector2(8f + (float)random.NextDouble() * 6f, 12f + (float)random.NextDouble() * 8f);
                rect.anchorMin = rect.anchorMax = new Vector2(left ? 0f : 1f, 0f);
                rect.anchoredPosition = Vector2.zero;
                // 60 degrees from the left corner, 120 from the right, with the reference's 55-degree spread.
                float angle = (left ? 60f : 120f) + ((float)random.NextDouble() - 0.5f) * 55f;
                float speed = size.y * (0.95f + (float)random.NextDouble() * 0.55f);
                pieces.Add(new Piece
                {
                    rect = rect,
                    velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed,
                    spin = ((float)random.NextDouble() - 0.5f) * 720f,
                    // The reference fires every frame for its three seconds; spreading the pieces over
                    // the first second reads the same without a thousand of them.
                    delay = (float)random.NextDouble(),
                });
                rect.gameObject.SetActive(false);
            }
            age = 0f;
        }

        public void Clear()
        {
            foreach (var piece in pieces) if (piece.rect != null) Destroy(piece.rect.gameObject);
            pieces.Clear();
            age = -1f;
        }

        private void Update()
        {
            if (age < 0f) return;
            age += Time.unscaledDeltaTime;
            if (age >= Seconds) { Clear(); return; }
            float gravity = ((RectTransform)transform).rect.height * 1.1f;
            foreach (var piece in pieces)
            {
                if (piece.rect == null) continue;
                float t = age - piece.delay;
                if (t < 0f) { piece.rect.gameObject.SetActive(false); continue; }
                piece.rect.gameObject.SetActive(true);
                piece.rect.anchoredPosition = piece.velocity * t + 0.5f * gravity * t * t * Vector2.down;
                piece.rect.localRotation = Quaternion.Euler(0f, 0f, piece.spin * t);
            }
        }

        private void OnDisable() => Clear();
    }
}
