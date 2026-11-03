using UnCredibles.Players;
using UnCredibles.UI.Garage;
using UnityEngine;

namespace UnCredibles.Trailer
{
    // Previs shots 01-02 in SCN_MainMenu: the garage door rises with light pouring in, then the
    // four heroes run out into it. Replaces the menu (UI, camera rig, lobby) while it plays.
    public sealed class TrailerGarage : MonoBehaviour
    {
        private const float DoorRise = 4.2f;
        private const float FigureX = -2.2f;
        private const float RunSpeed = 2.6f;
        private static readonly float[] FigureZ = { .19f, -.91f, -2.01f, -3.11f };
        private static readonly float[] RunDelay = { .05f, 0f, .12f, .2f };

        public float doorOpenStart = .6f, doorOpenEnd = 3.8f;
        public float runStart = 4f;

        private Transform[] door;
        private Vector3[] doorStarts;
        private Transform[] figures;
        private PlayerCharacterAnimator[] characters;
        private Transform group;
        private TrailerShots shots;

        private void Start()
        {
            var output = Camera.main;
            var rig = FindFirstObjectByType<GarageCameraRig>(FindObjectsInactive.Include);
            if (rig != null) rig.enabled = false;
            var players = FindFirstObjectByType<GaragePlayers>(FindObjectsInactive.Include);
            door = players.doorPieces;
            doorStarts = new Vector3[door.Length];
            for (int i = 0; i < door.Length; i++) doorStarts[i] = door[i].localPosition;

            figures = new Transform[FigureZ.Length];
            characters = new PlayerCharacterAnimator[FigureZ.Length];
            for (int i = 0; i < FigureZ.Length; i++)
            {
                var figure = Instantiate(players.figures.For(i));
                figure.name = "Trailer hero " + i;
                figure.transform.SetPositionAndRotation(new Vector3(FigureX, 0f, FigureZ[i]), Quaternion.Euler(0f, 270f, 0f));
                figures[i] = figure.transform;
                characters[i] = figure.GetComponentInChildren<PlayerCharacterAnimator>();
                characters[i].SetRunPlaybackSpeed(1.1f);
            }
            group = new GameObject("Trailer heroes centre").transform;
            group.position = new Vector3(FigureX, 1f, -1.46f);

            StageDaylight();
            TrailerLook.Apply(output, .1f, .9f);
            BuildShots(output);
        }

        // The garage model is walled behind the door, so the bright exterior is a glowing panel filling the
        // doorway just behind the door (the heroes vanish into it), plus a warm light flooding in.
        private static void StageDaylight()
        {
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Trailer daylight backdrop";
            Destroy(backdrop.GetComponent<Collider>());
            backdrop.transform.SetPositionAndRotation(new Vector3(-5.14f, 1.58f, -1.46f), Quaternion.Euler(0f, -90f, 0f));
            backdrop.transform.localScale = new Vector3(4.15f, 3.16f, 1f);
            var glow = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glow.SetColor("_BaseColor", Color.white);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, .97f, .9f) * 6f);
            backdrop.GetComponent<Renderer>().sharedMaterial = glow;

            var sun = new GameObject("Trailer daylight").AddComponent<Light>();
            sun.type = LightType.Spot;
            sun.transform.SetPositionAndRotation(new Vector3(-5f, 2.9f, -1.46f), Quaternion.Euler(28f, 90f, 0f));
            sun.range = 16f;
            sun.spotAngle = 110f;
            sun.innerSpotAngle = 50f;
            sun.intensity = 25f;
            sun.color = new Color(1f, .93f, .8f);
            sun.shadows = LightShadows.Soft;
        }

        private void BuildShots(Camera output)
        {
            shots = TrailerShots.Create(output);
            // 01 - Detail: the door starts to rise, seen from inside behind the waiting heroes.
            shots.Add(new TrailerShot
            {
                Name = "01 Garage door", Duration = 4f, Fov = 50f, ToFov = 46f,
                FromPosition = new Vector3(1.6f, 1.05f, -1.46f), ToPosition = new Vector3(1.1f, 1.05f, -1.46f),
                FromEuler = new Vector3(-7f, 270f, 0f), ToEuler = new Vector3(-8f, 270f, 0f)
            });
            // 02 - Wide tracking: the four heroes run out into the light.
            shots.Add(new TrailerShot
            {
                Name = "02 Heroes run out", Duration = 2f, Fov = 56f,
                Follow = group, FromPosition = new Vector3(3.8f, .7f, 1.9f), ToPosition = new Vector3(3.2f, .5f, 1.5f),
                LookAt = group, LookAtOffset = new Vector3(-2.5f, -.2f, 0f)
            });
        }

        private void Update()
        {
            float t = shots != null ? shots.Elapsed : 0f;
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(doorOpenStart, doorOpenEnd, t));
            for (int i = 0; i < door.Length; i++)
                door[i].localPosition = doorStarts[i] + Vector3.up * (open * DoorRise);

            for (int i = 0; i < figures.Length; i++)
            {
                float run = t - runStart - RunDelay[i];
                characters[i].SetRunning(run > 0f);
                figures[i].position = new Vector3(FigureX - Mathf.Max(0f, run) * RunSpeed, 0f, FigureZ[i]);
            }
            float groupRun = Mathf.Max(0f, t - runStart);
            // The camera rides with the group but stops short of the doorway.
            group.position = new Vector3(Mathf.Max(-4.3f, FigureX - groupRun * RunSpeed), 1f, -1.46f);
        }
    }
}
