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
    private static readonly Color ParchmentFace = new(186, 163, 111);
    private static readonly Color ParchmentLight = new(208, 189, 143);
    private static readonly Color Ink = new(57, 43, 27);
    private static readonly Color SoftInk = new(87, 69, 42);

    private readonly PixelCanvas _canvas;
    private readonly PixelFont _font;
    private readonly AssetCatalog _assets;
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
        UnitCatalog units, AssetCatalog assets, string siteName, Action close)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        ArgumentNullException.ThrowIfNull(close);
        _canvas = canvas;
        _font = new PixelFont(canvas);
        _assets = assets;
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
            var quantityDelta = RecruitmentLayout.QuantityDeltaAt(mouse.X, mouse.Y);
            if (quantityDelta != 0) ChangeQuantity(quantityDelta);
            else if (mouse.X is >= 80 and < 770 && mouse.Y is >= 202 and < 462)
                SelectOffer((mouse.Y - 202) / 86);
            else if (Inside(mouse.X, mouse.Y, 772, 568, 220, 62)) Buy();
            else if (Inside(mouse.X, mouse.Y, 1004, 568, 196, 62)) _close();
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
    }

    public void Draw()
    {
        _canvas.Rect(0, 0, 1280, 720, new Color(48, 59, 35));
        _canvas.Rect(36, 27, 1208, 666, Wood);
        _canvas.Frame(36, 27, 1208, 666, Bronze, 5);
        _canvas.Frame(46, 37, 1188, 646, new Color(89, 67, 40), 2);
        DrawWoodGrain(52, 44, 1176, 66);
        _canvas.Rect(52, 44, 1176, 3, Gold * 0.78f);
        _canvas.Rect(52, 107, 1176, 2, DeepWood);
        _font.Draw(_siteName.ToUpperInvariant(), 76, 61, Gold, 3);
        _font.Draw("МЕСТО НАЙМА", 1020, 68, Parchment, 1);

        DrawOfferList();
        DrawSelectedOffer();

        _canvas.Rect(74, 544, 1128, 1, Bronze);
        _font.Draw(_message, 82, 562, _message.StartsWith("НАБРАНО", StringComparison.Ordinal) ? Gold : Parchment, 1);
        DrawButton(772, 568, 220, 62, "НАНЯТЬ", Gold);
        DrawButton(1004, 568, 196, 62, "НАЗАД", Parchment);
        _font.Draw("ESC — ОТМЕНА   /   ENTER — ПОКУПКА", 83, 652, new Color(172, 160, 124), 1);
    }

    private void DrawOfferList()
    {
        DrawParchmentPanel(72, 132, 686, 394);
        _font.Draw("ОТРЯДЫ РОЩИ", 91, 151, Ink, 2);
        for (var index = 0; index < _site.Offers.Count; index++)
        {
            var offer = _site.Offers[index];
            var unit = _units.Get(offer.UnitTypeId);
            var y = 202 + index * 86;
            var selected = index == _selectedOffer;
            _canvas.Rect(84, y, 660, 74, selected ? new Color(118, 128, 73) : ParchmentLight);
            _canvas.Frame(84, y, 660, 74, selected ? Gold : new Color(127, 101, 58), selected ? 2 : 1);
            DrawUnitSprite(unit, offer.UnitTypeId, new Rectangle(97, y + 5, 48, 64));
            _font.Draw(unit?.Name ?? offer.UnitTypeId, 159, y + 10,
                selected ? Parchment : Ink, 2);
            _font.Draw(Role(unit), 159, y + 39,
                selected ? new Color(221, 209, 167) : SoftInk, 1);
            _font.Draw($"ОСТАЛОСЬ {offer.RemainingStock}", 570, y + 16,
                selected ? Gold : Ink, 1);
            var price = _economy.GetAdjustedUnitCost(_hero, offer);
            _font.Draw(price is null ? "ЦЕНА —" : $"{price} ЗА ШТ.", 570, y + 39,
                selected ? Parchment : SoftInk, 1);
        }
    }

    private void DrawSelectedOffer()
    {
        DrawParchmentPanel(778, 132, 424, 394);
        _font.Draw("ВАШ ВЫБОР", 799, 151, Ink, 2);
        if (_selectedOffer < 0 || _selectedOffer >= _site.Offers.Count) return;

        var offer = _site.Offers[_selectedOffer];
        var unit = _units.Get(offer.UnitTypeId);
        DrawUnitSprite(unit, offer.UnitTypeId, new Rectangle(812, 194, 72, 96));
        _font.Draw(unit?.Name ?? offer.UnitTypeId, 913, 207, Ink, 2);
        _font.Draw(Role(unit), 913, 234, SoftInk, 1);
        _font.Draw(unit is null ? "" : $"ЗДОРОВЬЕ {unit.HitPoints}   УРОН {unit.Damage}", 913, 255,
            SoftInk, 1);
        _canvas.Rect(799, 301, 382, 1, new Color(133, 108, 67));
        _font.Draw("КОЛИЧЕСТВО", 800, 324, Ink, 1);
        var minusBounds = RecruitmentLayout.QuantityMinusBounds;
        DrawButton(minusBounds.X, minusBounds.Y, minusBounds.Width, minusBounds.Height, "-", Parchment);
        var quantityBounds = RecruitmentLayout.QuantityValueBounds;
        _canvas.Rect(quantityBounds.X, quantityBounds.Y, quantityBounds.Width, quantityBounds.Height, new Color(54, 43, 29));
        _canvas.Frame(quantityBounds.X, quantityBounds.Y, quantityBounds.Width, quantityBounds.Height, Bronze, 2);
        _font.Draw(_quantity.ToString(), quantityBounds.X + 20, quantityBounds.Y + 11, Gold, 2);
        var plusBounds = RecruitmentLayout.QuantityPlusBounds;
        DrawButton(plusBounds.X, plusBounds.Y, plusBounds.Width, plusBounds.Height, "+", Parchment);
        var unitCost = _site.Offers[_selectedOffer] is { } selectedOffer
            ? _economy.GetAdjustedUnitCost(_hero, selectedOffer) : null;
        var total = unitCost is null ? 0L : (long)unitCost.Value * _quantity;
        _font.Draw($"ИТОГО: {(unitCost is null ? "---" : total.ToString())} ЗОЛОТА", 990, 359, Ink, 1);
        _font.Draw($"АРМИЯ {_hero.Army.Stacks.Count}/{Army.MaximumStacks}", 800, 408, Ink, 1);
        DrawArmy();
        _font.Draw($"ЗОЛОТО ГЕРОЯ: {_hero.Gold}", 800, 493, Ink, 1);
        _font.Draw($"РЕПУТАЦИЯ: {_hero.Reputation:+#;-#;0}", 1010, 493, SoftInk, 1);
    }

    private void DrawArmy()
    {
        for (var slot = 0; slot < Army.MaximumStacks; slot++)
        {
            var x = 800 + slot * 74;
            _canvas.Rect(x, 427, 68, 60, new Color(72, 59, 38));
            _canvas.Frame(x, 427, 68, 60,
                slot < _hero.Army.Stacks.Count ? Bronze : new Color(116, 93, 56), 2);
            if (slot >= _hero.Army.Stacks.Count) continue;
            var stack = _hero.Army.Stacks[slot];
            DrawUnitSprite(_units.Get(stack.UnitTypeId), stack.UnitTypeId,
                new Rectangle(x + 16, 429, 36, 38));
            _font.Draw(stack.Count.ToString(), x + 28, 470, Parchment, 1);
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

    private void DrawUnitSprite(UnitDefinition? unit, string fallbackId, Rectangle destination)
    {
        if (unit is not null && _assets.TryDrawCropped(_canvas, unit.ArtKey, destination)) return;
        DrawUnitMark(destination.Center.X, destination.Center.Y, fallbackId);
    }

    private void DrawParchmentPanel(int x, int y, int width, int height)
    {
        _canvas.Rect(x, y, width, height, Wood);
        _canvas.Frame(x, y, width, height, Bronze, 2);
        _canvas.Rect(x + 6, y + 6, width - 12, height - 12, ParchmentFace);
        _canvas.Rect(x + 7, y + 7, width - 14, 1, ParchmentLight);
        _canvas.Rect(x + 7, y + height - 8, width - 14, 1, new Color(139, 115, 73));
        for (var row = y + 18; row < y + height - 16; row += 39)
        {
            _canvas.Rect(x + 14, row, 14, 1, new Color(217, 198, 151) * 0.42f);
            _canvas.Rect(x + width - 28, row + 4, 12, 1, new Color(155, 132, 88) * 0.36f);
        }
    }

    private void DrawWoodGrain(int x, int y, int width, int height)
    {
        _canvas.Rect(x + 12, y + height - 7, width - 24, 1, new Color(103, 75, 42) * 0.55f);
        _canvas.Rect(x + 20, y + height - 4, width - 40, 1, new Color(188, 148, 83) * 0.38f);
        _canvas.Rect(x + 22, y + 12, 15, 1, new Color(182, 139, 75) * 0.30f);
        _canvas.Rect(x + width - 49, y + 25, 21, 1, new Color(182, 139, 75) * 0.24f);
        _canvas.Rect(x + width / 2, y + 39, 27, 1, new Color(90, 65, 37) * 0.25f);
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
