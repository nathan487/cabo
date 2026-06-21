using Cabo.Client.Art;
using NUnit.Framework;
using UnityEngine;

namespace Cabo.Client.Tests
{
    public sealed class TableCharacterRuntimeTests
    {
        [Test]
        public void TableCharacterCameraOnlyRendersIsolatedLayer()
        {
            var runtime = TableCharacterRuntime.Create(null);

            try
            {
                var camera = runtime.GetComponentInChildren<Camera>(true);

                Assert.NotNull(camera);
                Assert.AreEqual(1 << 31, camera.cullingMask);
            }
            finally
            {
                Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }
}
