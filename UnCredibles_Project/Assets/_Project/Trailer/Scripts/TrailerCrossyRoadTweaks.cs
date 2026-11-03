using System.Collections.Generic;
using System.Reflection;
using UnCredibles.Minigames.CrossyRoad;
using UnityEngine;

namespace UnCredibles.Trailer
{
    // Previs only: more oil trucks from the start and bots that walk into the slicks, so the takes
    // have slips to film (shot 05). The settings asset is restored when Play Mode ends.
    [DefaultExecutionOrder(-1000)]
    public sealed class TrailerCrossyRoadTweaks : MonoBehaviour
    {
        public float oilTruckChance = 4f;
        public int maxOilTrucks = 2;
        public int oilDropsPerTruck = 3;
        public int maxOilSlicks = 8;
        public float botOilPenalty = -.8f;

        private readonly Dictionary<FieldInfo, object> originals = new();
        private CrossyRoadSettings settings;
        private float originalPenalty;

        private void Awake()
        {
            settings = FindFirstObjectByType<CrossyRoadBoard>().Settings;
            Set("oilTruckChance", oilTruckChance);
            Set("maxOilTrucks", maxOilTrucks);
            Set("oilDropsPerTruck", oilDropsPerTruck);
            Set("maxOilSlicks", maxOilSlicks);
            originalPenalty = CrossyRoadAIBrain.OilPenalty;
            CrossyRoadAIBrain.OilPenalty = botOilPenalty;
        }

        private void Set(string field, object value)
        {
            var info = typeof(CrossyRoadSettings).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) return;
            originals[info] = info.GetValue(settings);
            info.SetValue(settings, value);
        }

        private void OnDestroy()
        {
            foreach (var original in originals) original.Key.SetValue(settings, original.Value);
            CrossyRoadAIBrain.OilPenalty = originalPenalty;
        }
    }
}
