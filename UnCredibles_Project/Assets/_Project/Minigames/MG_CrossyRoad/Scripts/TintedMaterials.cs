using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames.CrossyRoad
{
    // Tinted material copies, one per (original material, colour). Used instead of
    // MaterialPropertyBlock because property blocks make renderers incompatible with the SRP Batcher.
    // The owner must call DestroyAll() in its OnDestroy.
    public sealed class TintedMaterials
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<(Material, Color), Material> copies = new Dictionary<(Material, Color), Material>();
        private readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>(); // copy -> source
        private readonly List<Material> buffer = new List<Material>();

        public Material Get(Material source, Color color)
        {
            if (source == null) return null;
            // Repainting a renderer that already uses one of our copies must not copy the copy.
            if (originals.TryGetValue(source, out var original)) source = original;
            if (copies.TryGetValue((source, color), out var copy)) return copy;

            copy = new Material(source) { name = source.name + "_Tinted" };
            copy.SetColor(BaseColorId, color);
            copies.Add((source, color), copy);
            originals.Add(copy, source);
            return copy;
        }

        // Every material slot of the renderer is swapped for its tinted copy.
        public void Apply(Renderer target, Color color)
        {
            target.GetSharedMaterials(buffer);
            for (int i = 0; i < buffer.Count; i++) buffer[i] = Get(buffer[i], color);
            target.SetSharedMaterials(buffer);
        }

        public void DestroyAll()
        {
            foreach (var copy in copies.Values)
                if (copy != null) Object.Destroy(copy);
            copies.Clear();
            originals.Clear();
        }
    }
}
