using UnCredibles.Players;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.Trailer
{
    // Previs shot 01 in SCN_MainMenu, one continuous take: from behind the waiting heroes the garage door
    // rises with daylight pouring in, they run out into it while the camera pushes in after them, and the
    // light floods the frame to white, which hands over to the white title card.
    public sealed class TrailerGarage : MonoBehaviour
    {
        private const float DoorRise = 4.2f;
        private const float FigureX = -2.2f;
        private const float RunSpeed = 2.8f;
        private static readonly float[] FigureZ = { .19f, -.91f, -2.01f, -3.11f };
        private static readonly float[] RunDelay = { .05f, 0f, .12f, .2f };

        public float duration = 6.5f;
        public float doorOpenStart = .5f, doorOpenEnd = 3.1f;
        public float runStart = 2.9f;
        public float whiteStart = 5.4f;

        private Transform[] door;
        private Vector3[] doorStarts;
        private Transform[] figures;
        private PlayerCharacterAnimator[] characters;
        private TrailerShots shots;
        private Transform group;
        private Image white;

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
                characters[i].SetRunPlaybackSpeed(1.15f);
            }

            StageDaylight();
            var look = TrailerLook.Apply(output, .1f, .9f);
            look.keepCanvases.Add("Trailer white");
            white = WhiteOverlay();
            BuildShot(output);
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

        private static Image WhiteOverlay()
        {
            var canvas = new GameObject("Trailer white", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var image = new GameObject("White", typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(canvas.transform, false);
            image.color = new Color(1f, 1f, 1f, 0f);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
        }

        // One slow push-in from behind the heroes towards the doorway, opening up as they run out.
        private void BuildShot(Camera output)
        {
            shots = TrailerShots.Create(output);
            group = new GameObject("Trailer heroes centre").transform;
            group.position = new Vector3(FigureX, 0f, -1.46f);
            shots.Add(new TrailerShot
            {
                Name = "01 Garage door and run out", Duration = duration, Fov = 44f, ToFov = 52f,
                Follow = group, FromPosition = new Vector3(4.1f, 1.0f, 0f), ToPosition = new Vector3(2.9f, 1.2f, 0f),
                FromEuler = new Vector3(-6f, 270f, 0f), ToEuler = new Vector3(-3f, 270f, 0f)
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
            // The camera rides behind the running heroes and stops short of the doorway.
            float groupRun = Mathf.Max(0f, t - runStart);
            group.position = new Vector3(Mathf.Max(-4.9f, FigureX - groupRun * RunSpeed * .85f), 0f, -1.46f);
            if (white != null)
                white.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(whiteStart, duration - .05f, t)));
        }
    }
}
