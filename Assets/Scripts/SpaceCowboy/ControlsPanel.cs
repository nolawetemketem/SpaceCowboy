using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The player's settings, on two tabs so each fits on one screen: SENSITIVITY (how fast the ship
    /// responds) and CONTROLS (which input does which mechanic). The start menu and the in-game overlay
    /// each build one. Works with the mouse, the keyboard and a gamepad: Tab, LB or RB switch tabs.
    /// Choosing a mechanic asks for its new input, then asks the player to confirm it.
    /// Whoever owns it calls Tick every frame while it's open, so a press only ever reaches one screen.
    /// Laid out at 540 × 351, the size of the start menu's screen.
    /// </summary>
    public class ControlsPanel
    {
        const float Width = 540f, PadX = 34f, StickThreshold = 0.6f;
        const float TrackX = 230f, TrackWidth = 205f, ValueX = 447f;
        const float KeysX = 215f, ControllerX = 355f;
        const float TabBarY = 46f, TabBarHeight = 22f, TabsBottom = 72f;
        const float SliderRow = 32f, MappingRow = 15.5f, ButtonY = 301f, ButtonHeight = 18f;
        const float FirstRepeatSeconds = 0.3f, RepeatSeconds = 0.06f;

        const int Sensitivity = 0, Controls = 1, OnEveryTab = -1;
        static readonly string[] TabNames = { "SENSITIVITY", "CONTROLS" };
        static readonly string[] TabHints =
        {
            "↑↓ SELECT · LEFT / RIGHT ADJUST · TAB / LB / RB TABS · ESC / Ⓑ BACK",
            "↑↓ SELECT · ENTER / Ⓐ CHANGE · TAB / LB / RB TABS · ESC / Ⓑ BACK",
        };

        class Row
        {
            public Image band, track, fill;
            public TextMeshProUGUI label, value, keys, controller;
            public GameSettings.Setting setting; // sliders only
            public ControlBindings.Mechanic mechanic; // mapping rows only
            public Action chosen;
            public EventTrigger events;
            public int tab = OnEveryTab;
            public bool isTabBar;
        }

        /// <summary>The panel has just closed.</summary>
        public event Action Closed;
        public bool IsOpen { get; private set; }

        readonly RectTransform page;
        readonly RectTransform[] tabPages = new RectTransform[2];
        readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[2];
        readonly Image[] tabUnderlines = new Image[2];
        readonly List<Row> rows = new();
        readonly List<Row> visible = new(); // the tab bar, the current tab's rows, then the buttons
        TextMeshProUGUI hint;
        int tab, selected;
        bool stickWasHeld;
        int heldDirection;
        float repeatTimer;

        // Like the start menu: amber text glows, except on the selected row, which is dark on amber.
        readonly Material glow = Fonts.Glow(Fonts.Mono, new Color(1f, 0.54f, 0.12f, 0.55f), 0.35f);
        readonly Material plain = Fonts.Mono.material;
        readonly Material tabGlow = Fonts.Glow(Fonts.Condensed, new Color(0.97f, 0.84f, 0.11f, 0.35f), 0.5f);

        RectTransform rebindBox;
        TextMeshProUGUI rebindName, rebindPrompt, rebindWarning, rebindHint;
        GameObject confirmButton;
        ControlBindings.Mechanic rebinding;
        ControlBindings.PressedInput newInput;
        bool hasNewInput;

        readonly Action mainMenu;

        /// <summary>Give mainMenu to add a MAIN MENU button (in Training); leave it out on the start menu itself.</summary>
        public ControlsPanel(RectTransform parent, Action mainMenu = null)
        {
            this.mainMenu = mainMenu;
            page = UI.Rect("Settings", parent, Vector2.zero, Vector2.one);
            BuildPage();
            BuildRebindBox();
            page.gameObject.SetActive(false);
        }

        public void Open()
        {
            IsOpen = true;
            page.gameObject.SetActive(true); // it stays under the screen's scanlines
            var pad = Gamepad.current;
            stickWasHeld = pad != null && Mathf.Abs(pad.leftStick.ReadValue().y) > StickThreshold; // the push that got us here doesn't count
            heldDirection = 0;
            EndRebind();
            ShowTab(tab); // the tab you were on last time
            Select(1);    // its first row, ready to use
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            page.gameObject.SetActive(false);
            GameSettings.Save();
            Closed?.Invoke();
        }

        /// <summary>Read the keyboard and gamepad. Call once a frame while open.</summary>
        public void Tick()
        {
            if (rebinding != null)
            {
                TickRebind();
                return;
            }

            var keys = Keyboard.current;
            var pad = Gamepad.current;

            if ((keys != null && (keys.escapeKey.wasPressedThisFrame || keys.backspaceKey.wasPressedThisFrame)) ||
                (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)))
            {
                Close();
                return;
            }

            int move = 0, direction = 0, switchTab = 0;
            bool choose = false;
            if (keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) move--;
                if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) move++;
                if (keys.leftArrowKey.isPressed || keys.aKey.isPressed) direction--;
                if (keys.rightArrowKey.isPressed || keys.dKey.isPressed) direction++;
                if (keys.tabKey.wasPressedThisFrame) switchTab = keys.shiftKey.isPressed ? -1 : 1;
                choose |= keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                bool stickHeld = Mathf.Abs(stick.y) > StickThreshold;
                if (pad.dpad.up.wasPressedThisFrame || (stickHeld && !stickWasHeld && stick.y > 0f)) move--;
                if (pad.dpad.down.wasPressedThisFrame || (stickHeld && !stickWasHeld && stick.y < 0f)) move++;
                stickWasHeld = stickHeld;
                if (pad.dpad.left.isPressed || stick.x < -StickThreshold) direction--;
                if (pad.dpad.right.isPressed || stick.x > StickThreshold) direction++;
                if (pad.leftShoulder.wasPressedThisFrame) switchTab = -1;
                if (pad.rightShoulder.wasPressedThisFrame) switchTab = 1;
                choose |= pad.buttonSouth.wasPressedThisFrame;
            }

            if (switchTab != 0) SwitchTab(switchTab);
            if (move != 0) Select((selected + move + visible.Count) % visible.Count);
            Adjust(Mathf.Clamp(direction, -1, 1));
            if (choose) visible[selected].chosen?.Invoke();
        }

        /// <summary>Holding left or right steps once, waits a moment, then keeps stepping. On the tab bar it switches tabs, once per press.</summary>
        void Adjust(int direction)
        {
            if (direction == 0)
            {
                heldDirection = 0;
                return;
            }
            if (direction == heldDirection)
            {
                repeatTimer -= Time.unscaledDeltaTime;
                if (repeatTimer > 0f) return;
                repeatTimer = RepeatSeconds;
                if (visible[selected].isTabBar) return;
            }
            else
            {
                heldDirection = direction;
                repeatTimer = FirstRepeatSeconds;
                if (visible[selected].isTabBar)
                {
                    SwitchTab(direction);
                    return;
                }
            }
            var setting = visible[selected].setting;
            if (setting == null) return;
            setting.Value += GameSettings.Setting.Step * direction;
            Show(visible[selected]);
        }

        void SwitchTab(int by)
        {
            var was = visible[selected];
            ShowTab((tab + by + TabNames.Length) % TabNames.Length);
            // On a row that belongs to the old tab, move to the new tab's first row; otherwise stay put.
            Select(was.tab == OnEveryTab ? visible.IndexOf(was) : 1);
        }

        void ShowTab(int index)
        {
            tab = index;
            for (int i = 0; i < tabPages.Length; i++) tabPages[i].gameObject.SetActive(i == tab);
            visible.Clear();
            foreach (var row in rows)
                if (row.tab == OnEveryTab || row.tab == tab) visible.Add(row);
            hint.text = TabHints[tab];
        }

        void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, visible.Count - 1);
            foreach (var row in rows)
            {
                bool on = row == visible[selected];
                row.band.color = on ? Palette.Amber : Color.clear;
                row.label.color = on ? Palette.Crt : Palette.Amber;
                row.label.fontSharedMaterial = on ? plain : glow;
                if (row.isTabBar) ShowTabBar(on);
                if (row.mechanic != null)
                {
                    row.keys.color = on ? Palette.Crt : Palette.TealGlow;
                    row.controller.color = on ? Palette.Crt : Palette.TealGlow;
                }
                if (row.setting == null) continue;
                row.value.color = on ? Palette.Crt : Palette.TealGlow;
                row.track.color = on ? new Color(0f, 0f, 0f, 0.45f) : new Color(1f, 0.54f, 0.12f, 0.18f);
                row.fill.color = on ? Palette.Crt : Palette.TealGlow;
            }
        }

        /// <summary>The open tab is bright and underlined; the other is dim.</summary>
        void ShowTabBar(bool focused)
        {
            for (int i = 0; i < tabLabels.Length; i++)
            {
                bool open = i == tab;
                tabLabels[i].color = focused ? Palette.Crt : open ? Palette.Yellow : Palette.AmberDim;
                tabLabels[i].fontSharedMaterial = open && !focused ? tabGlow : Fonts.Condensed.material;
                tabUnderlines[i].enabled = open;
                tabUnderlines[i].color = focused ? Palette.Crt : Palette.Amber;
            }
        }

        void Refresh()
        {
            foreach (var row in rows)
                Show(row);
        }

        static void Show(Row row)
        {
            if (row.setting != null)
            {
                row.fill.rectTransform.anchorMax = new Vector2(row.setting.Fraction, 1f);
                row.value.text = row.setting.Value.ToString("0.#");
            }
            if (row.mechanic != null)
            {
                row.keys.text = row.mechanic.KeyName;
                row.controller.text = row.mechanic.ControllerName;
            }
        }

        // --- Choosing a new input -----------------------------------------------------------------

        void StartRebind(ControlBindings.Mechanic mechanic)
        {
            rebinding = mechanic;
            hasNewInput = false;
            rebindBox.gameObject.SetActive(true);
            rebindBox.SetAsLastSibling();
            rebindName.text = mechanic.label;
            rebindPrompt.text = "PRESS THE NEW KEY OR CONTROLLER INPUT";
            rebindWarning.text = "";
            rebindHint.text = "ESC / MENU CANCEL";
            confirmButton.SetActive(false);
        }

        void TickRebind()
        {
            if (!hasNewInput)
            {
                if (ControlBindings.MenuButtonPressed) EndRebind();
                else if (ControlBindings.TryGetPressedInput(out newInput)) AskToConfirm();
                return;
            }

            var keys = Keyboard.current;
            var pad = Gamepad.current;
            if ((keys != null && (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)) ||
                (pad != null && pad.buttonSouth.wasPressedThisFrame))
                ConfirmRebind();
            else if (ControlBindings.MenuButtonPressed ||
                     (keys != null && keys.backspaceKey.wasPressedThisFrame) ||
                     (pad != null && pad.buttonEast.wasPressedThisFrame))
                EndRebind();
        }

        void AskToConfirm()
        {
            hasNewInput = true;
            string color = ColorUtility.ToHtmlStringRGB(Palette.TealGlow);
            rebindPrompt.text = $"USE <color=#{color}>{newInput.name}</color> FOR {rebinding.label}?";
            var other = ControlBindings.AlsoUsedBy(rebinding, newInput);
            rebindWarning.text = other != null ? $"{newInput.name} IS ALREADY {other.label}. THE TWO WILL SWAP." : "";
            rebindHint.text = "ENTER / Ⓐ CONFIRM · ESC / Ⓑ CANCEL";
            confirmButton.SetActive(true);
        }

        void ConfirmRebind()
        {
            ControlBindings.Assign(rebinding, newInput);
            EndRebind();
            Refresh();
        }

        void EndRebind()
        {
            rebinding = null;
            hasNewInput = false;
            rebindBox.gameObject.SetActive(false);
        }

        // --- Building it --------------------------------------------------------------------------

        void BuildPage()
        {
            StartMenu.Line(page, "Heading Shadow", "SETTINGS", Fonts.Condensed, 26f, Palette.Red, PadX + 2f, 12f + 2f, 30f);
            var heading = StartMenu.Line(page, "Heading", "SETTINGS", Fonts.Condensed, 26f, Palette.Yellow, PadX, 12f, 30f);
            heading.fontSharedMaterial = Fonts.Glow(Fonts.Condensed, new Color(0.97f, 0.84f, 0.11f, 0.35f), 0.6f);

            BuildTabBar();
            for (int i = 0; i < tabPages.Length; i++)
                tabPages[i] = UI.Rect(TabNames[i] + " Tab", page, Vector2.zero, Vector2.one);

            // SENSITIVITY: one slider per setting, with room to breathe.
            float y = TabsBottom + 14f;
            foreach (var setting in GameSettings.All)
            {
                AddSlider(setting, y);
                y += SliderRow;
            }

            // CONTROLS: every mechanic with its keyboard and controller input, all on one screen.
            y = TabsBottom + 3f;
            var mappings = tabPages[Controls];
            AddColumnTitle(mappings, "MECHANIC", PadX, y);
            AddColumnTitle(mappings, "KEYBOARD", KeysX, y);
            AddColumnTitle(mappings, "CONTROLLER", ControllerX, y);
            y += 14f;
            foreach (var mechanic in ControlBindings.All)
            {
                AddMapping(mechanic, y);
                y += MappingRow;
            }

            // On every tab, along the bottom.
            AddButton("RESET TAB TO DEFAULTS", PadX - 6f, ButtonY, 230f, ButtonHeight, () =>
            {
                if (tab == Sensitivity) GameSettings.ResetAll();
                else ControlBindings.ResetAll();
                Refresh();
            });
            AddButton("BACK", PadX + 236f, ButtonY, 110f, ButtonHeight, Close);
            if (mainMenu != null)
                AddButton("MAIN MENU", PadX + 352f, ButtonY, 120f, ButtonHeight, () =>
                {
                    Close();
                    mainMenu();
                });

            hint = StartMenu.Line(page, "Hint", "", Fonts.MonoRegular, 11f, Palette.AmberDim, PadX, ButtonY + ButtonHeight + 6f, 16f);
            ShowTab(Sensitivity);
        }

        /// <summary>The two tab names, under the heading. Selecting the bar and pressing left or right switches tabs; so does clicking a name.</summary>
        void BuildTabBar()
        {
            var row = AddRow("", PadX - 6f, TabBarY, Width - 2f * PadX + 12f, TabBarHeight, page);
            row.isTabBar = true;
            row.chosen = () => SwitchTab(1);

            float x = 6f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var label = UI.Text(TabNames[i], row.band.rectTransform, TabNames[i], 15f, Palette.Amber);
                label.font = Fonts.Condensed;
                label.characterSpacing = 8f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                StartMenu.Anchored(rect, x, 0f, 120f, TabBarHeight);
                tabLabels[i] = label;

                var underline = StartMenu.Anchored(UI.Rect("Underline", rect, new Vector2(0f, 0f), new Vector2(0f, 0f)), 0f, 0f, label.preferredWidth, 2f);
                underline.pivot = new Vector2(0f, 0f);
                underline.anchoredPosition = new Vector2(0f, 2f);
                tabUnderlines[i] = underline.gameObject.AddComponent<Image>();
                tabUnderlines[i].raycastTarget = false;

                // Clicking a tab's name opens it.
                label.raycastTarget = true;
                var events = label.gameObject.AddComponent<EventTrigger>();
                AddEvent(events, EventTriggerType.PointerEnter, _ => SelectRow(row));
                AddEvent(events, EventTriggerType.PointerClick, _ =>
                {
                    ShowTab(index);
                    SelectRow(row);
                });
                x += label.preferredWidth + 34f;
            }

            // A thin rule under the bar, so the tabs read as tabs.
            var rule = StartMenu.Anchored(UI.Rect("Tab Rule", page, new Vector2(0f, 1f), new Vector2(0f, 1f)), PadX - 6f, TabBarY + TabBarHeight + 1f, Width - 2f * PadX + 12f, 1f);
            var ruleImage = rule.gameObject.AddComponent<Image>();
            ruleImage.color = new Color(1f, 0.54f, 0.12f, 0.35f);
            ruleImage.raycastTarget = false;
        }

        void AddColumnTitle(RectTransform parent, string text, float x, float y)
        {
            StartMenu.Line(parent, text, text, Fonts.Condensed, 10f, Palette.Blush, x, y, 14f).characterSpacing = 12f;
        }

        void AddSlider(GameSettings.Setting setting, float y)
        {
            var parent = tabPages[Sensitivity];
            var row = AddRow(setting.label, PadX - 6f, y, Width - 2f * PadX + 12f, SliderRow - 10f, parent);
            row.setting = setting;
            row.tab = Sensitivity;
            float height = SliderRow - 10f;

            // An invisible box as tall as the row catches the mouse; the bar you see is a thin strip inside it.
            var track = StartMenu.Anchored(UI.Rect("Track", row.band.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)), TrackX - (PadX - 6f), 0f, TrackWidth, height, middle: true);
            track.gameObject.AddComponent<Image>().color = Color.clear;
            var strip = UI.Rect("Bar", track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            strip.sizeDelta = new Vector2(0f, 7f);
            row.track = strip.gameObject.AddComponent<Image>();
            row.track.raycastTarget = false;
            var fill = UI.Rect("Fill", strip, Vector2.zero, Vector2.one);
            row.fill = fill.gameObject.AddComponent<Image>();
            row.fill.raycastTarget = false;

            row.value = StartMenu.Line(parent, setting.label + " Value", "", Fonts.Lcd, 17f, Palette.TealGlow, ValueX, y, height);

            void SetFromPointer(BaseEventData data)
            {
                var pointer = (PointerEventData)data;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, pointer.position, pointer.pressEventCamera, out var local)) return;
                setting.Fraction = Mathf.Clamp01(local.x / TrackWidth);
                Show(row);
            }
            var events = track.gameObject.AddComponent<EventTrigger>();
            AddEvent(events, EventTriggerType.PointerEnter, _ => SelectRow(row));
            AddEvent(events, EventTriggerType.PointerDown, SetFromPointer);
            AddEvent(events, EventTriggerType.Drag, SetFromPointer);
        }

        void AddMapping(ControlBindings.Mechanic mechanic, float y)
        {
            var parent = tabPages[Controls];
            var row = AddRow(mechanic.label, PadX - 6f, y, Width - 2f * PadX + 12f, MappingRow, parent);
            row.mechanic = mechanic;
            row.tab = Controls;
            row.chosen = () => StartRebind(mechanic);
            row.keys = StartMenu.Line(parent, mechanic.label + " Key", "", Fonts.Lcd, 14f, Palette.TealGlow, KeysX, y, MappingRow);
            row.controller = StartMenu.Line(parent, mechanic.label + " Controller", "", Fonts.Lcd, 14f, Palette.TealGlow, ControllerX, y, MappingRow);
            AddEvent(row.events, EventTriggerType.PointerClick, _ => row.chosen());
        }

        void AddButton(string label, float x, float y, float width, float height, Action chosen)
        {
            var row = AddRow(label, x, y, width, height, page);
            row.chosen = chosen;
            row.label.alignment = TextAlignmentOptions.Midline;
            AddEvent(row.events, EventTriggerType.PointerClick, _ => chosen());
        }

        Row AddRow(string label, float x, float y, float width, float height, RectTransform parent)
        {
            var band = StartMenu.Anchored(UI.Rect(label, parent, new Vector2(0f, 1f), new Vector2(0f, 1f)), x, y, width, height);
            var row = new Row { band = band.gameObject.AddComponent<Image>() };

            // The label goes inside the band, so buttons can center it.
            row.label = UI.Text("Label", band, label, 12f, Palette.Amber);
            row.label.font = Fonts.Mono;
            row.label.margin = new Vector4(6f, 0f, 6f, 0f);
            row.label.alignment = TextAlignmentOptions.MidlineLeft;

            row.events = band.gameObject.AddComponent<EventTrigger>();
            AddEvent(row.events, EventTriggerType.PointerEnter, _ => SelectRow(row));
            rows.Add(row);
            return row;
        }

        void SelectRow(Row row)
        {
            int index = visible.IndexOf(row);
            if (IsOpen && rebinding == null && index >= 0) Select(index);
        }

        void BuildRebindBox()
        {
            // A solid box over the whole page, which also stops the mouse reaching the list behind it.
            rebindBox = UI.Rect("Change Control", page, Vector2.zero, Vector2.one);
            rebindBox.gameObject.AddComponent<Image>().color = Palette.Crt;

            StartMenu.Line(rebindBox, "Title", "CHANGE CONTROL", Fonts.Condensed, 12f, Palette.Blush, PadX, 62f, 16f).characterSpacing = 12f;
            rebindName = StartMenu.Line(rebindBox, "Mechanic", "", Fonts.Condensed, 36f, Palette.Yellow, PadX, 82f, 44f);
            rebindPrompt = StartMenu.Line(rebindBox, "Prompt", "", Fonts.Mono, 16f, Palette.Amber, PadX, 142f, 24f);
            rebindWarning = StartMenu.Line(rebindBox, "Warning", "", Fonts.Mono, 12f, Palette.Blush, PadX, 170f, 18f);

            confirmButton = AddBoxButton("CONFIRM", PadX, Palette.Amber, Palette.Crt, ConfirmRebind);
            AddBoxButton("CANCEL", PadX + 150f, new Color(1f, 0.54f, 0.12f, 0.18f), Palette.Amber, EndRebind);

            rebindHint = StartMenu.Line(rebindBox, "Hint", "", Fonts.MonoRegular, 11f, Palette.AmberDim, PadX, 324f, 16f);
            rebindBox.gameObject.SetActive(false);
        }

        GameObject AddBoxButton(string text, float x, Color fill, Color textColor, Action clicked)
        {
            var rect = StartMenu.Anchored(UI.Rect(text, rebindBox, new Vector2(0f, 1f), new Vector2(0f, 1f)), x, 208f, 130f, 26f);
            rect.gameObject.AddComponent<Image>().color = fill;
            var label = UI.Text("Label", rect, text, 14f, textColor);
            label.font = Fonts.Mono;
            label.alignment = TextAlignmentOptions.Midline;
            AddEvent(rect.gameObject.AddComponent<EventTrigger>(), EventTriggerType.PointerClick, _ => clicked());
            return rect.gameObject;
        }

        static void AddEvent(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => action(data));
            trigger.triggers.Add(entry);
        }
    }
}
