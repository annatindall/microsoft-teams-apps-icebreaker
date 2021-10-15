namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using Microsoft.Bot.Schema;
    using Microsoft.Bot.Schema.Teams;

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
                return NoPairNotificationAdaptiveCard.GetCard(teamName, sender, botDisplayName);
            }
        }
    }
}
