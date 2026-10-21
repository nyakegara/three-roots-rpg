using Game.Core.Campaigns;
using Game.Core.Characters;
using Game.Core.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

/// <summary>Confirmation over the world view; no battle state is created until the player accepts.</summary>
internal sealed class EncounterScreen : IGameScreen
{
    private readonly PixelCanvas _canvas;
    private readonly AssetCatalog _assets;
    private readonly PixelFont _font;
    private readonly UnitCatalog _units;
    private readonly Hero _hero;
    private readonly CampaignEncounter _encounter;
    private readonly Action _start;
    private readonly Action _cancel;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private string? _error;
    private static readonly Color Wood = new(45, 35, 24);
    private static readonly Color Gold = new(218, 190, 113);
    private static readonly Color Bronze = new(133, 107, 63);
    private static readonly Color Text = new(221, 211, 174);
    private static readonly Rectangle CancelButton = new(330, 550, 240, 36);
    private static readonly Rectangle FightButton = new(710, 550, 240, 36);

    public EncounterScreen(PixelCanvas canvas, AssetCatalog assets, UnitCatalog units,
        Hero hero, CampaignEncounter encounter, Action start, Action cancel,
        MouseState initialMouse, KeyboardState initialKeyboard)
    {
        _canvas = canvas;
        _assets = assets;
        _font = new PixelFont(canvas);
        _units = units;
        _hero = hero;
        _encounter = encounter;
        _start = start;
        _cancel = cancel;
        _previousMouse = initialMouse;
        _previousKeyboard = initialKeyboard;
    }

    public void ShowError() => _error = "НЕ УДАЛОСЬ НАЧАТЬ БОЙ. ПРОВЕРЬТЕ СОСТАВ АРМИИ.";

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape) ||
            clicked && CancelButton.Contains(mouse.Position)) _cancel();
        else if (_hero.Army.Stacks.Count > 0 &&
                 (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter) ||
                  clicked && FightButton.Contains(mouse.Position))) _start();
        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    public void Draw()
    {
        _canvas.Rect(0, 0, 1280, 720, Color.Black * 0.64f);
        _canvas.Rect(220, 100, 840, 508, Wood);
        _canvas.Frame(220, 100, 840, 508, Bronze, 3);
        _canvas.Frame(227, 107, 826, 494, Gold * 0.45f, 1);
        var title = Clip(_encounter.Name, 38);
        _font.Draw(title, 640 - title.Length * 9, 126, Gold, 3);
        _font.Draw("ВАША АРМИЯ", 260, 174, Gold, 2);
        _font.Draw("ПРОТИВНИК", 680, 174, Gold, 2);
        _canvas.Line(new Vector2(640, 196), new Vector2(640, 475), Bronze * 0.45f, 1);
        if (_hero.Army.Stacks.Count == 0)
        {
            _font.Draw("НЕТ ОТРЯДОВ", 260, 244, Text, 2);
            _font.Draw("СНАЧАЛА НАЙМИТЕ БОЙЦОВ", 260, 278, Gold, 1);
        }
        for (var i = 0; i < _hero.Army.Stacks.Count; i++)
        {
            var stack = _hero.Army.Stacks[i];
            DrawTroop(stack.UnitTypeId, stack.Count, 260, 204 + i * 54);
        }
        for (var i = 0; i < _encounter.EnemyArmy.Count; i++)
        {
            var stack = _encounter.EnemyArmy[i];
            DrawTroop(stack.UnitTypeId, stack.Count, 680, 204 + i * 54);
        }
        var reward = $"ЗА ПОБЕДУ: {_encounter.GoldReward} ЗОЛОТА И {_encounter.ExperienceReward} ОПЫТА";
        _font.Draw(reward, 640 - reward.Length * 6, 494, Gold, 2);
        var hint = _error ?? (_hero.Army.Stacks.Count == 0
            ? "БОЙ НЕДОСТУПЕН БЕЗ АРМИИ. ВЕРНИТЕСЬ К МЕСТУ НАЙМА."
            : "ПОГИБШИЕ БОЙЦЫ НЕ ВЕРНУТСЯ. ВЫЖИВШИЕ ПОСЛЕ БОЯ ПОЛНОСТЬЮ ЛЕЧАТСЯ.");
        _font.Draw(Clip(hint, 115), 640 - Math.Min(hint.Length, 115) * 3, 525, Text, 1);
        DrawButton(CancelButton, "ESC: ОТОЙТИ", true);
        DrawButton(FightButton, "ENTER: В БОЙ", _hero.Army.Stacks.Count > 0);
    }

    private void DrawTroop(string id, int count, int x, int y)
    {
        var definition = _units.Get(id)!;
        var icon = new Rectangle(x, y, 38, 44);
        if (!_assets.TryDrawCropped(_canvas, definition.ArtKey, icon))
        {
            // A small consistent emblem while human troop portraits are still placeholders.
            _canvas.Rect(x + 8, y + 4, 20, 31, new Color(79, 83, 60));
            _canvas.Frame(x + 8, y + 4, 20, 31, Bronze, 1);
            _font.Draw(id.EndsWith("longbow") ? "Л" : id.EndsWith("spearman") ? "К" : "О", x + 15, y + 13, Gold, 1);
        }
        _font.Draw(Clip(definition.Name + " X" + count, 25), x + 48, y + 7, Text, 2);
        _font.Draw($"ОЗ {definition.HitPoints}  УРОН {definition.Damage}  ХОД {definition.Movement}", x + 48, y + 31, Gold, 1);
    }

    private void DrawButton(Rectangle rect, string label, bool enabled)
    {
        _canvas.Rect(rect.X, rect.Y, rect.Width, rect.Height, enabled ? new Color(57, 44, 28) : new Color(37, 31, 24));
        _canvas.Frame(rect.X, rect.Y, rect.Width, rect.Height, enabled ? Gold : Bronze * 0.5f, 1);
        _font.Draw(label, rect.X + (rect.Width - label.Length * 12) / 2, rect.Y + 11, enabled ? Text : Bronze, 2);
    }

    private static string Clip(string text, int length) => text.Length <= length ? text : text[..(length - 2)] + "..";
}
