using System;
using System.Collections.Generic;
using MCM.Abstractions;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace CustomisableUI
{
    // The MCM screen. Every value is read back through ThemeState, which re-resolves this object ten times a second,
    // so changes show up live, even on the settings screen itself.
    // [Backgrounds] A partial class: the custom backgrounds feature's options are in Backgrounds\ModSettings.Backgrounds.cs.
    public sealed partial class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        // Every player-visible string in this file starts with a "{=CUI_...}" tag: the game looks the tag up in the
        // player's language files (Module\ModuleData\Languages) and shows the English text after it when there is no
        // translation. See docs/TRANSLATING.md. The tags are part of the text, so they are never shown to the player.
        private const string General = "{=CUI_Group_General}1. General";
        private const string Panels = "{=CUI_Group_Panels}2. Panels & backgrounds";
        private const string Frames = "{=CUI_Group_Frames}3. Frames & borders";
        private const string Buttons = "{=CUI_Group_Buttons}4. Buttons";
        private const string TextGroup = "{=CUI_Group_Text}5. Text";
        private const string Fonts = "{=CUI_Group_Fonts}6. Fonts";
        private const string Grading = "{=CUI_Group_Grading}7. Colour grading";
        private const string Relations = "{=CUI_Group_Relations}8. Allies, enemies & neutrals";
        private const string Statuses = "{=CUI_Group_Statuses}9. Good, bad & warning";
        private const string CrosshairGroup = "{=CUI_Group_Crosshair}10. Crosshair";

        internal const string FontVanilla = "{=CUI_Vanilla}Vanilla (no change)";

        internal static readonly string[] FontChoices =
        {
            FontVanilla,
            "{=CUI_Font_Light}Fira Sans Light",
            "{=CUI_Font_Regular}Fira Sans Regular",
            "{=CUI_Font_Medium}Fira Sans Medium",
            "{=CUI_Font_Galahad}Galahad (decorative)",
        };

        internal static readonly string[] ContrastChoices =
        {
            "{=CUI_Contrast_Standard}Standard (3:1)",
            "{=CUI_Contrast_Good}Good (4.5:1, WCAG AA)",
            "{=CUI_Contrast_Best}Best (7:1, WCAG AAA)",
        };

        internal static readonly string[] StatusChoices =
        {
            "{=CUI_Status_Vanilla}Keep red and green (vanilla)",
            "{=CUI_Status_ColourBlind}Colour-blind safe (orange and blue)",
            "{=CUI_Status_Recolour}Recolour like other text",
        };

        // The order matters: Hotkey.cs maps an index back to a key.
        internal static readonly string[] HotkeyChoices =
        {
            "{=CUI_Hotkey_Off}Off",
            "Ctrl+Shift+U",
            "F8",
            "F9",
            "F10",
        };

        public override string Id => "CustomisableUI_v1";
        public override string DisplayName => "Customisable UI";
        public override string FolderName => "CustomisableUI";
        public override string FormatType => "json2";

        // ---- General ---------------------------------------------------------------------------------------------

        [SettingPropertyBool("{=CUI_Enabled}Enable Customisable UI", Order = 0, RequireRestart = false,
            HintText = "{=CUI_Enabled_Hint}Master switch. Off restores the vanilla look immediately.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("{=CUI_DebugLogging}Write a debug log", Order = 1, RequireRestart = false,
            HintText = "{=CUI_DebugLogging_Hint}Lists every UI brush and sprite the mod sees, and what it decided to treat it as, in " +
                       "Documents\\Mount and Blade II Bannerlord\\Logs\\CustomisableUI.log. Useful if something " +
                       "is being recoloured that shouldn't be.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool DebugLogging { get; set; }

        // Compatibility with mods that colour the UI to carry meaning, e.g. item rows tinted by quality or rarity.
        // Off by default: it also leaves a few of the game's own coloured elements (green and gold buttons, damage
        // flashes) unthemed, and most players don't need it.
        [SettingPropertyBool("{=CUI_KeepOtherModColours}Keep colours added by other mods", Order = 2, RequireRestart = false,
            HintText = "{=CUI_KeepOtherModColours_Hint}Compatibility with mods that colour the interface to mean something, such as item rows tinted " +
                       "by quality. Panels, buttons and text that are already a clearly coloured shade (not black, white, " +
                       "grey or the game's usual browns) are left alone instead of being themed. A few of the game's own " +
                       "coloured buttons and panels stay as they are too.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool KeepOtherModColours { get; set; }

        [SettingPropertyDropdown("{=CUI_ToggleHotkey}Hotkey: show vanilla look", Order = 3, RequireRestart = false,
            HintText = "{=CUI_ToggleHotkey_Hint}Press to switch between your theme and the game's vanilla look, from anywhere (including this " +
                       "screen). Handy for comparing, and a way out if a combination makes the interface hard to read. " +
                       "Press again to bring your theme back.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public Dropdown<string> ToggleHotkey { get; set; } = new Dropdown<string>(HotkeyChoices, 1);

        // ---- Panels ----------------------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_PanelColour}Panel colour", Order = 0, RequireRestart = false,
            HintText = "{=CUI_PanelColour_Hint}Tint for window and panel backgrounds. The tint is multiplied with the original artwork, " +
                       "so pair darker colours with a higher brightness.")]
        [SettingPropertyGroup(Panels, GroupOrder = 1)]
        public Dropdown<string> PanelColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_PanelHex}Panel custom colour", 1, false,
            "{=CUI_PanelHex_Hint}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB, e.g. #2A3F8F.")]
        [SettingPropertyGroup(Panels, GroupOrder = 1)]
        public string PanelHex { get; set; } = "#2A3F8F";

        [SettingPropertyFloatingInteger("{=CUI_PanelStrength}Panel colour strength", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=CUI_Hint_Strength}0% keeps the vanilla colour, 100% applies the full tint.")]
        [SettingPropertyGroup(Panels, GroupOrder = 1)]
        public float PanelStrength { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=CUI_PanelBrightness}Panel brightness", 0.3f, 2.5f, "0.00", Order = 3, RequireRestart = false,
            HintText = "{=CUI_PanelBrightness_Hint}1.00 is vanilla. Raise it if a dark tint makes panels too murky.")]
        [SettingPropertyGroup(Panels, GroupOrder = 1)]
        public float PanelBrightness { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=CUI_PanelOpacity}Panel opacity", 0.1f, 1f, "0%", Order = 4, RequireRestart = false,
            HintText = "{=CUI_PanelOpacity_Hint}Lower values let the game world show through panels. Keep it high enough to read text.")]
        [SettingPropertyGroup(Panels, GroupOrder = 1)]
        public float PanelOpacity { get; set; } = 1f;

        // ---- Frames ----------------------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_FrameColour}Frame colour", Order = 0, RequireRestart = false,
            HintText = "{=CUI_FrameColour_Hint}Tint for the decorative borders around panels.")]
        [SettingPropertyGroup(Frames, GroupOrder = 2)]
        public Dropdown<string> FrameColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_FrameHex}Frame custom colour", 1, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Frames, GroupOrder = 2)]
        public string FrameHex { get; set; } = "#E0B040";

        [SettingPropertyFloatingInteger("{=CUI_FrameStrength}Frame colour strength", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=CUI_Hint_Strength}0% keeps the vanilla colour, 100% applies the full tint.")]
        [SettingPropertyGroup(Frames, GroupOrder = 2)]
        public float FrameStrength { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=CUI_FrameBrightness}Frame brightness", 0.3f, 2.5f, "0.00", Order = 3, RequireRestart = false,
            HintText = "{=CUI_Hint_BrightnessVanilla}1.00 is vanilla.")]
        [SettingPropertyGroup(Frames, GroupOrder = 2)]
        public float FrameBrightness { get; set; } = 1f;

        // ---- Buttons ---------------------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_ButtonColour}Button colour", Order = 0, RequireRestart = false,
            HintText = "{=CUI_ButtonColour_Hint}Tint for button artwork (hover and pressed states keep their relative brightness).")]
        [SettingPropertyGroup(Buttons, GroupOrder = 3)]
        public Dropdown<string> ButtonColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_ButtonHex}Button custom colour", 1, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Buttons, GroupOrder = 3)]
        public string ButtonHex { get; set; } = "#3A64D8";

        [SettingPropertyFloatingInteger("{=CUI_ButtonStrength}Button colour strength", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=CUI_Hint_Strength}0% keeps the vanilla colour, 100% applies the full tint.")]
        [SettingPropertyGroup(Buttons, GroupOrder = 3)]
        public float ButtonStrength { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=CUI_ButtonBrightness}Button brightness", 0.3f, 2.5f, "0.00", Order = 3, RequireRestart = false,
            HintText = "{=CUI_Hint_BrightnessVanilla}1.00 is vanilla.")]
        [SettingPropertyGroup(Buttons, GroupOrder = 3)]
        public float ButtonBrightness { get; set; } = 1f;

        // ---- Text ------------------------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_TextColour}Text colour", Order = 0, RequireRestart = false,
            HintText = "{=CUI_TextColour_Hint}Replaces the colour of all text. 'Auto' picks light or dark text to suit your panel and button colours.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public Dropdown<string> TextColour { get; set; } = new Dropdown<string>(Swatches.TextNames(), 0);

        [SettingPropertyText("{=CUI_TextHex}Text custom colour", 1, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public string TextHex { get; set; } = "#F5EBD3";

        [SettingPropertyFloatingInteger("{=CUI_TextStrength}Text colour strength", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=CUI_TextStrength_Hint}0% keeps the vanilla colour, 100% replaces it completely.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public float TextStrength { get; set; } = 1f;

        [SettingPropertyBool("{=CUI_KeepTextReadable}Keep text readable", Order = 3, RequireRestart = false,
            HintText = "{=CUI_KeepTextReadable_Hint}If your text colour would be hard to read on your panel and button colours (pale text on a light " +
                       "panel, dark text on a dark one), light or dark text is used instead. Also corrects vanilla text " +
                       "when a theme makes it hard to read.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public bool KeepTextReadable { get; set; } = true;

        [SettingPropertyDropdown("{=CUI_ContrastTarget}Minimum text contrast", Order = 4, RequireRestart = false,
            HintText = "{=CUI_ContrastTarget_Hint}How much contrast 'Keep text readable' insists on, as a WCAG contrast ratio. 4.5:1 is the " +
                       "accessibility standard (AA) for normal text; 7:1 (AAA) is the enhanced level for low vision.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public Dropdown<string> ContrastTarget { get; set; } = new Dropdown<string>(ContrastChoices, 1);

        // ---- Good, bad and warning -------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_StatusColours}Red and green status palette", Order = 0, RequireRestart = false,
            HintText = "{=CUI_StatusColours_Hint}Losses, gains and warnings are drawn in red and green, which people with red-green colour " +
                       "blindness often can't tell apart. 'Colour-blind safe' swaps them for orange and blue. Any " +
                       "colour picked below takes priority over this.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public Dropdown<string> StatusColours { get; set; } = new Dropdown<string>(StatusChoices, 0);

        [SettingPropertyDropdown("{=CUI_GoodColour}Good colour", Order = 1, RequireRestart = false,
            HintText = "{=CUI_GoodColour_Hint}Gains, bonuses and positive values: green text, and the game's 'positive' and 'bonus' text and " +
                       "slider fills. 'Vanilla' leaves them alone.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public Dropdown<string> GoodColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_GoodHex}Good custom colour", 2, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public string GoodHex { get; set; } = "#4EC24E";

        [SettingPropertyDropdown("{=CUI_BadColour}Bad colour", Order = 3, RequireRestart = false,
            HintText = "{=CUI_BadColour_Hint}Losses, penalties and negative values: red text, and the game's 'negative' and 'penalty' text " +
                       "and slider fills.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public Dropdown<string> BadColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_BadHex}Bad custom colour", 4, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public string BadHex { get; set; } = "#E04545";

        [SettingPropertyDropdown("{=CUI_WarningColour}Warning colour", Order = 5, RequireRestart = false,
            HintText = "{=CUI_WarningColour_Hint}Text the game marks as a warning.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public Dropdown<string> WarningColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_WarningHex}Warning custom colour", 6, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Statuses, GroupOrder = 8)]
        public string WarningHex { get; set; } = "#E0A030";

        // ---- Crosshair -------------------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_CrosshairColour}Crosshair colour", Order = 0, RequireRestart = false,
            HintText = "{=CUI_CrosshairColour_Hint}Colour of the crosshair lines in battle. 'Vanilla' keeps the game's own.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public Dropdown<string> CrosshairColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_CrosshairHex}Crosshair custom colour", 1, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public string CrosshairHex { get; set; } = "#FFFFFF";

        [SettingPropertyDropdown("{=CUI_BlockedColour}Crosshair colour when blocked", Order = 2, RequireRestart = false,
            HintText = "{=CUI_BlockedColour_Hint}The game turns the crosshair red when the target is invalid. This is the colour used instead.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public Dropdown<string> BlockedColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_BlockedHex}Blocked custom colour", 3, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public string BlockedHex { get; set; } = "#FF4444";

        [SettingPropertyDropdown("{=CUI_HitMarkerColour}Hit marker colour", Order = 4, RequireRestart = false,
            HintText = "{=CUI_HitMarkerColour_Hint}The marker that flashes when you hit something. Vanilla is white.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public Dropdown<string> HitMarkerColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_HitMarkerHex}Hit marker custom colour", 5, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public string HitMarkerHex { get; set; } = "#FFFFFF";

        [SettingPropertyDropdown("{=CUI_KillMarkerColour}Kill and headshot marker colour", Order = 6, RequireRestart = false,
            HintText = "{=CUI_KillMarkerColour_Hint}The marker that flashes on a kill, and the headshot marker. Vanilla is red.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public Dropdown<string> KillMarkerColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_KillMarkerHex}Kill marker custom colour", 7, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public string KillMarkerHex { get; set; } = "#FF3333";

        [SettingPropertyFloatingInteger("{=CUI_CrosshairOpacity}Crosshair opacity", 0.2f, 1f, "0%", Order = 8, RequireRestart = false,
            HintText = "{=CUI_CrosshairOpacity_Hint}Lower values make the crosshair and its markers more transparent. 100% is vanilla.")]
        [SettingPropertyGroup(CrosshairGroup, GroupOrder = 9)]
        public float CrosshairOpacity { get; set; } = 1f;

        [SettingPropertyFloatingInteger("{=CUI_TextBrightness}Text brightness", 0.5f, 1.8f, "0.00", Order = 6, RequireRestart = false,
            HintText = "{=CUI_Hint_BrightnessVanilla}1.00 is vanilla.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public float TextBrightness { get; set; } = 1f;

        [SettingPropertyDropdown("{=CUI_OutlineColour}Text outline colour", Order = 7, RequireRestart = false,
            HintText = "{=CUI_OutlineColour_Hint}Colour of the outline added by 'Text outline thickness'.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public Dropdown<string> OutlineColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), Swatches.IndexOf("Black"));

        [SettingPropertyText("{=CUI_OutlineHex}Text outline custom colour", 8, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public string OutlineHex { get; set; } = "#000000";

        [SettingPropertyFloatingInteger("{=CUI_OutlineThickness}Text outline thickness", 0f, 1f, "0%", Order = 9, RequireRestart = false,
            HintText = "{=CUI_OutlineThickness_Hint}Adds an outline to text to keep it readable on busy or recoloured backgrounds. " +
                       "0% leaves each piece of text with its vanilla outline.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public float OutlineThickness { get; set; }

        [SettingPropertyFloatingInteger("{=CUI_ShadowStrength}Text shadow", 0f, 1f, "0%", Order = 10, RequireRestart = false,
            HintText = "{=CUI_ShadowStrength_Hint}Adds a soft dark halo behind text, which helps names stay readable over a busy map. " +
                       "0% leaves each piece of text with its vanilla shadow.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public float ShadowStrength { get; set; }

        [SettingPropertyDropdown("{=CUI_ShadowColour}Text shadow colour", Order = 11, RequireRestart = false,
            HintText = "{=CUI_ShadowColour_Hint}Colour of the shadow added by 'Text shadow'.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public Dropdown<string> ShadowColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), Swatches.IndexOf("Black"));

        [SettingPropertyText("{=CUI_ShadowHex}Text shadow custom colour", 12, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(TextGroup, GroupOrder = 4)]
        public string ShadowHex { get; set; } = "#000000";

        // ---- Fonts -----------------------------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_MainFont}Main font", Order = 0, RequireRestart = false,
            HintText = "{=CUI_MainFont_Hint}Replaces the font used for most interface text (vanilla: Fira Sans Extra Condensed). " +
                       "Only applies to Latin-alphabet languages.")]
        [SettingPropertyGroup(Fonts, GroupOrder = 5)]
        public Dropdown<string> MainFont { get; set; } = new Dropdown<string>(FontChoices, 0);

        [SettingPropertyDropdown("{=CUI_DecorativeFont}Decorative font", Order = 1, RequireRestart = false,
            HintText = "{=CUI_DecorativeFont_Hint}Replaces the decorative serif font used for titles and many labels (vanilla: Galahad). " +
                       "Only applies to Latin-alphabet languages.")]
        [SettingPropertyGroup(Fonts, GroupOrder = 5)]
        public Dropdown<string> DecorativeFont { get; set; } = new Dropdown<string>(FontChoices, 0);

        [SettingPropertyFloatingInteger("{=CUI_TextScale}Text size", 0.75f, 1.35f, "0%", Order = 2, RequireRestart = false,
            HintText = "{=CUI_TextScale_Hint}Scales labels and most single-line text. Long passages (conversations, encyclopedia) " +
                       "keep their vanilla size.")]
        [SettingPropertyGroup(Fonts, GroupOrder = 5)]
        public float TextScale { get; set; } = 1f;

        // ---- Colour grading --------------------------------------------------------------------------------------

        [SettingPropertyInteger("{=CUI_HueShift}Hue shift", -180, 180, "0", Order = 0, RequireRestart = false,
            HintText = "{=CUI_HueShift_Hint}Rotates the hue of panels, frames and buttons, in degrees. Applied on top of the tints above.")]
        [SettingPropertyGroup(Grading, GroupOrder = 6)]
        public int HueShift { get; set; }

        [SettingPropertyInteger("{=CUI_Saturation}Saturation", -100, 100, "0", Order = 1, RequireRestart = false,
            HintText = "{=CUI_Saturation_Hint}Negative values drain colour, positive values intensify it.")]
        [SettingPropertyGroup(Grading, GroupOrder = 6)]
        public int Saturation { get; set; }

        [SettingPropertyInteger("{=CUI_Brightness}Brightness", -100, 100, "0", Order = 2, RequireRestart = false,
            HintText = "{=CUI_Brightness_Hint}Negative values darken, positive values lighten.")]
        [SettingPropertyGroup(Grading, GroupOrder = 6)]
        public int Brightness { get; set; }

        [SettingPropertyBool("{=CUI_GradeEverything}Also grade icons and artwork", Order = 3, RequireRestart = false,
            HintText = "{=CUI_GradeEverything_Hint}Off (default): only panels, frames and buttons are graded. On: every sprite is, including " +
                       "icons, banners and portraits' frames.")]
        [SettingPropertyGroup(Grading, GroupOrder = 6)]
        public bool GradeEverything { get; set; }

        // ---- Allies, enemies and neutrals ------------------------------------------------------------------------

        [SettingPropertyDropdown("{=CUI_AllyColour}Ally colour", Order = 0, RequireRestart = false,
            HintText = "{=CUI_AllyColour_Hint}Colour for allies: your own faction and anyone allied with it. 'Vanilla' leaves the game's " +
                       "own colours alone, so pick a colour for each of ally, enemy and neutral to switch them on.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public Dropdown<string> AllyColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_AllyHex}Ally custom colour", 1, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public string AllyHex { get; set; } = "#32A852";

        [SettingPropertyDropdown("{=CUI_EnemyColour}Enemy colour", Order = 2, RequireRestart = false,
            HintText = "{=CUI_EnemyColour_Hint}Colour for enemies: anyone at war with your faction, including bandits.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public Dropdown<string> EnemyColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_EnemyHex}Enemy custom colour", 3, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public string EnemyHex { get; set; } = "#C03040";

        [SettingPropertyDropdown("{=CUI_NeutralColour}Neutral colour", Order = 4, RequireRestart = false,
            HintText = "{=CUI_NeutralColour_Hint}Colour for everyone who is neither an ally nor an enemy.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public Dropdown<string> NeutralColour { get; set; } = new Dropdown<string>(Swatches.AllNames(), 0);

        [SettingPropertyText("{=CUI_NeutralHex}Neutral custom colour", 5, false,
            "{=CUI_Hint_CustomColour}Used when 'Custom (use hex code)' is picked above. Format: #RRGGBB.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public string NeutralHex { get; set; } = "#E0B040";

        [SettingPropertyBool("{=CUI_RelationBars}Colour settlement bars on the map", Order = 6, RequireRestart = false,
            HintText = "{=CUI_RelationBars_Hint}The coloured bar behind each castle, town and village name on the campaign map. The game " +
                       "draws these in dark green, dark red or black.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public bool RelationBars { get; set; } = true;

        // The party and settlement names (and party troop counts) you see on the campaign map. This is what "the colour
        // of parties on the map" means in the game's interface. Called RelationNames in the first release, where it
        // defaulted to off; renamed so that the new default (on) applies to everyone. It still changes nothing until
        // a colour is picked above.
        [SettingPropertyBool("{=CUI_RelationNameplates}Colour parties and settlements on the map", Order = 7, RequireRestart = false,
            HintText = "{=CUI_RelationNameplates_Hint}Draws party and settlement names and troop counts on the campaign map in their relation colour " +
                       "instead of their faction's colour. Your own party keeps its colour. Turn off to keep faction colours.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public bool RelationNameplates { get; set; } = true;

        [SettingPropertyBool("{=CUI_RelationTooltips}Colour party tooltip troop counts", Order = 8, RequireRestart = false,
            HintText = "{=CUI_RelationTooltips_Hint}The ally, enemy and neutral troop numbers shown when you hover over a party.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public bool RelationTooltips { get; set; } = true;

        [SettingPropertyBool("{=CUI_RelationBattle}Colour battle markers", Order = 9, RequireRestart = false,
            HintText = "{=CUI_RelationBattle_Hint}In battle: the markers over formations, siege engine markers, and friendly and enemy name text.")]
        [SettingPropertyGroup(Relations, GroupOrder = 7)]
        public bool RelationBattle { get; set; } = true;

        // ---- Presets ---------------------------------------------------------------------------------------------

        public override IEnumerable<ISettingsPreset> GetBuiltInPresets()
        {
            foreach (ISettingsPreset preset in base.GetBuiltInPresets())
            {
                yield return preset;
            }

            // Accessibility. These use the strictest contrast target (7:1, WCAG AAA) unless noted, so "keep text
            // readable" backs up the colours chosen here, and add text outlines, larger text or colour-blind-safe
            // status colours where that helps. Listed first so they are easy to find.
            yield return Preset("acc-contrast-dark", "Accessible: High Contrast (Dark)", s =>
            {
                s.Panel("Black");
                s.Frame("White");
                s.Button("Charcoal", 1.8f);
                s.Text("White");
                s.Outline("Black", 0.5f);
                s.RequireContrast(2);
            });
            yield return Preset("acc-contrast-light", "Accessible: High Contrast (Light)", s =>
            {
                s.Panel("White", 2.5f);
                s.Frame("Charcoal");
                s.Button("White", 2.5f);
                s.TextColour.SelectedIndex = Swatches.TextIndexOf(Swatches.Auto);
                s.Outline("White", 0.35f);
                s.RequireContrast(2);
            });
            yield return Preset("acc-low-vision", "Accessible: Yellow on Black (Low Vision)", s =>
            {
                s.Panel("Black");
                s.Frame("Yellow", 1.2f);
                s.Button("Charcoal", 1.8f);
                s.Text("Yellow");
                s.Outline("Black", 0.5f);
                s.TextScale = 1.2f;
                Pick(s.CrosshairColour, "Yellow");
                s.RequireContrast(2);
            });
            yield return Preset("acc-large-text", "Accessible: Large Text", s =>
            {
                s.Panel("Charcoal", 1.2f);
                s.Frame("Steel", 1.2f);
                s.Button("Slate", 1.4f);
                s.Text("White");
                s.Outline("Black", 0.4f);
                s.TextScale = 1.3f;
                s.RequireContrast(2);
            });
            yield return Preset("acc-colour-blind", "Accessible: Colour-blind Safe (Blue and Orange)", s =>
            {
                s.Panel("Navy", 1.6f);
                s.Frame("Orange", 1.2f);
                s.Button("Deep Blue", 1.5f);
                s.TextColour.SelectedIndex = Swatches.TextIndexOf(Swatches.Auto);
                s.StatusColours.SelectedIndex = 1;

                // Ally and enemy are the pair most often confused (green and red), so they get blue and vermillion.
                s.AllyColour.SelectedIndex = Swatches.IndexOf("Sky Blue");
                s.EnemyColour.SelectedIndex = Swatches.IndexOf("Vermillion");
                s.NeutralColour.SelectedIndex = Swatches.IndexOf("Steel");
                s.RelationNameplates = true;

                // The crosshair turns red when blocked and flashes red on a kill: vermillion stays distinct from the
                // white crosshair for every common kind of colour blindness.
                Pick(s.BlockedColour, "Vermillion");
                Pick(s.KillMarkerColour, "Vermillion");
                s.RequireContrast(2);
            });
            yield return Preset("acc-easy-reading", "Accessible: Easy Reading (plain font, larger)", s =>
            {
                // The game ships no dyslexia-specific typeface, so this uses its plainest sans-serif in the heaviest
                // weight in place of the decorative serif, at a larger size, in soft off-white on charcoal.
                s.Panel("Charcoal", 1.3f);
                s.Frame("Steel");
                s.Button("Slate", 1.3f);
                s.Text("Cream");
                s.MainFont.SelectedIndex = 3;
                s.DecorativeFont.SelectedIndex = 3;
                s.TextScale = 1.15f;
                s.Outline("Black", 0.3f);
                s.RequireContrast(2);
            });
            yield return Preset("acc-low-glare", "Accessible: Low Glare (soft contrast)", s =>
            {
                // For light sensitivity: no pure white, slightly dimmed text, muted panels. Held to AA rather than
                // AAA, since maximum contrast is exactly what this one avoids.
                s.Panel("Charcoal", 1.1f);
                s.Frame("Slate");
                s.Button("Slate", 1.2f);
                s.Text("Cream");
                s.TextBrightness = 0.9f;
                s.RequireContrast(1);
            });

            // Colour-pair themes. Each one sets panels, frames, buttons and text together; everything not mentioned
            // stays at its default. Brightness values compensate for tints being multiplied with the (dark) vanilla
            // artwork, so treat them as starting points and fine-tune with the sliders. All are held to the default
            // 4.5:1 text contrast by "keep text readable".
            yield return Theme("black-gold", "Black and Gold",
                panel: "Black", frame: "Gold", button: "Charcoal", text: "Gold",
                frameBrightness: 1.4f, buttonBrightness: 1.6f);
            yield return Theme("purple-gold", "Purple and Gold",
                panel: "Royal Purple", frame: "Gold", button: "Plum", text: "Gold",
                panelBrightness: 1.5f, buttonBrightness: 1.5f);
            yield return Theme("red-white", "Red and White",
                panel: "Crimson", frame: "White", button: "Burgundy", text: "White",
                panelBrightness: 1.4f, buttonBrightness: 1.5f, outline: 0.3f);
            yield return Theme("black-red", "Black and Red",
                panel: "Black", frame: "Crimson", button: "Burgundy", text: "White",
                frameBrightness: 1.3f, buttonBrightness: 1.5f);
            yield return Theme("black-white", "Black and White",
                panel: "Black", frame: "White", button: "Charcoal", text: "White",
                buttonBrightness: 1.6f);
            yield return Theme("navy-silver", "Navy and Silver",
                panel: "Navy", frame: "Steel", button: "Slate", text: "White",
                panelBrightness: 1.6f, frameBrightness: 1.2f, buttonBrightness: 1.2f);
            yield return Theme("blue-gold", "Blue and Gold",
                panel: "Royal Blue", frame: "Gold", button: "Navy", text: "Cream",
                panelBrightness: 1.3f, buttonBrightness: 1.6f);
            yield return Theme("green-gold", "Green and Gold",
                panel: "Forest Green", frame: "Gold", button: "Olive", text: "Gold",
                panelBrightness: 0.9f, buttonBrightness: 1.0f);
            yield return Theme("green-white", "Green and White",
                panel: "Forest Green", frame: "White", button: "Teal", text: "White",
                panelBrightness: 1.5f, buttonBrightness: 1.2f);
            yield return Theme("burgundy-cream", "Burgundy and Cream",
                panel: "Burgundy", frame: "Cream", button: "Crimson", text: "Cream",
                panelBrightness: 1.7f, buttonBrightness: 1.2f);
            yield return Theme("teal-cream", "Teal and Cream",
                panel: "Teal", frame: "Cream", button: "Navy", text: "Cream",
                panelBrightness: 1.4f, buttonBrightness: 1.6f);
            yield return Theme("orange-black", "Orange and Black",
                panel: "Black", frame: "Amber", button: "Charcoal", text: "Amber",
                frameBrightness: 1.4f, buttonBrightness: 1.5f);
            yield return Theme("rose-cream", "Rose and Cream",
                panel: "Plum", frame: "Rose", button: "Rose", text: "Cream",
                panelBrightness: 1.7f, frameBrightness: 1.1f, buttonBrightness: 0.9f);
            yield return Theme("slate-sky", "Slate and Sky Blue",
                panel: "Slate", frame: "Sky Blue", button: "Charcoal", text: "White",
                panelBrightness: 1.5f, frameBrightness: 1.1f, buttonBrightness: 1.6f);
            yield return Theme("parchment-ink", "Parchment and Ink",
                panel: "Parchment", frame: "Copper", button: "Cream", text: "Black",
                panelBrightness: 2.1f, frameBrightness: 1.2f, buttonBrightness: 2.4f);
        }

        // ---- Plumbing --------------------------------------------------------------------------------------------

        // Called for every change made through the settings screen. ThemeState also polls, so this just makes the
        // change land a few frames sooner.
        public override void OnPropertyChanged(string propertyName = null)
        {
            ThemeState.Invalidate();
            base.OnPropertyChanged(propertyName);
        }

        // The preset's name is tagged for translation like everything else: "acc-low-glare" becomes the string id
        // CUI_Preset_acc_low_glare.
        internal static string PresetName(string id, string name)
        {
            return "{=CUI_Preset_" + id.Replace('-', '_') + "}" + name;
        }

        private ISettingsPreset Preset(string id, string name, Action<ModSettings> configure)
        {
            return new MemorySettingsPreset(Id, id, PresetName(id, name), () =>
            {
                var settings = new ModSettings();
                configure(settings);
                return settings;
            });
        }

        private ISettingsPreset Theme(
            string id, string name, string panel, string frame, string button, string text,
            float panelBrightness = 1f, float frameBrightness = 1f, float buttonBrightness = 1f, float outline = 0f)
        {
            return Preset(id, name, s =>
            {
                s.Panel(panel, panelBrightness);
                s.Frame(frame, frameBrightness);
                s.Button(button, buttonBrightness);
                s.Text(text);
                if (outline > 0f)
                {
                    s.OutlineColour.SelectedIndex = Swatches.IndexOf("Black");
                    s.OutlineThickness = outline;
                }
            });
        }

        private static void Pick(Dropdown<string> dropdown, string swatch)
        {
            dropdown.SelectedIndex = Swatches.IndexOf(swatch);
        }

        private void Panel(string swatch, float brightness = 1f)
        {
            PanelColour.SelectedIndex = Swatches.IndexOf(swatch);
            PanelBrightness = brightness;
        }

        private void Frame(string swatch, float brightness = 1f)
        {
            FrameColour.SelectedIndex = Swatches.IndexOf(swatch);
            FrameBrightness = brightness;
        }

        private void Button(string swatch, float brightness = 1f)
        {
            ButtonColour.SelectedIndex = Swatches.IndexOf(swatch);
            ButtonBrightness = brightness;
        }

        private void Outline(string swatch, float thickness)
        {
            OutlineColour.SelectedIndex = Swatches.IndexOf(swatch);
            OutlineThickness = thickness;
        }

        private void RequireContrast(int choice)
        {
            ContrastTarget.SelectedIndex = choice;
        }

        private void Text(string swatch)
        {
            TextColour.SelectedIndex = Swatches.TextIndexOf(swatch);
        }
    }
}
