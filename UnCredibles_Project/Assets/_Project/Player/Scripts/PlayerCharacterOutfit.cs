using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Players
{
    // Dresses the character: mounts the parts of a loadout on its skeleton and hides the
    // body zones they cover. The animations keep working because every part shares the
    // character bones.
    public sealed class PlayerCharacterOutfit : MonoBehaviour
    {
        [Serializable]
        private struct ZoneRenderer
        {
            public BodyZone Zone;
            public Renderer Renderer;
        }

        [SerializeField] private CharacterPartCatalog catalog;
        [SerializeField, Tooltip("Object with the Animator; parts are mounted under it.")]
        private Transform model;
        [SerializeField] private ZoneRenderer[] zones = new ZoneRenderer[0];
        [SerializeField, Tooltip("Eyes, eyelids and brows: hidden by helmets that cover the face.")]
        private Renderer[] face = new Renderer[0];
        [SerializeField, Tooltip("Worn from the start (each prefab variant can have its own).")]
        private CharacterLoadout loadout;
        [SerializeField, Tooltip("Player colour used by the outfit, independent of the skin material.")]
        private Material playerColorMaterial;

        // Darker shade of each player colour, shared by every character wearing it.
        private static readonly Dictionary<Material, Material> Shades = new Dictionary<Material, Material>();
        private const float ShadeValue = .72f;
        // Black censor bar over the groin while no suit is worn (BodyNude), in the spine bone's space
        // (measured on SK_Shared_PJ, whose spine bone is scaled .3533): about 10 x 7 cm, just in front of the body.
        private const string CensorBone = "spine";
        private static readonly Vector3 CensorPosition = new Vector3(0f, -.05f, .19f);
        private static readonly Vector3 CensorSize = new Vector3(.28f, .19f, .042f);
        private static Material censorMaterial;
        private GameObject censor;

        private readonly List<GameObject> mounted = new List<GameObject>();
        private readonly List<Renderer> partRenderers = new List<Renderer>();
        private Dictionary<string, Transform> bones;
        private bool applied;

        public CharacterLoadout Loadout => loadout;
        public CharacterPartCatalog Catalog => catalog;
        // How much higher the head reaches with the helmet on, in world units.
        public float ExtraHeadHeight { get; private set; }
        public event Action Changed;

        private void Awake()
        {
            if (!applied) Apply(loadout);
        }

        // Body zones still visible plus every mounted part (for labels, bounds...).
        public List<Renderer> VisibleRenderers()
        {
            var result = new List<Renderer>();
            foreach (var zone in zones)
                if (zone.Renderer != null && zone.Renderer.enabled) result.Add(zone.Renderer);
            result.AddRange(partRenderers);
            return result;
        }

        public void Equip(CharacterPartSlot slot, string partId) => Apply(loadout.With(slot, partId));

        public void Apply(CharacterLoadout newLoadout)
        {
            applied = true;
            loadout = newLoadout;
            foreach (var go in mounted)
                if (go != null) Kill(go);
            mounted.Clear();
            partRenderers.Clear();

            var hidden = BodyZone.None;
            bool coverFace = false;
            float extraHeight = 0f;
            foreach (CharacterPartSlot slot in Enum.GetValues(typeof(CharacterPartSlot)))
            {
                var part = catalog != null ? catalog.Find(loadout.Get(slot)) : null;
                if (part == null || part.Model == null) continue;
                if (part.Slot != slot)
                {
                    Debug.LogWarning($"[Outfit] {part.Id} no es una pieza de {slot}");
                    continue;
                }
                Mount(part);
                hidden |= part.HiddenZones;
                coverFace |= part.CoversFace;
                extraHeight = Mathf.Max(extraHeight, part.ExtraHeadHeight);
            }

            foreach (var zone in zones)
                if (zone.Renderer != null) zone.Renderer.enabled = (hidden & zone.Zone) == 0;
            Censor(catalog == null || catalog.Find(loadout.Outfit) == null);
            foreach (var faceRenderer in face)
                if (faceRenderer != null) faceRenderer.enabled = !coverFace;
            ExtraHeadHeight = extraHeight * transform.lossyScale.y;
            // Parts hang deep under the model, out of reach of the filter's own child watch.
            if (TryGetComponent<UnCredibles.Shared.ComicFilter>(out var comic)) comic.Collect();
            Changed?.Invoke();
        }

        private void Mount(CharacterPart part)
        {
            bones ??= CollectBones();
            var skin = playerColorMaterial != null ? playerColorMaterial : SkinMaterial();
            var playerMaterial = part.MaterialForPlayer(skin != null ? skin.color : PlayerIdentity.ColorFor(0));
            var instance = Instantiate(part.Model);
            try
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is SkinnedMeshRenderer skinned) Rebind(skinned);
                    else if (!Reattach(renderer.transform)) continue;

                    var materials = (Material[])part.Materials.Clone();
                    if (materials.Length == 0) materials = renderer.sharedMaterials;
                    if (materials.Length > 0)
                    {
                        if (playerMaterial != null) materials[0] = playerMaterial;
                        else if (part.UsePlayerColor && skin != null) materials[0] = Shade(skin);
                    }
                    renderer.sharedMaterials = materials;
                    renderer.gameObject.layer = model.gameObject.layer;
                    mounted.Add(renderer.gameObject);
                    partRenderers.Add(renderer);
                }
            }
            finally { Kill(instance); } // its own skeleton is not needed any more
        }

        private void Censor(bool show)
        {
            if (!show)
            {
                if (censor != null) censor.SetActive(false);
                return;
            }
            if (censor == null)
            {
                bones ??= CollectBones();
                if (!bones.TryGetValue(CensorBone, out var bone)) return;
                censor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                censor.name = "Censura";
                censor.hideFlags = HideFlags.DontSave; // editor previews must not bake it into a prefab
                DestroyImmediate(censor.GetComponent<Collider>());
                censor.transform.SetParent(bone, false);
                censor.transform.localPosition = CensorPosition;
                censor.transform.localScale = CensorSize;
                censor.layer = model.gameObject.layer;
                var renderer = censor.GetComponent<Renderer>();
                renderer.sharedMaterial = CensorMaterial(SkinMaterial());
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            censor.SetActive(true);
        }

        // Flat matte black, from the body's own (URP Lit) material so the shader is always in the build.
        private static Material CensorMaterial(Material skin)
        {
            if (censorMaterial != null || skin == null) return censorMaterial;
            censorMaterial = new Material(skin) { name = "Censura", hideFlags = HideFlags.DontSave };
            foreach (var texture in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
                if (censorMaterial.HasProperty(texture)) censorMaterial.SetTexture(texture, null);
            censorMaterial.DisableKeyword("_NORMALMAP");
            censorMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");
            censorMaterial.DisableKeyword("_OCCLUSIONMAP");
            censorMaterial.DisableKeyword("_EMISSION");
            if (censorMaterial.HasProperty("_Smoothness")) censorMaterial.SetFloat("_Smoothness", 0f);
            if (censorMaterial.HasProperty("_Metallic")) censorMaterial.SetFloat("_Metallic", 0f);
            censorMaterial.SetColor("_BaseColor", Color.black);
            censorMaterial.color = Color.black;
            return censorMaterial;
        }

        // Skinned part: same bones by name as the character, mounted next to the body meshes.
        private void Rebind(SkinnedMeshRenderer part)
        {
            var source = part.bones;
            var targets = new Transform[source.Length];
            for (int i = 0; i < source.Length; i++)
                targets[i] = source[i] != null && bones.TryGetValue(source[i].name, out var bone) ? bone : null;
            part.bones = targets;
            if (part.rootBone != null && bones.TryGetValue(part.rootBone.name, out var root)) part.rootBone = root;
            var local = (part.transform.localPosition, part.transform.localRotation, part.transform.localScale);
            part.transform.SetParent(BodyParent(), false);
            part.transform.SetLocalPositionAndRotation(local.localPosition, local.localRotation);
            part.transform.localScale = local.localScale;
            part.updateWhenOffscreen = false;
        }

        // Rigid part (helmet): moves to the character bone it hung from in its own model.
        private bool Reattach(Transform part)
        {
            if (part.parent == null || !bones.TryGetValue(part.parent.name, out var bone)) return false;
            part.SetParent(bone, false);
            return true;
        }

        // Skinned parts hang from the same object as the body meshes (same place in both models).
        private Transform BodyParent()
        {
            foreach (var zone in zones)
                if (zone.Renderer != null) return zone.Renderer.transform.parent;
            return model;
        }

        private static void Kill(UnityEngine.Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        // Clothes in the player colour use a darker shade of it, so they read as clothes and
        // not as bare skin.
        private static Material Shade(Material skin)
        {
            if (Shades.TryGetValue(skin, out var shade) && shade != null) return shade;
            var color = skin.HasProperty("_BaseColor") ? skin.GetColor("_BaseColor") : skin.color;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            var dark = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.1f), v * ShadeValue);
            shade = new Material(skin) { name = skin.name + "_Prenda" };
            // The flat legacy outfits do not share the body's UV atlas.
            shade.SetTexture("_BaseMap", null);
            shade.SetTexture("_BumpMap", null);
            shade.SetTexture("_MetallicGlossMap", null);
            shade.SetTexture("_OcclusionMap", null);
            shade.DisableKeyword("_NORMALMAP");
            shade.DisableKeyword("_METALLICSPECGLOSSMAP");
            shade.DisableKeyword("_OCCLUSIONMAP");
            shade.SetColor("_BaseColor", dark);
            shade.color = dark;
            Shades[skin] = shade;
            return shade;
        }

        // The player colour of this variant is the material on the body.
        private Material SkinMaterial()
        {
            foreach (var zone in zones)
                if (zone.Zone == BodyZone.Torso && zone.Renderer != null) return zone.Renderer.sharedMaterial;
            return null;
        }

        private Dictionary<string, Transform> CollectBones()
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!map.ContainsKey(t.name)) map.Add(t.name, t);
            return map;
        }
    }
}
