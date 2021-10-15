// <copyright file="NoMatchNotificationAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// </copyright>

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Globalization;
    using global::AdaptiveCards;
    using global::AdaptiveCards.Templating;
    using Icebreaker.Properties;
    using Microsoft.Bot.Schema;
    using Microsoft.Bot.Schema.Teams;

    /// <summary>
    /// Builder class for the no matchup notification card
    /// </summary>
    public class NoMatchNotificationAdaptiveCard : AdaptiveCardBase
    {
        /// <summary>
        /// Default marker string in the UPN that indicates a user is externally-authenticated
        /// </summary>
        private const string ExternallyAuthenticatedUpnMarker = "#ext#";

        private static readonly Lazy<AdaptiveCardTemplate> AdaptiveCardTemplate =
            new Lazy<AdaptiveCardTemplate>(() => CardTemplateHelper.GetAdaptiveCardTemplate(AdaptiveCardName.NoMatchNotification));

        /// <summary>
        /// Creates the matchup notification card.
        /// </summary>
        /// <param name="teamName">The team name.</param>
        /// <param name="sender">The user who will be sending this card.</param>
        /// <param name="botDisplayName">The bot display name.</param>
        /// <returns>No matchup notification card</returns>
        public static Attachment GetCard(string teamName, TeamsChannelAccount sender, string botDisplayName)
        {
            // Set alignment of text based on default locale.
            var textAlignment = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft ? AdaptiveHorizontalAlignment.Right.ToString() : AdaptiveHorizontalAlignment.Left.ToString();

            // Guest users may not have their given name specified in AAD, so fall back to the full name if needed
            var senderGivenName = string.IsNullOrEmpty(sender.GivenName) ? sender.Name : sender.GivenName;
            var cardData = new
            {
                noMatchUpCardTitleContent = "Sorry, no matches this time", // Resources.NoMatchUpCardTitleContent,
                noMatchUpCardContent = $"Hi there again, I'm {botDisplayName} in {teamName}. A bot that groups you with new coworkers to meet each week. You didn't get matched to a group this round, but hopefully I'll help you meet people next time! ", // string.Format(Resources.NoMatchUpCardContent, botDisplayName, teamName),
                pauseMatchesButtonText = Resources.PauseMatchupsButtonText,
                textAlignment,
            };

            return GetCard(AdaptiveCardTemplate.Value, cardData);
        }
    }
}