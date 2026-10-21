using System.Runtime.CompilerServices;
using CampaignHero = Game.Core.Characters.Hero;
using Game.Core.Content;
using Game.Core.Economy;
using Game.MonoGame;
using Game.MonoGame.Screens;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Game.Tests;

public sealed class RecruitmentInputTests
{
    [Fact]
    public void HeldOpeningClick_DoesNotBuyUntilReleasedAndPressedAgain()
    {
        var held = Mouse(ButtonState.Pressed);
        var (screen, hero, offer) = Create(held, default);
        screen.Update(held, default, 0.016f);
        screen.Update(held, default, 0.016f);
        Assert.Equal(100, hero.Gold);
        Assert.Equal(5, offer.RemainingStock);
        screen.Update(Mouse(ButtonState.Released), default, 0.016f);
        screen.Update(held, default, 0.016f);
        screen.Update(held, default, 0.016f);
        Assert.Equal(90, hero.Gold);
        Assert.Equal(4, offer.RemainingStock);
    }

    [Fact]
    public void HeldOpeningEnter_DoesNotBuyUntilReleasedAndPressedAgain()
    {
        var held = new KeyboardState(Keys.Enter);
        var (screen, hero, _) = Create(default, held);
        screen.Update(default, held, 0.016f);
        Assert.Equal(100, hero.Gold);
        screen.Update(default, default, 0.016f);
        screen.Update(default, held, 0.016f);
        Assert.Equal(90, hero.Gold);
    }

    [Fact]
    public void HeldOpeningEscape_DoesNotCloseUntilReleasedAndPressedAgain()
    {
        var closed = 0;
        var held = new KeyboardState(Keys.Escape);
        var (screen, _, _) = Create(default, held, () => closed++);
        screen.Update(default, held, 0.016f);
        Assert.Equal(0, closed);
        screen.Update(default, default, 0.016f);
        screen.Update(default, held, 0.016f);
        Assert.Equal(1, closed);
    }

    private static MouseState Mouse(ButtonState left) => new(800, 580, 0, left,
        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static (RecruitmentScreen Screen, CampaignHero Hero, RecruitmentOffer Offer) Create(
        MouseState mouse, KeyboardState keyboard, Action? close = null)
    {
        var hero = new CampaignHero("test", "Лиара", 100);
        var offer = new RecruitmentOffer("flora.thorn-guard", 10, 5);
        var site = new RecruitmentSite("grove", [offer]);
        var catalog = UnitCatalog.Load(Path.Combine(AppContext.BaseDirectory, "content", "units", "units.json")).Catalog!;
        // Input does not access the GPU. Supply inert rendering dependencies without creating a window.
        var canvas = (PixelCanvas)RuntimeHelpers.GetUninitializedObject(typeof(PixelCanvas));
        var assets = (AssetCatalog)RuntimeHelpers.GetUninitializedObject(typeof(AssetCatalog));
        return (new RecruitmentScreen(canvas, hero, site, catalog, assets, "Роща", close ?? (() => { }),
            initialMouse: mouse, initialKeyboard: keyboard), hero, offer);
    }
}
