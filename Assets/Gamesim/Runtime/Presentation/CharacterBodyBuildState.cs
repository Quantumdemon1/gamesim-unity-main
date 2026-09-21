using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Present on deferred providers. Ready includes the post-build DNA/color pass.</summary>
    public sealed class CharacterBodyBuildState : MonoBehaviour
    {
        public int Revision { get; set; }
        public bool Ready { get; set; }
        public string Substitution { get; set; }
    }
}
