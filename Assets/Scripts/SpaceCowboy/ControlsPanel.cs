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
    /// The player's sensitivities with the control mapping under them, all on one scrolling page. The start
    /// menu and the in-game overlay each build one. Works with the mouse, the keyboard and a gamepad.
    /// Choosing a mechanic asks for its new input, then asks the player to confirm it.
    /// Whoever owns it calls Tick every frame while it's open, so a press only ever reaches one screen.
    /// Laid out at 540 × 351, the size of the start menu's screen.
    /// </summary>
    public class ControlsPanel
    {
        const float Width = 540f, PadX = 34f, StickThreshold = 0.6f;
        const float TrackX = 230f, TrackWidth = 205f, ValueX = 447f;
        const float KeysX = 215f, ControllerX = 355f;
        const float ListTop = 44f, ListBottom = 296f, RowHeight = 18f, SectionHeight = 15f, ScrollPerNotch = 18f;
        const float FirstRepeatSeconds = 0.3f, RepeatSeconds = 0.06f;

        class Row
        {
            public Image band, track, fill;
            public TextMeshProUGUI label, value, keys, controller;
            public GameSettings.Setting setting; // sliders only
            public ControlBindings.Mechanic mechanic; // mapping rows only
            public Action chosen;
            public EventTrigger events;
            public bool inList;
            public float top;
        }

        /// <summary>The panel has just closed.</summary>
        public event Action Closed;
        public bool IsOpen { get; private set; }

        readonly RectTransform page;
        readonly List<Row> rows = new();
        RectTransform content;
        Image scrollThumb;
        float contentHeight, scroll;
        int selected;
        bool stickWasHeld;
        int heldDirection;
        float repeatTimer;

        RectTransform rebindBox;
        TextMeshProUGUI rebindName, rebindPrompt, rebindWarning, rebindHint;
        GameObject confirmButton;
        ControlBindings.Mechanic rebinding;
        ControlBindings.PressedInput newInput;
        bool hasNewInput;

        float ListHeight => ListBottom - ListTop;
        float MostScroll => Mathf.Max(0f, contentHeight - ListHeight);

        public ControlsPanel(RectTransform parent)
        {
            page = UI.Rect("Controls and Settings", parent, Vector2.zero, Vector2.one);
            BuildPage();
            BuildRebindBox();
            page.gameObject.SetActive(false);
        }

        public void Open()
        {
            IsOpen = true;
            page.gameObject.SetActive(true);
            page.SetAsLastSibling();
            var pad = Gamepad.current;
            stickWasHeld = pad != null && Mathf.Abs(pad.leftStick.ReadValue().y) > StickThreshold; // the push that got us here doesn't count
            heldDirection = 0;
            EndRebind();
            Select(0);
            ScrollTo(0f);
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

            int move = 0, direction = 0;
            bool choose = false;
            if (keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) move--;
                if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) move++;
                if (keys.leftArrowKey.isPressed || keys.aKey.isPressed) direction--;
                if (keys.rightArrowKey.isPressed || keys.dKey.isPressed) direction++;
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
                choose |= pad.buttonSouth.wasPressedThisFrame;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.scroll.ReadValue().y != 0f)
                ScrollTo(scroll - Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f) * ScrollPerNotch);

            if (move != 0)
            {
                Select((selected + move + rows.Count) % rows.Count);
                Reveal(rows[selected]);
            }
            Adjust(Mathf.Clamp(direction, -1, 1));
            if (choose) rows[selected].chosen?.Invoke();
        }

        /// <summary>Holding left or right steps once, waits a moment, then keeps stepping.</summary>
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
            }
            else
            {
                heldDirection = direction;
                repeatTimer = FirstRepeatSeconds;
            }
            var setting = rows[selected].setting;
            if (setting == null) return;
            setting.Value += GameSettings.Setting.Step * direction;
            Show(rows[selected]);
        }

        void Select(int index)
        {
            selected = index;
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i == selected;
                var row = rows[i];
                row.band.color = on ? Palette.Amber : Color.clear;
                row.label.color = on ? Palette.Crt : Palette.Amber;
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

        // --- Scrolling ----------------------------------------------------------------------------

        void ScrollTo(float y)
        {
            scroll = Mathf.Clamp(y, 0f, MostScroll);
            content.anchoredPosition = new Vector2(0f, scroll);

            // The little bar at the side shows how much more there is.
            float thumbHeight = ListHeight * ListHeight / contentHeight;
            float travel = MostScroll > 0f ? scroll / MostScroll : 0f;
            StartMenu.Anchored(scrollThumb.rectTransform, Width - PadX + 10f, ListTop + travel * (ListHeight - thumbHeight), 3f, thumbHeight);
        }

        /// <summary>Scroll just enough to show this row. The space above it leaves its section's title in view.</summary>
        void Reveal(Row row)
        {
            if (!row.inList) return;
            if (row.top - SectionHeight < scroll) ScrollTo(row.top - SectionHeight);
            else if (row.top + RowHeight > scroll + ListHeight) ScrollTo(row.top + RowHeight - ListHeight);
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
            StartMenu.Line(page, "Heading Shadow", "CONTROLS & SETTINGS", Fonts.Condensed, 26f, Palette.Red, PadX + 2f, 12f + 2f, 30f);
            var heading = StartMenu.Line(page, "Heading", "CONTROLS & SETTINGS", Fonts.Condensed, 26f, Palette.Yellow, PadX, 12f, 30f);
            heading.fontSharedMaterial = Fonts.Glow(Fonts.Condensed, new Color(0.97f, 0.84f, 0.11f, 0.35f), 0.6f);

            // The part that scrolls: it's cut off at the edges of this window.
            var list = UI.Rect("List", page, new Vector2(0f, 1f), new Vector2(1f, 1f));
            list.offsetMin = new Vector2(0f, -ListBottom);
            list.offsetMax = new Vector2(0f, -ListTop);
            list.gameObject.AddComponent<RectMask2D>();
            content = UI.Rect("Content", list, new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);

            float y = 0f;
            AddSectionTitle("SENSITIVITY", PadX, y);
            y += SectionHeight;
            foreach (var setting in GameSettings.All)
            {
                AddSlider(setting, y);
                y += RowHeight;
            }

            y += 8f;
            AddSectionTitle("CONTROLS", PadX, y);
            AddSectionTitle("KEYBOARD", KeysX, y);
            AddSectionTitle("CONTROLLER", ControllerX, y);
            y += SectionHeight;
            foreach (var mechanic in ControlBindings.All)
            {
                AddMapping(mechanic, y);
                y += RowHeight;
            }
            contentHeight = y;
            content.sizeDelta = new Vector2(0f, contentHeight);

            var thumb = UI.Rect("Scroll Bar", page, new Vector2(0f, 1f), new Vector2(0f, 1f));
            scrollThumb = thumb.gameObject.AddComponent<Image>();
            scrollThumb.color = Palette.AmberDim;
            scrollThumb.raycastTarget = false;

            // These stay put below the list.
            const float buttonY = ListBottom + 5f;
            AddButton("RESET TO DEFAULTS", PadX - 6f, buttonY, 230f, RowHeight, () =>
            {
                GameSettings.ResetAll();
                ControlBindings.ResetAll();
                Refresh();
            });
            AddButton("BACK", PadX + 236f, buttonY, 110f, RowHeight, Close);

            StartMenu.Line(page, "Hint", "UP / DOWN SELECT · LEFT / RIGHT ADJUST · ENTER / Ⓐ CHANGE · ESC / Ⓑ BACK",
                Fonts.MonoRegular, 11f, Palette.AmberDim, PadX, buttonY + RowHeight + 6f, 16f);
        }

        void AddSectionTitle(string text, float x, float y)
        {
            StartMenu.Line(content, text, text, Fonts.Condensed, 10f, Palette.Blush, x, y, SectionHeight).characterSpacing = 12f;
        }

        void AddSlider(GameSettings.Setting setting, float y)
        {
            var row = AddRow(setting.label, PadX - 6f, y, Width - 2f * PadX + 12f, RowHeight, content);
            row.setting = setting;
            row.inList = true;
            row.top = y;

            // An invisible box as tall as the row catches the mouse; the bar you see is a thin strip inside it.
            var track = StartMenu.Anchored(UI.Rect("Track", row.band.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)), TrackX - (PadX - 6f), 0f, TrackWidth, RowHeight, middle: true);
            track.gameObject.AddComponent<Image>().color = Color.clear;
            var strip = UI.Rect("Bar", track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            strip.sizeDelta = new Vector2(0f, 7f);
            row.track = strip.gameObject.AddComponent<Image>();
            row.track.raycastTarget = false;
            var fill = UI.Rect("Fill", strip, Vector2.zero, Vector2.one);
            row.fill = fill.gameObject.AddComponent<Image>();
            row.fill.raycastTarget = false;

            row.value = StartMenu.Line(content, setting.label + " Value", "", Fonts.Lcd, 17f, Palette.TealGlow, ValueX, y, RowHeight);

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
            var row = AddRow(mechanic.label, PadX - 6f, y, Width - 2f * PadX + 12f, RowHeight, content);
            row.mechanic = mechanic;
            row.inList = true;
            row.top = y;
            row.chosen = () => StartRebind(mechanic);
            row.keys = StartMenu.Line(content, mechanic.label + " Key", "", Fonts.Lcd, 15f, Palette.TealGlow, KeysX, y, RowHeight);
            row.controller = StartMenu.Line(content, mechanic.label + " Controller", "", Fonts.Lcd, 15f, Palette.TealGlow, ControllerX, y, RowHeight);
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
            if (IsOpen) Select(rows.IndexOf(row));
        }

        void BuildRebindBox()
        {
            // A solid box over the whole page, which also stops the mouse reaching the list behind it.
            rebindBox = UI.Rect("Change Control", page, Vector2.zero, Vector2.one);
            rebindBox.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.03f, 1f);

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
