using System.Collections.ObjectModel;

namespace Game.Core.Economy;

/// <summary>A recruitment location with finite supplies of its listed unit types.</summary>
public sealed class RecruitmentSite
{
    private readonly Dictionary<string, RecruitmentOffer> _offers;
    private readonly ReadOnlyCollection<RecruitmentOffer> _offerView;

    public string Id { get; }
    public string? FactionId { get; }
    public IReadOnlyList<RecruitmentOffer> Offers => _offerView;

    public RecruitmentSite(string id, IEnumerable<RecruitmentOffer> offers, string? factionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(offers);
        Id = id;
        FactionId = string.IsNullOrWhiteSpace(factionId) ? null : factionId;
        _offers = new Dictionary<string, RecruitmentOffer>(StringComparer.Ordinal);
        foreach (var offer in offers)
        {
            ArgumentNullException.ThrowIfNull(offer);
            if (!_offers.TryAdd(offer.UnitTypeId, offer))
            {
                throw new ArgumentException($"Recruitment site contains duplicate unit type '{offer.UnitTypeId}'.", nameof(offers));
            }
        }

        _offerView = Array.AsReadOnly(_offers.Values.ToArray());
    }

    public RecruitmentOffer? GetOffer(string unitTypeId) =>
        string.IsNullOrWhiteSpace(unitTypeId) ? null : _offers.GetValueOrDefault(unitTypeId);
}
