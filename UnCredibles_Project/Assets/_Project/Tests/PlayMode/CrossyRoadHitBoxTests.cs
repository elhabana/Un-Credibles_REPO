using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnCredibles.Minigames.CrossyRoad;
using UnityEngine;

namespace UnCredibles.Tests.PlayMode
{
    // The car hit box must follow the model, its Scale and the lane fit, on every side.
    public sealed class CrossyRoadHitBoxTests
    {
        private const float Radius = 0.1f;
        private const float Edge = 0.02f; // just inside / just outside

        [Test]
        public void HitBoxMatchesScaledModelsOfCarAndTruck()
        {
            var settings = ScriptableObject.CreateInstance<CrossyRoadSettings>();
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube); // 2 x 1 x 1 m car
            model.transform.position = new Vector3(100f, 0f, 0f);
            model.transform.localScale = new Vector3(2f, 1f, 1f);
            var carBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var truckBox = new GameObject("Truck", typeof(CrossyRoadOilTruck));
            var board = new GameObject("Board").AddComponent<CrossyRoadBoard>();
            var traffic = new GameObject("Traffic").AddComponent<CrossyRoadTraffic>();
            try
            {
                Set(settings, "smallCar", new CrossyRoadSettings.VehicleModel { model = model, scale = 1f });
                Set(settings, "oilTruck", new CrossyRoadSettings.VehicleModel { model = model, scale = 1.5f });
                Set(board, "settings", settings);
                Set(traffic, "board", board);
                Set(traffic, "carPrefab", carBox.transform);
                Set(traffic, "oilTruckPrefab", truckBox.transform);
                traffic.Initialize(false);

                // Default lanes: cell 1.5 m, roads 0 and 3 have cars 1.5 cells long (2.25 m), max depth 0.7 cells.
                // Car scale = min(2.25 / 2, 1.05 / 1) = 1.05 -> half 1.05 x 0.525. Truck x1.5 -> half 1.575 x 0.7875.
                traffic.ReadState(State(settings.RoadCount, carRoad: 0, truckRoad: 3));
                float carZ = board.LaneToZ(CrossyRoadBoard.FirstRoadLane);
                float truckZ = board.LaneToZ(CrossyRoadBoard.FirstRoadLane + 3);

                AssertEdge(traffic, new Vector3(1.05f + Radius, 0f, carZ), Vector3.right, "car front");
                AssertEdge(traffic, new Vector3(0f, 0f, carZ + 0.525f + Radius), Vector3.forward, "car side");
                AssertEdge(traffic, new Vector3(1.575f + Radius, 0f, truckZ), Vector3.right, "truck front");
                AssertEdge(traffic, new Vector3(0f, 0f, truckZ + 0.7875f + Radius), Vector3.forward, "truck side (wider than a car)");
            }
            finally
            {
                foreach (var go in new[] { model, carBox, truckBox, board.gameObject, traffic.gameObject }) Object.Destroy(go);
                Object.Destroy(settings);
            }
        }

        private static void AssertEdge(CrossyRoadTraffic traffic, Vector3 edge, Vector3 outward, string what)
        {
            Assert.IsTrue(traffic.Overlaps(edge - outward * Edge, Radius), what + ": just inside must hit");
            Assert.IsFalse(traffic.Overlaps(edge + outward * Edge, Radius), what + ": just outside must miss");
        }

        // A host snapshot with one car at x = 0 on one road and one oil truck on another.
        private static BinaryReader State(int roads, int carRoad, int truckRoad)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write((byte)0);
            writer.Write((byte)roads);
            for (int i = 0; i < roads; i++)
            {
                bool any = i == carRoad || i == truckRoad;
                writer.Write((byte)(any ? 1 : 0));
                if (!any) continue;
                writer.Write((ushort)(i + 1));
                writer.Write(0f);
                writer.Write(i == truckRoad);
            }
            stream.Position = 0;
            return new BinaryReader(stream);
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
