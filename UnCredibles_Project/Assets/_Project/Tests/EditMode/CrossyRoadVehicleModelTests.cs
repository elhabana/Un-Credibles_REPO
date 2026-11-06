using NUnit.Framework;
using UnCredibles.Minigames.CrossyRoad;
using UnityEditor;
using UnityEngine;

namespace UnCredibles.Tests.EditMode
{
    public sealed class CrossyRoadVehicleModelTests
    {
        // Default lanes have Car Lengths 1.5, 2 and 2.5: small, medium and large.
        [Test]
        public void PicksModelBySizeAndFallsBackToSmaller()
        {
            var settings = ScriptableObject.CreateInstance<CrossyRoadSettings>();
            var small = new GameObject("small");
            var medium = new GameObject("medium");
            var large = new GameObject("large");
            try
            {
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("smallCar.model").objectReferenceValue = small;
                serialized.FindProperty("mediumCar.model").objectReferenceValue = medium;
                serialized.FindProperty("largeCar.model").objectReferenceValue = large;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreSame(small, settings.CarModelFor(1.5f).model);
                Assert.AreSame(medium, settings.CarModelFor(2f).model);
                Assert.AreSame(large, settings.CarModelFor(2.5f).model);

                serialized.FindProperty("largeCar.model").objectReferenceValue = null;
                serialized.FindProperty("mediumCar.model").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreSame(small, settings.CarModelFor(2.5f).model);
            }
            finally
            {
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(medium);
                Object.DestroyImmediate(large);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
