using UnityEngine;

namespace UnCredibles.UI.Results
{
    // One podium place. Its height is where the figure stands, so taller art only needs the spot moved.
    public sealed class PodiumPedestal : MonoBehaviour
    {
        [SerializeField, Tooltip("Top of the pedestal, where the player's figure stands.")] private Transform figureSpot;

        public Transform FigureSpot => figureSpot;
    }
}
