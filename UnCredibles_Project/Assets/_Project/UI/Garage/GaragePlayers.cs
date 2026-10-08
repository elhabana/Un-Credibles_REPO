using UnCredibles.UI.PartyLobby;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.UI.Garage
{
    public sealed class GaragePlayers : MonoBehaviour
    {
        public PartyLobbyController lobby;
        public GameObject[] avatars;
        private Renderer[][] avatarRenderers;
        private MaterialPropertyBlock colors;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void Awake()
        {
            colors = new MaterialPropertyBlock();
            avatarRenderers = new Renderer[avatars.Length][];
            var positions = new Vector3[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                positions[i] = avatars[i].transform.position;
                avatarRenderers[i] = avatars[i].GetComponentsInChildren<Renderer>(true);
            }
            // The lobby camera faces west: increasing Z runs from screen left to right.
            System.Array.Sort(positions, (a, b) => a.z.CompareTo(b.z));
            for (int i = 0; i < avatars.Length; i++) avatars[i].transform.position = positions[i];
        }
        private void OnEnable()
        {
            lobby.Initialized += Refresh;
            lobby.SlotsChanged += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            lobby.Initialized -= Refresh;
            lobby.SlotsChanged -= Refresh;
            foreach (var avatar in avatars)
                if (avatar != null) avatar.SetActive(false);
        }
        private void Refresh()
        {
            for (int i = 0; i < avatars.Length; i++)
            {
                bool occupied = lobby.Players != null && lobby.Slots[i].Occupied;
                avatars[i].SetActive(occupied);
                if (!occupied) continue;
                Color color = PlayerIdentity.ColorFor(i, lobby.Slots[i].IsAI);
                foreach (var renderer in avatarRenderers[i])
                {
                    // Tint each figure without changing the shared garage materials.
                    renderer.GetPropertyBlock(colors);
                    colors.SetColor(BaseColor, color);
                    colors.SetColor(ColorProperty, color);
                    renderer.SetPropertyBlock(colors);
                }
            }
        }
    }
}
