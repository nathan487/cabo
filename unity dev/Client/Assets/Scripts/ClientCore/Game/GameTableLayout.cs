using UnityEngine;

namespace Cabo.Client.Game
{
    public struct LayoutPos
    {
        public Vector2 anchoredPos;
        public Vector2 size;
        public float rotation;
    }

    /// <summary>Calculates positions for player areas based on player count and seat.</summary>
    public static class GameTableLayout
    {
        /// <param name="totalPlayers">2-4</param>
        /// <param name="mySeat">Your own seat index (0-based)</param>
        /// <param name="canvasSize">UI canvas reference resolution</param>
        /// <returns>Array indexed by display position (0=self, 1+=opponents clockwise)</returns>
        public static LayoutPos[] Calculate(int totalPlayers, Vector2 canvasSize)
        {
            var layout = new LayoutPos[totalPlayers];
            float cx = canvasSize.x / 2f, cy = canvasSize.y / 2f;

            if (totalPlayers == 2)
            {
                float w = 270, h = 115;
                layout[0] = new LayoutPos { anchoredPos = new Vector2(0, -cy + 220), size = new Vector2(w, h), rotation = 0 };
                layout[1] = new LayoutPos { anchoredPos = new Vector2(0, cy - 140), size = new Vector2(w, h), rotation = 0 };
            }
            else if (totalPlayers == 3)
            {
                float w = 220, h = 105;
                // Self: bottom center
                layout[0] = new LayoutPos { anchoredPos = new Vector2(0, -cy + 220), size = new Vector2(w, h), rotation = 0 };
                // Two opponents: top-left and top-right, spaced apart
                float oppX = cx * 0.55f; // ~220
                layout[1] = new LayoutPos { anchoredPos = new Vector2(-oppX, cy - 140), size = new Vector2(w, h), rotation = 0 };
                layout[2] = new LayoutPos { anchoredPos = new Vector2(oppX, cy - 140), size = new Vector2(w, h), rotation = 0 };
            }
            else // 4 players
            {
                // Top/bottom: full width
                float wH = 260, hH = 110;
                layout[0] = new LayoutPos { anchoredPos = new Vector2(0, -cy + 220), size = new Vector2(wH, hH), rotation = 0 };
                layout[1] = new LayoutPos { anchoredPos = new Vector2(0, cy - 140), size = new Vector2(wH, hH), rotation = 0 };
                // Left/right: narrower, centered vertically between top and bottom
                float wV = 210, hV = 100;
                layout[2] = new LayoutPos { anchoredPos = new Vector2(-cx + wV * 0.7f, 30), size = new Vector2(wV, hV), rotation = 0 };
                layout[3] = new LayoutPos { anchoredPos = new Vector2(cx - wV * 0.7f, 30), size = new Vector2(wV, hV), rotation = 0 };
            }

            return layout;
        }
    }
}
