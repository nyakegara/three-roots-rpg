using Game.Core.Campaigns;
using Game.Core.Economy;

namespace Game.MonoGame;

/// <summary>Buys a reproducible party with real gold and stock only for explicit screenshot fixtures.</summary>
public static class CampaignCaptureSetup
{
    public static void RecruitParty(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var economy = new EconomyRules();
        Buy("starting-clearing", "flora.thorn-guard", 8);
        Buy("starting-clearing", "flora.sprout", 4);

        void Buy(string siteId, string unitId, int count)
        {
            var site = campaign.RecruitmentSites.Single(candidate => candidate.Id == siteId);
            var result = economy.TryRecruit(campaign.Hero, site, unitId, count);
            if (!result.Success) throw new InvalidOperationException("Cannot prepare capture party: " + result.Message);
        }
    }
}
