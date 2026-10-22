namespace SunnyValley.Layout
{
    /// <summary>
    /// The style of the whole interface in one place: colours, corner radii, font sizes, spacing and the sizes
    /// of standard elements, all in canvas units of the 1080×1920 reference canvas. Screens use these names only,
    /// never their own numbers, so the game looks the same everywhere and one change restyles everything.
    /// Colours are 0xRRGGBBAA (the Unity side turns them into Color32). Look: light wood and greenery,
    /// yellow «Играть» buttons, round icons (GDD §10).
    /// </summary>
    public static class Tokens
    {
        // ------------------------------------------------------------------ colours
        public const uint Sky = 0x9ED9F6FF;
        public const uint Grass = 0x86C95AFF;
        public const uint GrassDark = 0x6BAE45FF;
        public const uint Soil = 0xB7834EFF;
        public const uint Wood = 0xF6E3BCFF;       // panels
        public const uint WoodDark = 0xC8955AFF;   // panel rims, wells
        public const uint WoodInset = 0xE9CF9AFF;  // sub-panels inside a panel
        public const uint Plate = 0x3E2A17C8;      // currency plates: dark translucent
        public const uint Yellow = 0xFFC928FF;     // «Играть»
        public const uint YellowShade = 0xD98E00FF;
        public const uint Green = 0x6CC04AFF;
        public const uint GreenShade = 0x3F8C2AFF;
        public const uint Red = 0xEE5A3CFF;
        public const uint RedShade = 0xA8331FFF;
        public const uint Blue = 0x47A8EEFF;
        public const uint BlueShade = 0x2369A8FF;
        public const uint Purple = 0xA070E0FF;
        public const uint Orange = 0xFF9A3CFF;
        public const uint Gold = 0xFFD54AFF;
        public const uint Pink = 0xFF6F91FF;
        public const uint Cyan = 0x4FD8E8FF;
        public const uint White = 0xFFFFFFFF;
        public const uint TextDark = 0x5A3512FF;
        public const uint TextLight = 0xFFFFFFFF;
        public const uint TextMuted = 0x8A6A44FF;
        public const uint Dim = 0x000000A0;        // behind popups
        public const uint Shadow = 0x00000040;
        public const uint TrackOff = 0xC9B48EFF;   // toggle off
        public const uint BarTrack = 0x5A3A1EA0;

        // ------------------------------------------------------------------ corner radii
        public const float RadiusSmall = 16;
        public const float RadiusMedium = 28;
        public const float RadiusLarge = 44;

        // ------------------------------------------------------------------ font sizes: preferred and the smallest allowed
        public const float FontLogo = 104, FontLogoMin = 64;
        public const float FontTitle = 64, FontTitleMin = 44;
        public const float FontPlay = 76, FontPlayMin = 48;
        public const float FontButton = 56, FontButtonMin = 38;
        public const float FontValue = 46, FontValueMin = 30;      // numbers on plates
        public const float FontBig = 84, FontBigMin = 52;          // the moves counter
        public const float FontBody = 42, FontBodyMin = 30;
        public const float FontCaption = 34, FontCaptionMin = 28;
        public const float FontBadge = 32, FontBadgeMin = 28;

        /// <summary>The smallest text allowed on a real screen, in points (dp): below this it is unreadable.</summary>
        public const float MinReadablePoints = 8.5f;

        // ------------------------------------------------------------------ spacing
        public const float Margin = 24;      // from the safe area's edges
        public const float Gap = 16;         // between neighbours
        public const float GapBig = 32;
        public const float Pad = 40;         // inside popups

        // ------------------------------------------------------------------ element sizes
        /// <summary>A finger needs 44 points (Apple HIG; Android asks 48 dp for the touch target).</summary>
        public const float MinTouchPoints = 44;

        /// <summary>Board cells: nine across even a 360 dp phone, swiped rather than tapped precisely.</summary>
        public const float MinCellPoints = 36;

        public const float PlateHeight = 104;     // the pill
        public const float PlateTouch = 132;      // its tap area
        public const float PlateMaxWidth = 300;
        public const float SideButtonWidth = 172;
        public const float SideButtonHeight = 200;
        public const float SideIcon = 136;
        public const float RoundButton = 140;       // icon-only buttons: settings, pause
        public const float CloseButton = 136;
        public const float PlayHeight = 176;
        public const float PlayMinWidth = 560;
        public const float PlayMaxWidth = 780;
        public const float ButtonHeight = 156;
        public const float SmallButtonHeight = 136;
        public const float PopupMaxWidth = 940;
        public const float PopupMargin = 48;
        public const float LevelTopHeight = 200;
        public const float BoosterWidth = 200;
        public const float BoosterHeight = 204;
        public const float BoosterMaxGap = 56;
        public const float BoardMargin = 16;
        public const float MaxCell = 140;
        public const float BadgeSize = 56;
        public const float ProgressHeight = 56;
        public const float ToggleWidth = 184;
        public const float ToggleHeight = 136;
        public const float RowHeight = 168;
    }
}
