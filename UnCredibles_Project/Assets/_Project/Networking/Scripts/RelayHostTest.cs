using UnityEngine;
namespace UnCredibles.Networking
{
    // Standalone UI; menus use the same connection component.
    public sealed class RelayHostTest : MonoBehaviour
    {
        private RelayConnection connection;
        private string code = "";
        private void Awake() => connection = gameObject.AddComponent<RelayConnection>();
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, Mathf.Min(520, Screen.width - 48), 510), GUI.skin.box);
            GUILayout.Label("Un-Credibles - Prueba de Relay");
            GUI.enabled = connection.CanConnect;
            if (GUILayout.Button("Crear Host", GUILayout.Height(40))) connection.CreateHost();
            code = GUILayout.TextField(code, 32);
            GUI.enabled = connection.CanConnect && !string.IsNullOrWhiteSpace(code);
            if (GUILayout.Button("Unirse", GUILayout.Height(40))) connection.JoinHost(code);
            GUI.enabled = true;
            GUILayout.Label(connection.Status);
            if (!string.IsNullOrEmpty(connection.JoinCode)) GUILayout.TextField(connection.JoinCode);
            if (connection.IsConnected)
            {
                foreach (ulong id in connection.Connections)
                    GUILayout.Label((id == 0 ? "Host" : $"Cliente {id}") + (id == connection.LocalClientId ? " (tu)" : ""));
                if (GUILayout.Button("Desconectar", GUILayout.Height(36))) connection.Disconnect();
            }
            GUILayout.EndArea();
        }
    }
}
