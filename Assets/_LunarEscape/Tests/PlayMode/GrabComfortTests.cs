using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 远距离抓取的物体应被吸到手上，而不是悬停在射线命中处。
    public sealed class GrabComfortTests
    {
        [UnityTest] public IEnumerator FarGrabbedItemsSnapToTheHand()
        {
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            yield return null;
            var hands = Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(hands.Length, Is.GreaterThanOrEqualTo(2), "左右手都有 Near-Far 交互器");
            Assert.That(hands.All(h => h.farAttachMode == InteractorFarAttachMode.Near),
                "请执行 Lunar Escape → Install Grab Comfort: Items Snap To Hand (All Scenes)");
        }
    }
}
