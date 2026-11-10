using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnCredibles.Tests.EditMode
{
    // The PJ inside the lobby/podium figure and the CrossyRoad player stands on its origin:
    // the lowest foot of every loop has to touch y = 0, neither floating nor sunk into the floor.
    public sealed class PJFeetOnGroundTests
    {
        private const string Model = "Assets/_Project/Characters/PJ/SK_Shared_PJ.fbx";
        private const float Tolerance = .01f; // of the character height

        [TestCase("Assets/_Project/Player/Prefabs/PF_PlayerFigure.prefab")]
        [TestCase("Assets/_Project/Minigames/MG_CrossyRoad/Prefabs/PF_CR_Player.prefab")]
        public void LowestFootOfEveryLoopTouchesTheGround(string prefabPath)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            var mesh = new Mesh();
            try
            {
                var character = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "PJ");
                var model = character.GetComponentInChildren<Animator>(true).gameObject;
                float height = character.lossyScale.y;
                var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => c.name.StartsWith("AN_Shared_PJ_")).ToArray();
                Assert.AreEqual(4, clips.Length);
                foreach (var clip in clips)
                {
                    float lowest = float.MaxValue;
                    for (int i = 0; i < 24; i++)
                    {
                        clip.SampleAnimation(model, clip.length * i / 24f);
                        foreach (var skinned in character.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skinned.BakeMesh(mesh, true);
                            var matrix = skinned.transform.localToWorldMatrix; // same measure as PJCharacterSetup.BakedPoints
                            lowest = Mathf.Min(lowest, mesh.vertices.Min(v => matrix.MultiplyPoint3x4(v).y));
                        }
                    }
                    float ground = (lowest - character.position.y) / height;
                    Assert.That(Mathf.Abs(ground), Is.LessThan(Tolerance), $"{prefabPath} {clip.name}: lowest foot at {ground:P1} of the height");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(instance);
            }
        }
    }
}
