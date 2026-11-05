using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnCredibles.Trailer
{
    // Previs only: overrides numeric fields of a minigame's settings for the recording (e.g. a longer
    // stun so it can be filmed), reached from a component: "settings.stunSeconds" on GarageMiceController.
    // Every value is restored when Play Mode ends, so the settings assets are left untouched.
    [DefaultExecutionOrder(-1000)]
    public sealed class TrailerFieldTweaks : MonoBehaviour
    {
        [Serializable]
        public sealed class Tweak
        {
            [Tooltip("Component type name in the scene, e.g. GarageMiceController.")] public string component;
            [Tooltip("Dotted path of private or public fields, e.g. settings.stunSeconds.")] public string path;
            public float value;
        }

        public Tweak[] tweaks = new Tweak[0];

        private readonly List<(object owner, FieldInfo field, object original)> originals = new();
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private void Awake()
        {
            foreach (var tweak in tweaks)
            {
                object owner = null;
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (behaviour.GetType().Name == tweak.component) { owner = behaviour; break; }
                var names = tweak.path.Split('.');
                for (int i = 0; owner != null && i < names.Length; i++)
                {
                    var field = owner.GetType().GetField(names[i], Fields);
                    if (field == null) { Debug.LogWarning("[Trailer] No field " + tweak.path + " on " + tweak.component); break; }
                    if (i < names.Length - 1) { owner = field.GetValue(owner); continue; }
                    originals.Add((owner, field, field.GetValue(owner)));
                    field.SetValue(owner, Convert.ChangeType(tweak.value, field.FieldType));
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var (owner, field, original) in originals) field.SetValue(owner, original);
        }
    }
}
