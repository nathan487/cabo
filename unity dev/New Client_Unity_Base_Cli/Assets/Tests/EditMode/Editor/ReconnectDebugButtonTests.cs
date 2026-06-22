using Cabo.Client.UI;
using NUnit.Framework;

namespace Cabo.Client.Tests
{
    public sealed class ReconnectDebugButtonTests
    {
        [Test]
        public void DebugDisconnectButtonOnlyShowsInEditorOrDevelopmentBuild()
        {
            Assert.IsTrue(UIManager.ShouldShowReconnectDebugButton(isEditor: true, isDevelopmentBuild: false));
            Assert.IsTrue(UIManager.ShouldShowReconnectDebugButton(isEditor: false, isDevelopmentBuild: true));
            Assert.IsFalse(UIManager.ShouldShowReconnectDebugButton(isEditor: false, isDevelopmentBuild: false));
        }

        [Test]
        public void DebugDisconnectButtonRequiresReconnectableSession()
        {
            Assert.IsTrue(UIManager.ShouldEnableReconnectDebugButton(
                isConnected: true,
                isReconnecting: false,
                sessionToken: "session-token",
                playerId: 10000,
                roomId: 77));

            Assert.IsFalse(UIManager.ShouldEnableReconnectDebugButton(
                isConnected: false,
                isReconnecting: false,
                sessionToken: "session-token",
                playerId: 10000,
                roomId: 77));
            Assert.IsFalse(UIManager.ShouldEnableReconnectDebugButton(
                isConnected: true,
                isReconnecting: true,
                sessionToken: "session-token",
                playerId: 10000,
                roomId: 77));
            Assert.IsFalse(UIManager.ShouldEnableReconnectDebugButton(
                isConnected: true,
                isReconnecting: false,
                sessionToken: "",
                playerId: 10000,
                roomId: 77));
            Assert.IsFalse(UIManager.ShouldEnableReconnectDebugButton(
                isConnected: true,
                isReconnecting: false,
                sessionToken: "session-token",
                playerId: 0,
                roomId: 77));
            Assert.IsFalse(UIManager.ShouldEnableReconnectDebugButton(
                isConnected: true,
                isReconnecting: false,
                sessionToken: "session-token",
                playerId: 10000,
                roomId: 0));
        }
    }
}
