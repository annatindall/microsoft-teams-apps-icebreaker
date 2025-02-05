﻿// <copyright file="PairUpNotificationAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// </copyright>

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using global::AdaptiveCards;
    using global::AdaptiveCards.Templating;
    using Icebreaker.Properties;
    using Microsoft.Bot.Schema;
    using Microsoft.Bot.Schema.Teams;

    /// <summary>
    /// Builder class for the pairup notification card
    /// </summary>
    public class PairUpNotificationAdaptiveCard : AdaptiveCardBase
    {
        /// <summary>
        /// Default marker string in the UPN that indicates a user is externally-authenticated
        /// </summary>
        private const string ExternallyAuthenticatedUpnMarker = "#ext#";

        private static readonly Lazy<AdaptiveCardTemplate> AdaptiveCardTemplate =
            new Lazy<AdaptiveCardTemplate>(() => CardTemplateHelper.GetAdaptiveCardTemplate(AdaptiveCardName.PairUpNotification));

        /// <summary>
        /// Creates the pairup notification card.
        /// </summary>
        /// <param name="teamName">The team name.</param>
        /// <param name="sender">The user who will be sending this card.</param>
        /// <param name="recipients">The users who will be receiving this card.</param>
        /// <param name="botDisplayName">The bot display name.</param>
        /// <returns>Pairup notification card</returns>
        public static Attachment GetCard(string teamName, TeamsChannelAccount sender, List<TeamsChannelAccount> recipients, string botDisplayName)
        {
            // Set alignment of text based on default locale.
            var textAlignment = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft ? AdaptiveHorizontalAlignment.Right.ToString() : AdaptiveHorizontalAlignment.Left.ToString();

            // Guest users may not have their given name specified in AAD, so fall back to the full name if needed
            var senderGivenName = GetName(sender);

            var recipientUpns = new List<String> {};
            var recipientGivenNames = new List<String> {};
            var recipientNames = new List<String> {};
            foreach (TeamsChannelAccount recipient in recipients)
            {
                recipientGivenNames.Add(GetName(recipient));
                recipientNames.Add(recipient.Name);

                // To start a chat with a guest user, use their external email, not the UPN
                var recipientUpn = !IsGuestUser(recipient) ? recipient.UserPrincipalName : recipient.Email;
                recipientUpns.Add(recipientUpn);
            }

            var recipientUpnsString = string.Join(",", recipientUpns);

            var meetingTitle = string.Format(Resources.MeetupTitle, senderGivenName, string.Join(" / ", recipientGivenNames));
            var meetingContent = string.Format(Resources.MeetupContent, botDisplayName);
            var meetingLink = "https://teams.microsoft.com/l/meeting/new?subject=" + Uri.EscapeDataString(meetingTitle) + "&attendees=" + recipientUpnsString + "&content=" + Uri.EscapeDataString(meetingContent);

            var cardData = new
            {
                matchUpCardTitleContent = Resources.MatchUpCardTitleContent,
                matchUpCardMatchedText = recipients.Count > 1 ? string.Format(Resources.MatchUpCardMatchedTextMultiple, "\r\n- " + string.Join("\r\n- ", recipientNames)) : string.Format(Resources.MatchUpCardMatchedText, recipientGivenNames[0]),
                matchUpCardContentPart1 = string.Format(Resources.MatchUpCardContentPart1, botDisplayName, teamName),
                matchUpCardContentPart2 = Resources.MatchUpCardContentPart2,
                chatWithMatchButtonText = recipients.Count > 1 ? Resources.ChatWithGroupButtonText : string.Format(Resources.ChatWithMatchButtonText, recipientGivenNames[0]),
                chatWithMessageGreeting = Uri.EscapeDataString(Resources.ChatWithMessageGreeting),
                pauseMatchesButtonText = Resources.PausePairingsButtonText,
                proposeMeetupButtonText = Resources.ProposeMeetupButtonText,
                personUpn = recipientUpnsString,
                meetingLink,
                textAlignment,
            };

            return GetCard(AdaptiveCardTemplate.Value, cardData);
        }

        /// <summary>
        /// Checks whether or not the account is a guest user.
        /// </summary>
        /// <param name="account">The <see cref="TeamsChannelAccount"/> user to check.</param>
        /// <returns>True if the account is a guest user, false otherwise.</returns>
        private static bool IsGuestUser(TeamsChannelAccount account)
        {
            return account.UserPrincipalName.IndexOf(ExternallyAuthenticatedUpnMarker, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        private static string GetName(TeamsChannelAccount user)
        {
            // Guest users may not have their given name specified in AAD, so fall back to the full name if needed
            return string.IsNullOrEmpty(user.GivenName) ? user.Name : user.GivenName;
        }
    }
}