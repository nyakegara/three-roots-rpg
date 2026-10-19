using Game.Core.Characters;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Economy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

/// <summary>Classic wood-and-parchment interface for buying troops at one visited site.</summary>
internal sealed class RecruitmentScreen : IGameScreen
{
    private static readonly Color Wood = new(47, 33, 23);
    private static readonly Color DeepWood = new(31, 24, 19);
    private static readonly Color Bronze = new(143, 108, 57);
    private static readonly Color Gold = new(224, 193, 119);
    private static readonly Color Parchment = new(220, 207, 164);
    private static readonly Color Moss = new(76, 94, 47);

    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly Hero _hero;
    private readonly RecruitmentSite _site;
    private readonly UnitCatalog _units;
    private readonly EconomyRules _economy = new();
    private readonly Action _close;
    private readonly string _siteName;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private int _selectedOffer;
    private int _quantity = 1;
    private string _message = "ВЫБЕРИТЕ ОТРЯД И КОЛИЧЕСТВО";

    public RecruitmentScreen(PixelCanvas canvas, Hero hero, RecruitmentSite site,
        UnitCatalog units, string siteName, Action close)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        ArgumentNullException.ThrowIfNull(close);
        _canvas = canvas;
        _font = new PixelFont(canvas);
        _hero = hero;
        _site = site;
        _units = units;
        _siteName = siteName;
        _close = close;
        _selectedOffer = site.Offers.Count == 0 ? -1 : 0;
    }

    public void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds)
    {
        if (Pressed(keyboard, _previousKeyboard, Keys.Escape) ||
            Pressed(keyboard, _previousKeyboard, Keys.Back))
        {
            _close();
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            return;
        }

        if (Pressed(keyboard, _previousKeyboard, Keys.Up)) SelectOffer(_selectedOffer - 1);
        if (Pressed(keyboard, _previousKeyboard, Keys.Down)) SelectOffer(_selectedOffer + 1);
        if (Pressed(keyboard, _previousKeyboard, Keys.Left)) ChangeQuantity(-1);
        if (Pressed(keyboard, _previousKeyboard, Keys.Right)) ChangeQuantity(1);
        if (Pressed(keyboard, _previousKeyboard, Keys.Enter)) Buy();

        var clicked = mouse.LeftButton == ButtonState.Pressed &&
                      _previousMouse.LeftButton == ButtonState.Released;
        if (clicked)
        {
            if (mouse.X is >= 80 and < 770 && mouse.Y is >= 202 and < 462)
                SelectOffer((mouse.Y - 202) / 86);
            else if (Inside(mouse.X, mouse.Y, 800, 414, 54, 42)) ChangeQuantity(-1);
            else if (Inside(mouse.X, mouse.Y, 924, 414, 54, 42)) ChangeQuantity(1);
            else if (Inside(mouse.X, mouse.Y, 772, 568, 220, 62)) Buy();
            else if (Inside(mouse.X, mouse.Y, 1004, 568, 196, 62)) _close();
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    public void Draw()
    {
        _canvas.Rect(0, 0, 1280, 720, new Color(48, 59, 35));
        _canvas.Rect(36, 27, 1208, 666, DeepWood);
        _canvas.Frame(36, 27, 1208, 666, Bronze, 5);
        _canvas.Frame(46, 37, 1188, 646, new Color(89, 67, 40), 2);
        _canvas.Rect(52, 44, 1176, 66, Wood);
        _canvas.Rect(52, 44, 1176, 3, Gold);
        _font.Draw(_siteName.ToUpperInvariant(), 76, 61, Gold, 3);
        _font.Draw("МЕСТО НАЙМА", 1020, 68, Parchment, 1);

        DrawOfferList();
        DrawSelectedOffer();
        DrawArmy();

        _canvas.Rect(74, 544, 1128, 1, Bronze);
        _font.Draw(_message, 82, 562, _message.StartsWith("НАБРАНО", StringComparison.Ordinal) ? Gold : Parchment, 1);
        DrawButton(772, 568, 220, 62, "НАНЯТЬ", Gold);
        DrawButton(1004, 568, 196, 62, "НАЗАД", Parchment);
        _font.Draw("ESC — ОТМЕНА   /   ENTER — ПОКУПКА", 83, 652, new Color(172, 160, 124), 1);
    }

    private void DrawOfferList()
    {
        _canvas.Rect(72, 132, 686, 394, new Color(39, 32, 24));
        _canvas.Frame(72, 132, 686, 394, Bronze, 2);
        _font.Draw("ОТРЯДЫ РОЩИ", 91, 151, Gold, 2);
        for (var index = 0; index < _site.Offers.Count; index++)
        {
            var offer = _site.Offers[index];
            var unit = _units.Get(offer.UnitTypeId);
            var y = 202 + index * 86;
            var selected = index == _selectedOffer;
            _canvas.Rect(84, y, 660, 74, selected ? new Color(69, 59, 38) : new Color(48, 39, 29));
            _canvas.Frame(84, y, 660, 74, selected ? Gold : new Color(87, 68, 42), selected ? 2 : 1);
            DrawUnitMark(121, y + 37, offer.UnitTypeId);
            _font.Draw(unit?.Name ?? offer.UnitTypeId, 159, y + 10, Parchment, 2);
            _font.Draw(Role(unit), 159, y + 39, new Color(173, 160, 124), 1);
            _font.Draw($"ОСТАЛОСЬ {offer.RemainingStock}", 570, y + 16, Gold, 1);
            var price = _economy.GetAdjustedUnitCost(_hero, offer);
            _font.Draw(price is null ? "ЦЕНА —" : $"{price} ЗА ШТ.", 570, y + 39, Parchment, 1);
        }
    }

    private void DrawSelectedOffer()
    {
        _canvas.Rect(778, 132, 424, 394, new Color(39, 32, 24));
        _canvas.Frame(778, 132, 424, 394, Bronze, 2);
        _font.Draw("ВАШ ВЫБОР", 799, 151, Gold, 2);
        if (_selectedOffer < 0 || _selectedOffer >= _site.Offers.Count) return;

        var offer = _site.Offers[_selectedOffer];
        var unit = _units.Get(offer.UnitTypeId);
        DrawUnitMark(848, 249, offer.UnitTypeId, large: true);
        _font.Draw(unit?.Name ?? offer.UnitTypeId, 913, 207, Parchment, 2);
        _font.Draw(Role(unit), 913, 234, new Color(174, 160, 123), 1);
        _font.Draw(unit is null ? "" : $"ЗДОРОВЬЕ {unit.HitPoints}   УРОН {unit.Damage}", 913, 255,
            new Color(193, 181, 142), 1);
        _canvas.Rect(799, 301, 382, 1, new Color(100, 78, 46));
        _font.Draw("КОЛИЧЕСТВО", 800, 324, new Color(176, 161, 121), 1);
        DrawButton(800, 414, 54, 42, "-", Parchment);
        _canvas.Rect(862, 414, 54, 42, new Color(32, 27, 20));
        _canvas.Frame(862, 414, 54, 42, Bronze, 2);
        _font.Draw(_quantity.ToString(), 882, 427, Gold, 2);
        DrawButton(924, 414, 54, 42, "+", Parchment);
        var unitCost = _site.Offers[_selectedOffer] is { } selectedOffer
            ? _economy.GetAdjustedUnitCost(_hero, selectedOffer) : null;
        var total = unitCost is null ? 0L : (long)unitCost.Value * _quantity;
        _font.Draw($"ИТОГО: {(unitCost is null ? "---" : total.ToString())} ЗОЛОТА", 990, 423, Gold, 1);
        _font.Draw($"ЗОЛОТО ГЕРОЯ: {_hero.Gold}", 800, 481, Parchment, 1);
        _font.Draw($"РЕПУТАЦИЯ: {_hero.Reputation:+#;-#;0}", 800, 500, new Color(172, 160, 124), 1);
    }

    private void DrawArmy()
    {
        _font.Draw($"АРМИЯ  {_hero.Army.Stacks.Count}/{Army.MaximumStacks}", 899, 322, Gold, 1);
        for (var slot = 0; slot < Army.MaximumStacks; slot++)
        {
            var x = 897 + slot * 55;
            _canvas.Rect(x, 346, 48, 52, new Color(33, 28, 21));
            _canvas.Frame(x, 346, 48, 52,
                slot < _hero.Army.Stacks.Count ? Bronze : new Color(78, 67, 47), 1);
            if (slot >= _hero.Army.Stacks.Count) continue;
            var stack = _hero.Army.Stacks[slot];
            DrawUnitMark(x + 24, 366, stack.UnitTypeId);
            _font.Draw(stack.Count.ToString(), x + 14, 382, Parchment, 1);
        }
    }

    private void SelectOffer(int index)
    {
        if (_site.Offers.Count == 0) return;
        _selectedOffer = Math.Clamp(index, 0, _site.Offers.Count - 1);
        _quantity = 1;
        _message = "ВЫБЕРИТЕ ОТРЯД И КОЛИЧЕСТВО";
    }

    private void ChangeQuantity(int amount)
    {
        if (_selectedOffer < 0 || _selectedOffer >= _site.Offers.Count) return;
        var stock = _site.Offers[_selectedOffer].RemainingStock;
        if (stock == 0) { _quantity = 0; return; }
        _quantity = Math.Clamp(_quantity + amount, 1, stock);
        _message = "ВЫБЕРИТЕ ОТРЯД И КОЛИЧЕСТВО";
    }

    private void Buy()
    {
        if (_selectedOffer < 0 || _selectedOffer >= _site.Offers.Count) return;
        var offer = _site.Offers[_selectedOffer];
        if (_quantity <= 0)
        {
            _message = "ЭТО ПРЕДЛОЖЕНИЕ ЗАКОНЧИЛОСЬ";
            return;
        }
        var result = _economy.TryRecruit(_hero, _site, offer.UnitTypeId, _quantity);
        _message = result.Success
            ? $"НАБРАНО: {_units.Get(offer.UnitTypeId)?.Name ?? offer.UnitTypeId} X {_quantity}"
            : result.Message.ToUpperInvariant();
        if (result.Success)
            _quantity = offer.RemainingStock == 0 ? 0 : Math.Min(1, offer.RemainingStock);
    }

    private void DrawUnitMark(int centerX, int centerY, string unitId, bool large = false)
    {
        var radius = large ? 31 : 22;
        var accent = unitId switch
        {
            "flora.sprout" => new Color(211, 159, 191),
            "flora.thorn-guard" => new Color(119, 157, 68),
            "flora.ember-bloom" => new Color(212, 117, 59),
            "flora.pod-slinger" => new Color(134, 149, 76),
            "flora.root-warden" => new Color(155, 139, 165),
            _ => new Color(123, 133, 75)
        };
        FantasyArt.Oval(_canvas, centerX, centerY + radius / 2, radius + 8, radius / 2, new Color(27, 24, 18));
        _canvas.Rect(centerX - radius, centerY - radius, radius * 2, radius * 2, new Color(36, 45, 29));
        _canvas.Frame(centerX - radius, centerY - radius, radius * 2, radius * 2,
            new Color(106, 82, 49), 2);
        if (unitId == "flora.thorn-guard")
        {
            FantasyArt.Oval(_canvas, centerX, centerY + 2, radius - 5, radius + 2, accent);
            _canvas.Rect(centerX - 3, centerY - radius - 4, 6, 9, new Color(82, 117, 48));
        }
        else
        {
            for (var petal = 0; petal < 6; petal++)
            {
                var dx = (petal % 3 - 1) * radius / 2;
                var dy = (petal / 3 == 0 ? -1 : 1) * radius / 3;
                FantasyArt.Oval(_canvas, centerX + dx, centerY + dy, radius / 2 + 4, radius / 2 + 2, accent);
            }
            FantasyArt.Oval(_canvas, centerX, centerY, radius / 2, radius / 2, new Color(214, 184, 105));
        }
    }

    private void DrawButton(int x, int y, int width, int height, string label, Color textColor)
    {
        _canvas.Rect(x, y, width, height, Wood);
        _canvas.Frame(x, y, width, height, Bronze, 2);
        _canvas.Rect(x + 3, y + 3, width - 6, 2, new Color(180, 142, 78) * 0.48f);
        _font.Draw(label, x + Math.Max(8, (width - label.Length * 12) / 2), y + (height - 14) / 2,
            textColor, 2);
    }

    private static string Role(UnitDefinition? unit) => unit is null ? "" :
        unit.Ability switch
        {
            UnitAbility.HealAlly => "ДАЛЬНИЙ БОЙ / ЛЕЧЕНИЕ",
            UnitAbility.ConvertOnHit => "ДАЛЬНИЙ БОЙ / СПОРОВЫЙ МОРОК",
            _ when unit.IsRanged => "ДАЛЬНИЙ БОЙ",
            _ => "БЛИЖНИЙ БОЙ"
        };

    private static bool Pressed(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && !previous.IsKeyDown(key);

    private static bool Inside(int px, int py, int x, int y, int width, int height) =>
        px >= x && px < x + width && py >= y && py < y + height;
}
