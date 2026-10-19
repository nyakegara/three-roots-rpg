using Microsoft.Xna.Framework.Input;

namespace Game.MonoGame.Screens;

internal interface IGameScreen
{
    void Update(MouseState mouse, KeyboardState keyboard, float elapsedSeconds);
    void Draw();
}
