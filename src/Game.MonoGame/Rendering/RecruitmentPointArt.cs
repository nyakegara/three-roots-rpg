namespace Game.MonoGame.Rendering;

/// <summary>Maps campaign recruitment-site identities to optional exploration-map art.</summary>
public static class RecruitmentPointArt
{
    public const string RootGreenhouse = "poi.flora.root-greenhouse";
    public const string SunnyClearing = "poi.flora.sunny-clearing";
    public const string SporeCircle = "poi.flora.spore-circle";

    public static string? GetAssetKey(string? recruitmentSiteId) => recruitmentSiteId?.ToLowerInvariant() switch
    {
        "root-greenhouse" => RootGreenhouse,
        "sunny-clearing" => SunnyClearing,
        "spore-circle" => SporeCircle,
        _ => null
    };
}
