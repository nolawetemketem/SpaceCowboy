using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The end of a match, on the same battered screen as the settings: who won, then every pilot's kills, deaths
    /// and K/D (and captures, in capture the flag), with PLAY AGAIN and MAIN MENU.
    /// Up/Down (or the D-pad or stick) and Enter / A to choose, or click.
    /// Whoever owns it calls Tick every frame while it's showing.
    /// </summary>
    public class ResultsPanel
    {
        public struct Line
        {
            public string pilot;
            public int team; // pilots of one team are listed together; free for all is all one team here
            public int kills, deaths, captures;
            public bool you;
        }

        const float PadX = 34f, StickThreshold = 0.6f;

        public event Action PlayAgain, MainMenu;
        public bool IsOpen => window.gameObject.activeSelf;

        readonly RectTransform window;
        readonly TextMeshProUGUI title, subtitle, table;
        readonly List<(Image band, TextMeshProUGUI label, Action chosen)> buttons = new();
        int selected;
        bool stickWasHeld;

        public ResultsPanel(Transform parent)
        {
            var centre = new Vector2(0.5f, 0.5f);
            window = UI.Rect("Results Window", parent, centre, centre);
            window.sizeDelta = StartMenu.FramePixels;
            window.localScale = Vector3.one * 1.45f;
            var bezel = window.gameObject.AddComponent<RawImage>();
            bezel.texture = StartMenu.FrameTexture();
            bezel.color = new Color(0.8f, 0.78f, 0.74f);

            var screen = UI.Rect("Screen", window, centre, centre);
            screen.sizeDelta = StartMenu.MenuPixels;
            screen.gameObject.AddComponent<Image>().color = Palette.Crt;

            // Everything on the results screen is the one amber, like the menu: no team or pilot colors.
            title = StartMenu.Line(screen, "Title", "", Fonts.Condensed, 34f, Palette.Amber, PadX, 14f, 40f);
            title.fontSharedMaterial = Fonts.Glow(Fonts.Condensed, new Color(1f, 0.54f, 0.12f, 0.35f), 0.6f);
            subtitle = StartMenu.Line(screen, "Subtitle", "", Fonts.Condensed, 12f, Palette.AmberDim, PadX, 56f, 16f);
            subtitle.characterSpacing = 10f;
            table = StartMenu.Line(screen, "Table", "", Fonts.Mono, 12f, Palette.Amber, PadX, 80f, 210f);
            table.alignment = TextAlignmentOptions.TopLeft;
            table.richText = true;

            AddButton(screen, "PLAY AGAIN", PadX - 6f, () => PlayAgain?.Invoke());
            AddButton(screen, "MAIN MENU", PadX + 236f, () => MainMenu?.Invoke());
            StartMenu.Line(screen, "Hint", "← → SELECT · ENTER / Ⓐ CHOOSE", Fonts.MonoRegular, 11f, Palette.AmberDim, PadX, 325f, 16f);
            StartMenu.AddScreenEffects(screen);
            window.gameObject.SetActive(false);
        }

        void AddButton(RectTransform screen, string text, float x, Action chosen)
        {
            var band = StartMenu.Anchored(UI.Rect(text, screen, new Vector2(0f, 1f), new Vector2(0f, 1f)), x, 298f, 220f, 20f);
            var image = band.gameObject.AddComponent<Image>();
            var label = UI.Text("Label", band, text, 13f, Palette.Amber);
            label.font = Fonts.Mono;
            label.alignment = TextAlignmentOptions.Midline;
            int index = buttons.Count;
            buttons.Add((image, label, chosen));
            UI.Choosable(band.gameObject, () => Select(index), chosen);
        }

        public void Show(string heading, string match, List<Line> lines, bool flags)
        {
            title.text = heading;
            subtitle.text = match;

            var text = new StringBuilder();
            string Columns(string pilot, string kills, string deaths, string kd, string caps) =>
                $"{pilot}<pos=40%>{kills}<pos=56%>{deaths}<pos=72%>{kd}" + (flags ? $"<pos=88%>{caps}" : "");
            text.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(Palette.AmberDim)}>" + Columns("PILOT", "KILLS", "DEATHS", "K/D", "FLAGS") + "</color>");
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                // A blank line between teams stands in for team colors.
                if (i > 0 && line.team != lines[i - 1].team) text.AppendLine();
                string kd = line.deaths == 0 ? line.kills.ToString("0.00") : ((float)line.kills / line.deaths).ToString("0.00");
                string name = line.you ? line.pilot + " (YOU)" : line.pilot;
                text.AppendLine(Columns(name, line.kills.ToString(), line.deaths.ToString(), kd, line.captures.ToString()));
            }
            table.text = text.ToString();

            window.gameObject.SetActive(true);
            window.SetAsLastSibling();
            var pad = Gamepad.current;
            stickWasHeld = pad != null && Mathf.Abs(pad.leftStick.ReadValue().x) > StickThreshold;
            Select(0);
        }

        public void Hide() => window.gameObject.SetActive(false);

        void Select(int index)
        {
            selected = index;
            for (int i = 0; i < buttons.Count; i++)
            {
                bool on = i == selected;
                buttons[i].band.color = on ? Palette.Amber : new Color(1f, 0.54f, 0.12f, 0.12f);
                buttons[i].label.color = on ? Palette.Crt : Palette.Amber;
            }
        }

        /// <summary>Read the keyboard and gamepad. Call once a frame while it's showing.</summary>
        public void Tick()
        {
            int move = 0;
            bool choose = false;
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame) move--;
                if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame) move++;
                choose |= keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                float x = pad.leftStick.ReadValue().x;
                bool held = Mathf.Abs(x) > StickThreshold;
                if (pad.dpad.left.wasPressedThisFrame || (held && !stickWasHeld && x < 0f)) move--;
                if (pad.dpad.right.wasPressedThisFrame || (held && !stickWasHeld && x > 0f)) move++;
                stickWasHeld = held;
                choose |= pad.buttonSouth.wasPressedThisFrame;
            }
            if (move != 0) Select((selected + move + buttons.Count) % buttons.Count);
            if (choose) buttons[selected].chosen();
        }
    }
}
