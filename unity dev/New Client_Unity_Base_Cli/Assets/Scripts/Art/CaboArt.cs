using UnityEngine;

namespace Cabo.Client.Art
{
    public static class CaboArt
    {
        const string CatalogResourcePath = "Art/CaboArtCatalog";

        static readonly string[] FoodNames =
        {
            "\u6E05\u6C34\u676F", "\u9EC4\u74DC\u7247", "\u6C34\u716E\u86CB", "\u65E0\u7CD6\u9178\u5976", "\u7389\u7C73\u676F", "\u9E21\u80F8\u8089\u6C99\u62C9", "\u5168\u9EA6\u4E09\u660E\u6CBB",
            "\u84DD\u8393\u71D5\u9EA6\u676F", "\u852C\u83DC\u80FD\u91CF\u6C64", "\u5BFF\u53F8\u4FBF\u5F53", "\u9999\u8549\u725B\u5976", "\u829D\u58EB\u62AB\u8428", "\u5DE7\u514B\u529B\u86CB\u7CD5", "\u8D85\u5927\u676F\u73CD\u73E0\u5976\u8336"
        };

        static readonly string[] Categories =
        {
            "\u96F6\u8D1F\u62C5", "\u6E05\u723D\u852C\u83DC", "\u8F7B\u86CB\u767D", "\u8F7B\u4E73\u5236\u54C1", "\u4E3B\u98DF\u5C11\u91CF", "\u5747\u8861\u8F7B\u98DF", "\u9971\u8179\u4E3B\u98DF",
            "\u8F7B\u751C\u70B9", "\u6696\u5FC3\u8F7B\u98DF", "\u7EFC\u5408\u9910", "\u751C\u996E", "\u9AD8\u70ED\u91CF\u4E3B\u98DF", "\u9AD8\u7CD6\u751C\u70B9", "\u9AD8\u7CD6\u996E\u54C1"
        };

        static CaboArtCatalog _catalog;
        static bool _loadAttempted;

        public static CaboArtCatalog Catalog
        {
            get
            {
                if (!_loadAttempted)
                {
                    _loadAttempted = true;
                    _catalog = Resources.Load<CaboArtCatalog>(CatalogResourcePath);
                    if (_catalog == null)
                        Debug.LogWarning($"[CaboArt] Resources/{CatalogResourcePath}.asset not found. Using generated fallback visuals.");
                }

                return _catalog;
            }
        }

        public static FoodCardDefinition GetFood(int value)
        {
            value = Mathf.Clamp(value, 0, 13);
            var configured = Catalog != null ? Catalog.GetFood(value) : null;
            if (configured != null)
                return configured;

            return new FoodCardDefinition
            {
                value = value,
                displayName = FoodNames[value],
                category = Categories[value],
                skillLabel = GetSkillLabel(value),
                accentColor = GetFallbackAccent(value)
            };
        }

        public static CharacterDefinition GetCharacter(string characterId)
        {
            var catalog = Catalog;
            var configured = catalog != null ? catalog.GetCharacter(characterId) : null;
            if (configured != null)
                return configured;

            if (catalog?.characters != null && catalog.characters.Length > 0)
                return catalog.characters[0];

            return null;
        }

        public static Sprite CardBack => Catalog != null ? Catalog.cardBack : null;
        public static Sprite HomeBackground => Catalog != null ? Catalog.homeBackground : null;
        public static Sprite TableBackground => Catalog != null ? Catalog.tableBackground : null;
        public static Sprite SettlementBackground => Catalog != null ? Catalog.settlementBackground : null;
        public static Sprite TableCenterBackground => Catalog != null ? Catalog.tableCenterBackground : null;
        public static AudioClip BGM => Catalog != null ? Catalog.bgmClip : null;
        public static AudioClip GetSfx(CaboSfx cue) => Catalog != null ? Catalog.GetSfx(cue) : null;
        public static Sprite GetSpecialEffect(CaboSpecialEffect cue) => Catalog != null ? Catalog.GetSpecialEffect(cue) : null;

        public static Sprite GetSeatBackground(int playerRoomIndex, int viewerRoomIndex)
        {
            var stations = Catalog?.tableStations;
            if (stations == null || stations.Length == 0)
                return null;

            int playerIndex = NormalizeSeat(playerRoomIndex);
            int viewerIndex = NormalizeSeat(viewerRoomIndex);
            int relativeSeat = NormalizeSeat(playerIndex - viewerIndex);
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                if (station != null && NormalizeSeat(station.playerRoomIndex) == playerIndex)
                    return station.GetView(relativeSeat);
            }

            return stations[0]?.GetView(relativeSeat);
        }

        static int NormalizeSeat(int seatIndex) => ((seatIndex % 4) + 4) % 4;

        public static void ResetCache()
        {
            _catalog = null;
            _loadAttempted = false;
        }

        static string GetSkillLabel(int value)
        {
            if (value == 7 || value == 8) return "\u81EA\u68C0";
            if (value == 9 || value == 10) return "\u4FA6\u67E5";
            if (value == 11 || value == 12) return "\u4EA4\u6362";
            return "";
        }

        static Color GetFallbackAccent(int value)
        {
            if (value <= 3) return new Color(0.48f, 0.70f, 0.42f, 1f);
            if (value <= 6) return new Color(0.86f, 0.66f, 0.25f, 1f);
            if (value <= 10) return new Color(0.93f, 0.52f, 0.23f, 1f);
            return new Color(0.88f, 0.35f, 0.31f, 1f);
        }
    }
}
