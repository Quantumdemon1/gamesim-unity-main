using UMA.PoseTools;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>Default pose library for the two game-supported UMA 3 human rigs.</summary>
    public sealed class UmaFaceRigDefaults : ScriptableObject
    {
        [SerializeField] private UMAExpressionSet expressions;
        public UMAExpressionSet Expressions => expressions;
    }
}
