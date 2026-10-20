using Game.Core.Combat;
using Microsoft.Xna.Framework;

namespace Game.MonoGame.Rendering;

/// <summary>Short pixel-art feedback for an attack already resolved by the battle rules.</summary>
internal sealed class BattleAttackEffect
{
    private const float Windup = 0.14f;
    private const float Flight = 0.42f;
    private const float Duration = 0.8f;
    private readonly Vector2 _from;
    private readonly Vector2 _to;
    private readonly Kind _kind;
    private float _elapsed;

    public bool IsFinished => _elapsed >= Duration;

    public BattleAttackEffect(BattleUnit attacker, Vector2 from, Vector2 to, bool healing = false)
    {
        _from = from + new Vector2(0, -20);
        _to = to + new Vector2(0, -20);
        _kind = healing ? Kind.HealingLeaf :
            attacker.AttackMode == AttackMode.HolyFire && attacker.Ability == UnitAbility.RadiantModes
            ? Kind.HolyFire
            : attacker.UnitTypeId switch
            {
                "human.longbow" => Kind.Arrow,
                "flora.sprout" => Kind.Vine,
                "flora.ember-bloom" => Kind.Ember,
                "flora.root-warden" => Kind.Spore,
                _ => attacker.IsRanged ? Kind.Arrow : Kind.Melee
            };
    }

    public void Advance(float seconds) => _elapsed = Math.Min(Duration, _elapsed + Math.Clamp(seconds, 0f, 0.1f));

    public void Draw(PixelCanvas canvas)
    {
        if (_elapsed < Windup || IsFinished) return;
        var travel = Math.Clamp((_elapsed - Windup) / Flight, 0f, 1f);
        if (travel < 1f)
        {
            var point = Vector2.Lerp(_from, _to, travel);
            var direction = Vector2.Normalize(_to - _from);
            if (!float.IsFinite(direction.X)) direction = Vector2.UnitX;
            var across = new Vector2(-direction.Y, direction.X);
            if (_kind == Kind.Melee)
            {
                var swing = MathHelper.Lerp(-0.8f, 0.8f, travel);
                var slash = new Vector2(MathF.Cos(swing), MathF.Sin(swing));
                var center = Vector2.Lerp(point, _to, 0.65f);
                canvas.Line(center - slash * 12, center + slash * 12, new Color(48, 37, 28), 6);
                canvas.Line(center - slash * 10, center + slash * 10, new Color(230, 218, 177), 3);
                return;
            }
            var tail = point - direction * 28;
            var farTail = point - direction * 42;
            using (canvas.PushTint(Color.White))
            {
                switch (_kind)
                {
                    case Kind.Arrow:
                        canvas.Line(tail, point - direction * 6, new Color(41, 33, 24), 5);
                        canvas.Line(tail, point - direction * 6, new Color(179, 138, 82), 2);
                        canvas.Polygon([point + direction * 5, point - direction * 8 + across * 5,
                            point - direction * 8 - across * 5], new Color(54, 54, 47));
                        canvas.Polygon([point + direction * 3, point - direction * 6 + across * 3,
                            point - direction * 6 - across * 3], new Color(210, 205, 169));
                        canvas.Line(tail, tail - direction * 7 + across * 7, new Color(220, 204, 158), 2);
                        canvas.Line(tail, tail - direction * 7 - across * 7, new Color(220, 204, 158), 2);
                        break;
                    case Kind.Vine:
                        canvas.Line(tail, point, new Color(31, 61, 30), 6);
                        canvas.Line(tail, point, new Color(126, 174, 78), 3);
                        canvas.Rect((int)point.X - 4, (int)point.Y - 4, 8, 8, new Color(155, 203, 99));
                        canvas.Rect((int)farTail.X - 2, (int)farTail.Y - 2, 5, 5, new Color(91, 139, 65));
                        break;
                    case Kind.HealingLeaf:
                        canvas.Rect((int)point.X - 5, (int)point.Y - 3, 10, 6, new Color(41, 88, 43));
                        canvas.Rect((int)point.X - 3, (int)point.Y - 5, 7, 9, new Color(163, 202, 102));
                        canvas.Line(tail, point, new Color(113, 165, 87), 2);
                        break;
                    case Kind.Ember:
                        canvas.Rect((int)farTail.X - 2, (int)farTail.Y - 2, 5, 5, new Color(156, 65, 34));
                        canvas.Rect((int)tail.X - 3, (int)tail.Y - 3, 7, 7, new Color(202, 83, 37));
                        canvas.Polygon([point + direction * 10, point + across * 7,
                            point - direction * 11 + across * 5, point - direction * 6,
                            point - direction * 11 - across * 5, point - across * 7], new Color(92, 41, 25));
                        canvas.Polygon([point + direction * 8, point + across * 5,
                            point - direction * 8 + across * 3, point - direction * 4,
                            point - direction * 8 - across * 3, point - across * 5], new Color(227, 103, 43));
                        canvas.Line(point - direction * 5, point + direction * 5, new Color(250, 200, 88), 4);
                        break;
                    case Kind.Spore:
                        canvas.Rect((int)point.X - 6, (int)point.Y - 6, 12, 12, new Color(70, 51, 99));
                        canvas.Rect((int)point.X - 4, (int)point.Y - 4, 8, 8, new Color(183, 151, 194));
                        canvas.Rect((int)tail.X - 2, (int)tail.Y - 2, 5, 5, new Color(130, 105, 158));
                        canvas.Rect((int)farTail.X, (int)farTail.Y, 3, 3, new Color(159, 131, 181));
                        break;
                    case Kind.HolyFire:
                        canvas.Line(tail, point, new Color(153, 104, 47), 5);
                        canvas.Rect((int)point.X - 6, (int)point.Y - 6, 12, 12, new Color(194, 140, 62));
                        canvas.Rect((int)point.X - 4, (int)point.Y - 4, 8, 8, new Color(255, 227, 148));
                        canvas.Rect((int)farTail.X - 2, (int)farTail.Y - 2, 5, 5, new Color(219, 186, 119));
                        break;
                }
            }
            return;
        }

        var impact = Math.Clamp((_elapsed - Windup - Flight) / (Duration - Windup - Flight), 0f, 1f);
        var x = (int)_to.X;
        var y = (int)_to.Y;
        var radius = 8 + (int)(impact * 19);
        var color = _kind switch
        {
            Kind.Vine => new Color(142, 189, 89),
            Kind.HealingLeaf => new Color(173, 211, 114),
            Kind.Ember => new Color(239, 135, 60),
            Kind.Spore => new Color(181, 146, 198),
            Kind.HolyFire => new Color(247, 214, 130),
            Kind.Arrow => new Color(223, 208, 162),
            _ => new Color(205, 191, 152)
        };
        var fade = 1f - impact;
        canvas.Rect(x - 7, y - 7, 14, 14, color * fade);
        for (var index = 0; index < (_kind == Kind.Arrow ? 5 : 8); index++)
        {
            var angle = index * MathHelper.TwoPi / (_kind == Kind.Arrow ? 5f : 8f);
            var dx = (int)(MathF.Cos(angle) * radius);
            var dy = (int)(MathF.Sin(angle) * radius);
            var size = _kind == Kind.Arrow ? 4 : 6;
            canvas.Line(new Vector2(x + dx / 2, y + dy / 2), new Vector2(x + dx, y + dy), color * fade, size);
        }
        if (_kind is Kind.Ember or Kind.HolyFire)
            canvas.Line(new Vector2(x, y + 2), new Vector2(x, y - radius - 8), color * fade, 3);
        if (_kind == Kind.Spore)
            canvas.Rect(x - radius - 6, y - radius / 2, 4, 4, color * fade);
    }

    private enum Kind { Melee, Arrow, Vine, HealingLeaf, Ember, Spore, HolyFire }
}
