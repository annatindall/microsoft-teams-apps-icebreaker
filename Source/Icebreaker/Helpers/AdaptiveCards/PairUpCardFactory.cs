
namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;

    /// <summary>
    /// Builder class for the pairup notification card
    /// </summary>
    public class PairUpCardFactory
    {
        public static Attachment GetCard(string teamName, TeamsChannelAccount sender, List<TeamsChannelAccount> group, string botDisplayName)
        {
            if (group.Count > 1)
            {
                return PairUpNotificationAdaptiveCard.GetCard(teamName, sender, group, botDisplayName);
            }
            else
            {
                return NoPairNotificationAdaptiveCard(teamName, sender, botDisplayName);
            }
        }
    }
}
