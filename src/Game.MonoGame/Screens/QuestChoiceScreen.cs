using Game.Core.Campaigns;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

/// <summary>A parchment dialogue whose height follows its story and choice text.</summary>
internal sealed class QuestChoiceScreen
{
    private const int PanelX = 166;
    private const int PanelY = 112;
    private const int PanelWidth = 948;
    private const int ChoiceX = 210;
    private const int ChoiceWidth = 860;
    // PixelFont advances six pixels per character at scale 3. Keep each line within
    // the parchment's ~840 px text area (840 / 18 = 46 whole characters).
    private const int NarrativeColumns = 46;
    private const int ChoiceColumns = 65;
    private static readonly Color Wood = new(44, 31, 22);
    private static readonly Color Bronze = new(148, 111, 57);
    private static readonly Color Gold = new(226, 196, 125);
    private static readonly Color Parchment = new(217, 203, 161);
    private static readonly Color Ink = new(52, 42, 28);
    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly Quest _quest;
    private readonly Action<string> _choose;
    private readonly Action _cancel;
    private readonly ChoiceLayout[] _choiceLayouts;
    private readonly int _narrativeY = PanelY + 88;
    private readonly int _footerY;
    private readonly int _panelHeight;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;

    public QuestChoiceScreen(PixelCanvas canvas, Quest quest, Action<string> choose,
        Action cancel, MouseState initialMouse, KeyboardState initialKeyboard)
    {
        _canvas = canvas;
        _font = new PixelFont(canvas);
        _quest = quest;
        _choose = choose;
        _cancel = cancel;
        _previousMouse = initialMouse;
        _previousKeyboard = initialKeyboard;

        var narrativeLineCount = Wrap(quest.Narrative, NarrativeColumns).Count;
        var nextY = Math.Max(PanelY + 230, _narrativeY + narrativeLineCount * 30 + 18);
        _choiceLayouts = quest.Choices.Take(4).Select((choice, index) =>
        {
            var lineCount = Wrap(choice.Description, ChoiceColumns).Count;
            var height = Math.Max(58, lineCount * 20 + 16);
            var layout = new ChoiceLayout(choice.Id, choice.Description,
                new Rectangle(ChoiceX, nextY, ChoiceWidth, height), nextY + 8);
            nextY += height + 10;
            return layout;
        }).ToArray();
        _footerY = nextY + 2;
        _panelHeight = Math.Max(306, _footerY + 32 - PanelY + 12);
    }

    public void Update(MouseState mouse, KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back))
        {
            _cancel();
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
            return;
        }

        for (var index = 0; index < _choiceLayouts.Length; index++)
        {
            var key = (Keys)((int)Keys.D1 + index);
            if (Pressed(keyboard, key) || index == 0 && Pressed(keyboard, Keys.Enter))
            {
                _choose(_choiceLayouts[index].Id);
                break;
            }
        }

        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (clicked)
        {
            var layout = _choiceLayouts.FirstOrDefault(choice => choice.Bounds.Contains(mouse.X, mouse.Y));
            if (layout is not null) _choose(layout.Id);
        }
        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    public void Draw()
    {
        _canvas.Rect(0, 0, 1280, 720, Color.Black * 0.48f);
        _canvas.Rect(PanelX, PanelY, PanelWidth, _panelHeight, Wood);
        _canvas.Frame(PanelX, PanelY, PanelWidth, _panelHeight, Bronze, 4);
        _canvas.Rect(PanelX + 10, PanelY + 10, PanelWidth - 20, _panelHeight - 20, Parchment);
        _canvas.Frame(PanelX + 10, PanelY + 10, PanelWidth - 20, _panelHeight - 20,
            new Color(85, 61, 33), 2);
        _font.Draw(_quest.Title, PanelX + 42, PanelY + 28, Ink, 3);
        _canvas.Rect(PanelX + 38, PanelY + 61, PanelWidth - 76, 3, Bronze);
        DrawWrapped(_quest.Narrative, PanelX + 44, _narrativeY, NarrativeColumns, 3, Ink, 3);

        for (var index = 0; index < _choiceLayouts.Length; index++)
        {
            var choice = _choiceLayouts[index];
            _canvas.Rect(choice.Bounds.X, choice.Bounds.Y, choice.Bounds.Width, choice.Bounds.Height,
                new Color(74, 56, 35));
            _canvas.Rect(choice.Bounds.X + 4, choice.Bounds.Y + 4,
                choice.Bounds.Width - 8, choice.Bounds.Height - 8, new Color(185, 165, 124));
            _canvas.Frame(choice.Bounds.X, choice.Bounds.Y, choice.Bounds.Width, choice.Bounds.Height, Bronze, 2);
            _font.Draw((index + 1) + ".", choice.Bounds.X + 18, choice.TextY + 6, Ink, 2);
            DrawWrapped(choice.Description, choice.Bounds.X + 68, choice.TextY,
                ChoiceColumns, 2, Ink, 2);
        }

        _font.Draw("НАЖМИТЕ НОМЕР ИЛИ ВЫБЕРИТЕ СТРОКУ МЫШЬЮ",
            PanelX + 88, _footerY, new Color(82, 62, 38), 1);
    }

    private void DrawWrapped(string text, int x, int y, int columns, int scale, Color color, int lineHeight)
    {
        var lines = Wrap(text, columns);
        for (var index = 0; index < lines.Count; index++)
            _font.Draw(lines[index], x, y + index * 10 * lineHeight, color, scale);
    }

    private static List<string> Wrap(string text, int columns)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > columns)
            {
                lines.Add(line);
                line = string.Empty;
            }
            line += (line.Length == 0 ? string.Empty : " ") + word;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    private bool Pressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private sealed record ChoiceLayout(string Id, string Description, Rectangle Bounds, int TextY);
}
