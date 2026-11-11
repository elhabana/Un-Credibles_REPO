using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Pixel-art gags on the ground of the TV settings screen: the hero in one of the player colours does a
    // minigame chore (mowing the lawn, walking grandma across, sweeping out the garage mice, jumping the
    // spinning churro) and messes it up, each with two possible endings. Gags come in random order, and the
    // hero who messes up is never the same colour twice in a row. Drawn at a choppy 12 fps into a small
    // point-filtered texture, one texel per scene pixel; row 0 is the top of the grass.
    public sealed class TvHeroGags
    {
        private const int Width = 170;
        private const int Height = 30;
        private const float Fps = 12f;
        private const float GagSeconds = 6.5f;
        private const int GagCount = 4;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Ink = new Color32(41, 18, 8, 255);
        private static readonly Color32 Skin = new Color32(255, 204, 153, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 HairBrown = new Color32(80, 45, 20, 255);
        private static readonly Color32 GreyHair = new Color32(205, 205, 215, 255);
        private static readonly Color32 Dress = new Color32(140, 77, 153, 255);
        private static readonly Color32 Glass = new Color32(170, 225, 255, 255);
        private static readonly Color32 Red = new Color32(220, 50, 40, 255);
        private static readonly Color32 Gold = new Color32(250, 205, 60, 255);
        private static readonly Color32 Brown = new Color32(125, 75, 30, 255);
        private static readonly Color32 Grey = new Color32(150, 150, 160, 255);
        private static readonly Color32 Smoke = new Color32(215, 215, 220, 255);
        private static readonly Color32 Soot = new Color32(55, 55, 60, 255);
        private static readonly Color32 Pink = new Color32(245, 150, 170, 255);
        private static readonly Color32 Leaf = new Color32(60, 170, 50, 255);
        private static readonly Color32 DarkLeaf = new Color32(35, 120, 35, 255);
        private static readonly Color32 Fry = new Color32(225, 145, 50, 255);
        private static readonly Color32[] CarColours =
        {
            new Color32(240, 240, 240, 255), new Color32(60, 60, 70, 255), new Color32(230, 120, 30, 255),
        };

        // The playable hero in the HeroCostume, facing right: hair (H), mask (D) with white eyes, cape (D)
        // behind, gold emblem and belt (Y), white gloves, dark boots. C = suit colour, D = its dark shade.
        private const int HeroW = 11;
        private static readonly string[] HeroStand =
        {
            "....KKKK...",
            "...KHHHHK..",
            "...KDWDWK..",
            "...KSSSSK..",
            "..DDKSSK...",
            ".DDKCCCCK..",
            ".DKCCYYCCK.",
            "DDKCCYYCCK.",
            "DDKWCCCCWK.",
            "DD.KYYYYK..",
            "D..KCCCCK..",
            "D..KCKKCK..",
            "...KCKKCK..",
            "...KDKKDK..",
            "...KDKKDDK.",
            "...KKKKKKK.",
        };
        private static readonly string[] HeroWalk =
        {
            "....KKKK...",
            "...KHHHHK..",
            "...KDWDWK..",
            "...KSSSSK..",
            "..DDKSSK...",
            "DDDKCCCCK..",
            "DDKCCYYCCK.",
            "D.KCCYYCCK.",
            "..KWCCCCWK.",
            "...KYYYYK..",
            "...KCCCCK..",
            "..KCK..KCK.",
            "..KCK..KCK.",
            "..KDK..KDK.",
            ".KDDK..KDDK",
            ".KKKK..KKKK",
        };
        private static readonly string[] Grandma =
        {
            "..GGG..",
            ".GGGGG.",
            ".GSSSG.",
            "..SSS..",
            ".PPPPP.",
            "PPPPPPP",
            "SPPPPPS",
            ".PPPPP.",
            "PPPPPPP",
            ".K...K.",
            "KK..KK.",
        };
        private static readonly string[] Car =
        {
            "....KKKKKKKK........",
            "...KLLLKLLLLK.......",
            "..KLLLLKLLLLLK......",
            "KKCCCCCCCCCCCCKKKK..",
            "KCCCCCCCCCCCCCCCCCK.",
            "KCCCCCCCCCCCCCCCCCYK",
            "KCCKKKCCCCCCCKKKCCCK",
            "...KKK.......KKK....",
        };
        private static readonly string[] Mower =
        {
            "..KKKKKK..",
            ".KRRRRRRK.",
            "KRRRRRRRRK",
            "KKKKKKKKKK",
            ".KK....KK.",
            ".KK....KK.",
        };
        private static readonly string[] Mouse =
        {
            ".....MQ..",
            "M..MMMMM.",
            ".MMMMMMWK",
            "..MMMMMM.",
            "...K..K..",
        };
        private static readonly string[] Rock =
        {
            ".MMMM.",
            "MMMMMM",
            "MMMMMM",
        };

        private enum Pose { Up, HeadRight, HeadLeft, UpsideDown, Flat }
        private static readonly Pose[] Tumble = { Pose.Up, Pose.HeadRight, Pose.UpsideDown, Pose.HeadLeft };

        private readonly Texture2D texture;
        private readonly Color32[] pixels = new Color32[Width * Height];
        private int gag = -1;
        private int colourIndex = -1;
        private int ending;
        private float start;
        private int lastFrame = -1;
        private Color32 suit;
        private Color32 car;
        private bool sooty;

        public RawImage Image { get; }

        public TvHeroGags(RectTransform root, float groundTop)
        {
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "TV hero gags",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            Image = new GameObject("Hero gags", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Image.transform.SetParent(root, false);
            Image.texture = texture;
            Image.raycastTarget = false;
            var rect = Image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, groundTop + Height * .5f - .5f);
            rect.sizeDelta = new Vector2(Width, Height);
        }

        public void Destroy() => Object.Destroy(texture);

        public void Tick()
        {
            float now = Time.unscaledTime;
            if (gag < 0 || now - start >= GagSeconds) Next(now);
            int frame = Mathf.FloorToInt((now - start) * Fps);
            if (frame == lastFrame) return;
            lastFrame = frame;
            float t = frame / Fps;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
            switch (gag)
            {
                case 0: Mow(t, frame); break;
                case 1: CrossRoad(t, frame); break;
                case 2: Mice(t, frame); break;
                default: Churro(t, frame); break;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        // Another gag and another hero colour than the last ones, with one of its two endings.
        private void Next(float now)
        {
            gag = gag < 0 ? Random.Range(0, GagCount) : (gag + Random.Range(1, GagCount)) % GagCount;
            colourIndex = colourIndex < 0 ? Random.Range(0, 4) : (colourIndex + Random.Range(1, 4)) % 4;
            ending = Random.Range(0, 2);
            suit = PlayerIdentity.ColorFor(colourIndex);
            car = CarColours[Random.Range(0, CarColours.Length)];
            start = now;
            lastFrame = -1;
        }

        // ---- Mowing: tall grass falls behind the mower until it hits a stone. Then either the mower
        // bolts and drags the hero along flat as a flag, or it jams, smokes, gets a kick and blows up.
        private void Mow(float t, int frame)
        {
            const int rockX = 100;
            const float speed = 20f;
            float hit = (rockX - 30f) / speed;
            float s = t - hit;
            int stopHero = 4 + Mathf.RoundToInt(speed * Mathf.Min(t, hit));
            int mx = stopHero + 16;
            if (s > .3f && ending == 0) mx += Mathf.RoundToInt(35f * (s - .3f) + 30f * (s - .3f) * (s - .3f));
            Grass(mx + 2);
            Draw(Rock, rockX, 0, false, Pose.Up, Grey);

            if (s < 0f)
            {
                Hero(stopHero, 0, false, frame % 4 < 2 ? HeroWalk : HeroStand);
                Line(stopHero + 8, 7, mx + 1, 4, Ink); // handle
                Draw(Mower, mx, 0, false, Pose.Up, Red);
                for (int i = 0; i < 4; i++) // clippings flying out behind
                {
                    int age = (frame + i * 3) % 6;
                    Put(mx - 1 - age * 2, 2 + age - age * age / 6, Leaf);
                }
                return;
            }

            if (ending == 0)
            {
                // Sparks off the stone, then the mower bolts and the hero is dragged along, flat out.
                int bounce = s > .3f ? Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(s * 12f)) * 3f) : 0;
                Draw(Mower, mx, bounce, false, Pose.Up, Red);
                if (s < .3f)
                {
                    for (int i = 0; i < 4; i++) Put(mx + 10 + (frame + i) % 3, 2 + (i * 2 + frame) % 5, Gold);
                    Hero(stopHero, 0, false, HeroStand);
                    Exclaim(stopHero + 5, 17);
                    Line(stopHero + 8, 7, mx + 1, 4, Ink);
                    return;
                }
                int hx = mx - 18;
                int lift = 4 + frame % 2; // flapping in the wind
                Hero(hx, lift, false, frame % 2 == 0 ? HeroWalk : HeroStand, Pose.HeadRight);
                Line(hx + 16, lift + 4, mx + 1, 4 + bounce, Ink);
                return;
            }

            // Jams: rattles and smokes, gets a kick, blows up and leaves a sooty hero.
            if (s < 1.3f)
            {
                int jitter = s < 1f ? frame % 2 : 0;
                Draw(Mower, mx + jitter, 0, false, Pose.Up, Red);
                for (int i = 0; i < 3; i++) Puff(mx + 3 + i * 2, 7 + (frame + i * 3) % 9, 1);
                bool kick = s >= 1f;
                Hero(stopHero + (kick ? 2 : 0), 0, false, kick ? HeroWalk : HeroStand);
                if (s > .5f && !kick) Exclaim(stopHero + 5, 17);
                return;
            }
            if (s < 1.8f)
            {
                int r = 2 + Mathf.RoundToInt(20f * (s - 1.3f));
                Disc(mx + 5, 3, r, Gold);
                Disc(mx + 5, 3, r - 3, Red);
                Disc(mx + 5, 3, r - 6, White);
                return;
            }
            for (int i = 0; i < 6; i++) Put(mx - 6 + i * 5, (i * 7) % 3, i % 2 == 0 ? Ink : Red); // debris
            sooty = true;
            Hero(stopHero + 2, 0, false, HeroStand);
            sooty = false;
            if (frame % 12 != 0) // blinking white eyes in the soot
            {
                Put(stopHero + 7, 13, White);
                Put(stopHero + 9, 13, White);
            }
            for (int i = 0; i < 3; i++) Puff(stopHero + 5 + i * 2 - 2, 17 + (frame + i * 4) % 10, 1);
        }

        // Tall grass ahead of `cut`, short stubble behind it.
        private void Grass(int cut)
        {
            for (int x = 0; x < Width; x += 3)
            {
                if (x < cut)
                {
                    Put(x, 0, Leaf);
                    continue;
                }
                int h = 2 + x % 2;
                for (int y = 0; y < h; y++) Put(x, y, y == h - 1 ? Leaf : DarkLeaf);
                Put(x + 1, 0, DarkLeaf);
                Put(x + 1, 1, Leaf);
            }
        }

        // ---- Grandma: walks her across and a car comes. Either it flattens the hero, or he stops it,
        // lifts it over his head like a real superhero... and it drops on him.
        private void CrossRoad(float t, int frame)
        {
            const float walk = 3f;
            const float speed = 18f;
            const float carStart = 3.2f;
            int hx = 24 + Mathf.RoundToInt(speed * Mathf.Min(t, walk));
            int gx = hx - 10;
            float carX = 175f - 150f * (t - carStart);
            float contact = carStart + (175f - (hx + HeroW)) / 150f;
            bool walking = t < walk && frame % 4 < 2;

            if (ending == 0)
            {
                bool flat = t >= contact;
                GrandmaAt(gx, flat, frame);
                if (flat) Hero(hx - 5, 0, false, HeroStand, Pose.Flat);
                else Hero(hx, 0, t >= walk, walking ? HeroWalk : HeroStand);
                if (t >= carStart) Draw(Car, Mathf.RoundToInt(flat ? hx + 14 : carX), 0, true, Pose.Up, car);
                return;
            }

            float s = t - contact;
            GrandmaAt(gx, s > 1.8f, frame);
            if (s < 0f)
            {
                bool sees = t > 3.4f;
                Hero(hx, 0, t >= walk && !sees, walking ? HeroWalk : HeroStand);
                if (sees) Exclaim(hx + 5, 17);
                if (t >= carStart) Draw(Car, Mathf.RoundToInt(carX), 0, true, Pose.Up, car);
                return;
            }
            int carLeft = hx - 4;
            if (s < 1.8f)
            {
                // Up it goes, held up proudly and wobbling, then it slips.
                float up = s < .5f ? s / .5f : s < 1.5f ? 1f : 1f - (s - 1.5f) / .3f;
                int y = Mathf.RoundToInt(16f * up);
                int wobble = s > 1f && s < 1.5f ? (frame % 2 == 0 ? -1 : 1) : 0;
                Hero(hx, 0, false, HeroStand);
                if (s > .1f && y > 6) ArmsUp(hx, 0, false);
                Draw(Car, carLeft + wobble, y, true, Pose.Up, car);
                return;
            }
            Hero(hx - 5, 0, false, HeroStand, Pose.Flat);
            Draw(Car, carLeft, 0, true, Pose.Up, car);
        }

        private void GrandmaAt(int gx, bool angry, int frame)
        {
            Draw(Grandma, gx, 0, false, Pose.Up, Dress);
            if (angry && frame % 4 < 2) Line(gx + 7, 6, gx + 10, 10, Brown); // cane up, shaking
            else Line(gx + 7, 4, gx + 8, 0, Brown);
            if (angry) Exclaim(gx + 3, 13);
        }

        // ---- Garage mice: three mice dart about while the hero sweeps after them, the broom slamming into
        // the floor in clouds of dust. Either he slams it on his own foot and hops about while the mice
        // laugh, or the mice gang up and chase him off the screen.
        private void Mice(float t, int frame)
        {
            const float chase = 3.2f;
            float[] speeds = { 55f, 70f, 45f };
            float[] offsets = { 0f, 60f, 120f };
            var mice = new int[3];
            var mouseLeft = new bool[3];
            for (int i = 0; i < 3; i++)
            {
                float at = Mathf.Min(t, chase);
                mice[i] = 6 + Mathf.RoundToInt(Mathf.PingPong(at * speeds[i] + offsets[i], 150f));
                mouseLeft[i] = Mathf.PingPong(at * speeds[i] + offsets[i] + .5f, 150f) < Mathf.PingPong(at * speeds[i] + offsets[i], 150f);
            }
            float heroAt = Mathf.Min(t, chase) * 18f;
            int hx = 40 + Mathf.RoundToInt(Mathf.PingPong(heroAt, 60f));
            bool heroLeft = Mathf.PingPong(heroAt + .5f, 60f) < Mathf.PingPong(heroAt, 60f);

            if (t < chase)
            {
                Hero(hx, 0, heroLeft, frame % 4 < 2 ? HeroWalk : HeroStand);
                float swing = (t % .8f) / .8f;
                Broom(hx, 0, heroLeft, swing < .5f ? 2 : swing < .65f ? 1 : 0, swing >= .65f && swing < .85f);
                for (int i = 0; i < 3; i++) Draw(Mouse, mice[i], 0, mouseLeft[i], Pose.Up, Grey);
                return;
            }

            float s = t - chase;
            if (ending == 0)
            {
                // Raises the broom high... and slams it down on his own boot.
                if (s < .5f)
                {
                    Hero(hx, 0, heroLeft, HeroStand);
                    ArmsUp(hx, 0, heroLeft);
                    Line(FrontHand(hx, heroLeft), 13, FrontHand(hx, heroLeft) - (heroLeft ? -2 : 2), 22, Brown);
                    Rect(FrontHand(hx, heroLeft) - (heroLeft ? -1 : 3), 22, 3, 3, Gold);
                }
                else if (s < .7f)
                {
                    Hero(hx, 0, heroLeft, HeroStand);
                    Broom(hx, 0, heroLeft, 0, true);
                }
                else
                {
                    int hop = Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(s * 9f)) * 5f);
                    Hero(hx, hop, heroLeft, HeroWalk);
                    if (frame % 2 == 0) Rect(heroLeft ? hx + 1 : hx + 7, hop + 1, 3, 2, Red); // throbbing boot
                    Exclaim(hx + 5, hop + 17);
                    Line(hx - 12, 0, hx - 2, 0, Brown); // dropped broom
                }
                for (int i = 0; i < 3; i++) // mice gather round and hop with laughter
                {
                    int goal = hx + (i == 0 ? -22 : i == 1 ? 20 : 30);
                    int mxi = Mathf.RoundToInt(Mathf.MoveTowards(mice[i], goal, 80f * s));
                    int laugh = s > .7f ? Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(t * 12f + i)) * 2f) : 0;
                    Draw(Mouse, mxi, laugh, mxi > hx, Pose.Up, Grey);
                }
                return;
            }

            // The mice regroup on the right and charge; the hero drops the broom and runs for it.
            if (s < 1f)
            {
                Hero(hx, 0, false, HeroStand);
                if (s > .6f)
                {
                    ArmsUp(hx, 0, false);
                    Exclaim(hx + 5, 17);
                    Line(hx - 12, 0, hx - 2, 0, Brown);
                }
                else Broom(hx, 0, false, 1, false);
            }
            else Hero(hx - Mathf.RoundToInt(55f * (s - 1f)), 0, true, frame % 2 == 0 ? HeroWalk : HeroStand);
            for (int i = 0; i < 3; i++)
            {
                int grouped = Mathf.RoundToInt(Mathf.MoveTowards(mice[i], 135 + i * 10, 90f * s));
                int x = s < .6f ? grouped : grouped - Mathf.RoundToInt(60f * (s - .6f));
                Draw(Mouse, x, (frame + i) % 3 == 0 ? 1 : 0, s >= .6f || x > mice[i], Pose.Up, Grey);
            }
        }

        // Broom in the front hand: 2 raised behind, 1 held forward, 0 slammed on the floor (dust if `dust`).
        private void Broom(int hx, int y, bool left, int pose, bool dust)
        {
            int dir = left ? -1 : 1;
            int hand = FrontHand(hx, left);
            int tipX = pose == 2 ? hand - 4 * dir : hand + (pose == 1 ? 8 : 11) * dir;
            int tipY = y + (pose == 2 ? 18 : pose == 1 ? 10 : 1);
            Line(hand, y + 7, tipX, tipY, Brown);
            Rect(tipX - 1, tipY - 1, 3, 3, Gold);
            if (!dust) return;
            Puff(tipX - 3 * dir, 2, 1);
            Puff(tipX + 3 * dir, 3, 1);
        }

        // ---- Churro: a churro arm spins round a post at ankle height and the hero jumps it twice. On the
        // third turn either it sweeps his feet and sends him tumbling through the air, or he jumps too early,
        // lands on it and rides it round and round until it flings him off.
        private void Churro(float t, int frame)
        {
            const int post = 50;
            const int hx = post + 22;
            const float turn = 1.4f;               // seconds per turn
            const float firstHit = .7f;
            float third = firstHit + 2f * turn;
            float angle = (t - firstHit) / turn * Mathf.PI * 2f; // 0 = pointing at the hero
            int tip = post + 1 + Mathf.RoundToInt(34f * Mathf.Cos(angle));
            bool far = Mathf.Sin(angle) > 0f;

            Rect(post, 0, 3, 7, Grey);
            Rect(post - 1, 7, 5, 1, Ink);
            if (far) ChurroArm(post + 1, tip, angle);

            if (t < third - (ending == 1 ? .35f : 0f))
            {
                int y = 0;
                for (int k = 0; k < 2; k++)
                {
                    float d = (t - (firstHit + k * turn)) / .22f;
                    if (Mathf.Abs(d) < 1f) y = Mathf.RoundToInt(9f * (1f - d * d));
                }
                bool distracted = ending == 0 && t > third - .6f; // waves at the camera and misses the cue
                Hero(hx, y, distracted, y > 0 ? HeroWalk : HeroStand);
                if (distracted) ArmsUp(hx, 0, true);
            }
            else if (ending == 0)
            {
                // Swept off his feet: tumbles through the air, lands and lies dizzy.
                float s = t - third;
                if (s < 1.6f)
                {
                    int x = hx + Mathf.RoundToInt(40f * s);
                    int y = Mathf.Max(0, Mathf.RoundToInt(36f * s - 22.5f * s * s));
                    Hero(x, y, false, HeroWalk, Tumble[(frame / 2) % 4]);
                }
                else
                {
                    Hero(hx + 64, 0, false, HeroStand, Pose.HeadRight);
                    Stars(hx + 76, 6, t);
                }
            }
            else
            {
                // Lands on the churro and goes round with it, then gets flung off.
                float s = t - third;
                if (s < -.0f)
                {
                    float d = (s + .35f) / .35f;
                    Hero(hx, Mathf.RoundToInt(9f * d * (2f - d) * .9f), false, HeroWalk);
                }
                else if (s < 2f)
                {
                    bool left = Mathf.Sin(angle) < 0f;
                    Hero(tip - 5, 3, left, HeroStand);
                    Stars(tip, 19, t);
                }
                else
                {
                    float f = s - 2f;
                    int x0 = post + 1 + Mathf.RoundToInt(34f * Mathf.Cos((third + 2f - firstHit) / turn * Mathf.PI * 2f)) - 5;
                    int x = x0 + Mathf.RoundToInt(70f * f);
                    int y = Mathf.Max(0, Mathf.RoundToInt(3f + 40f * f - 30f * f * f));
                    if (y > 0 || f < .5f) Hero(x, y, false, HeroWalk, Tumble[(frame / 2) % 4]);
                    else
                    {
                        Hero(x, 0, false, HeroStand, Pose.HeadRight);
                        Stars(x + 12, 6, t);
                    }
                }
            }
            if (!far) ChurroArm(post + 1, tip, angle);
        }

        // Sugared churro from the post to its tip, striped, rising a little on the far side.
        private void ChurroArm(int from, int tip, float angle)
        {
            int y = 2 + Mathf.RoundToInt(Mathf.Sin(angle) * 1.5f);
            int a = Mathf.Min(from, tip), b = Mathf.Max(from, tip);
            for (int x = a; x <= b; x++)
            {
                Put(x, y, x % 2 == 0 ? Brown : Fry);
                Put(x, y + 1, x % 2 == 0 ? Fry : Gold);
            }
        }

        // ---- Drawing helpers ----

        private void Hero(int x, int y, bool left, string[] art, Pose pose = Pose.Up) => Draw(art, x, y, left, pose, suit);

        private static int FrontHand(int hx, bool left) => left ? hx + 2 : hx + 8;

        // Both arms raised over the head, white gloves on top.
        private void ArmsUp(int hx, int y, bool left)
        {
            int back = left ? hx + 8 : hx + 2;
            int front = FrontHand(hx, left);
            foreach (int x in new[] { back, front })
            {
                Rect(x, y + 9, 1, 4, sooty ? Soot : suit);
                Put(x, y + 13, sooty ? Soot : White);
            }
        }

        // Red "!" over a head.
        private void Exclaim(int x, int y)
        {
            Put(x, y, Red);
            Rect(x, y + 2, 1, 3, Red);
        }

        // Three yellow stars turning over a dizzy head.
        private void Stars(int x, int y, float t)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = t * 8f + i * 2.094f;
                Put(x + Mathf.RoundToInt(Mathf.Cos(a) * 5f), y + Mathf.RoundToInt(Mathf.Sin(a) * 2f), Gold);
            }
        }

        private void Puff(int x, int y, int r) => Disc(x, y, r, Smoke);

        private void Disc(int cx, int cy, int r, Color32 c)
        {
            if (r < 0) return;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r + r) Put(cx + x, cy + y, c);
        }

        // Art rows top to bottom; (x, y) is the bottom-left corner. Lying poses turn the art a quarter turn
        // with the head to one side, UpsideDown flips it, Flat squashes it into a pancake twice as wide.
        private void Draw(string[] art, int x, int y, bool flip, Pose pose, Color32 main)
        {
            int h = art.Length, w = art[0].Length;
            var dark = new Color32((byte)(main.r * .6f), (byte)(main.g * .6f), (byte)(main.b * .6f), 255);
            for (int r = 0; r < h; r++)
                for (int col = 0; col < w; col++)
                {
                    char ch = art[r][col];
                    if (ch == '.') continue;
                    var c = sooty ? (ch == 'K' ? Ink : Soot) : Paint(ch, main, dark);
                    int cx = flip ? w - 1 - col : col;
                    int up = h - 1 - r;
                    switch (pose)
                    {
                        case Pose.Up: Put(x + cx, y + up, c); break;
                        case Pose.HeadRight: Put(x + up, y + cx, c); break;
                        case Pose.HeadLeft: Put(x + h - 1 - up, y + cx, c); break;
                        case Pose.UpsideDown: Put(x + cx, y + r, c); break;
                        default:
                            Put(x + cx * 2, y + up / 8, c);
                            Put(x + cx * 2 + 1, y + up / 8, c);
                            break;
                    }
                }
        }

        private static Color32 Paint(char ch, Color32 main, Color32 dark) => ch switch
        {
            'K' => Ink,
            'S' => Skin,
            'C' => main,
            'D' => dark,
            'W' => White,
            'H' => HairBrown,
            'G' => GreyHair,
            'P' => Dress,
            'L' => Glass,
            'R' => Red,
            'Y' => Gold,
            'Q' => Pink,
            _ => main, // M: the drawing's own colour (mouse, stone)
        };

        private void Put(int x, int y, Color32 c)
        {
            if ((uint)x < Width && (uint)y < Height) pixels[y * Width + x] = c;
        }

        private void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++) Put(x + i, y + j, c);
        }

        private void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Put(x0, y0, c);
                if (x0 == x1 && y0 == y1) return;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
