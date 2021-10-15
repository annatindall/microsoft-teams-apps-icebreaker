// <copyright file="MatchingService.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// </copyright>

namespace Icebreaker.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Icebreaker.Helpers;
    using Icebreaker.Helpers.AdaptiveCards;
    using Icebreaker.Interfaces;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Azure;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Schema;
    using Microsoft.Bot.Schema.Teams;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Implements the core logic for Icebreaker bot
    /// </summary>
    public class MatchingService : IMatchingService
    {
        private readonly IBotDataProvider dataProvider;
        private readonly ConversationHelper conversationHelper;
        private readonly TelemetryClient telemetryClient;
        private readonly BotAdapter botAdapter;
        private readonly int maxPairUpsPerTeam;
        private readonly int groupSize;
        private readonly string botDisplayName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchingService"/> class.
        /// </summary>
        /// <param name="dataProvider">The data provider to use</param>
        /// <param name="conversationHelper">Conversation helper instance to notify team members</param>
        /// <param name="telemetryClient">The telemetry client to use</param>
        /// <param name="botAdapter">Bot adapter.</param>
        public MatchingService(IBotDataProvider dataProvider, ConversationHelper conversationHelper, TelemetryClient telemetryClient, BotAdapter botAdapter)
        {
            this.dataProvider = dataProvider;
            this.conversationHelper = conversationHelper;
            this.telemetryClient = telemetryClient;
            this.botAdapter = botAdapter;
            this.maxPairUpsPerTeam = Convert.ToInt32(CloudConfigurationManager.GetSetting("MaxPairUpsPerTeam"));
            this.groupSize = Convert.ToInt32(CloudConfigurationManager.GetSetting("GroupSize"));
            this.botDisplayName = CloudConfigurationManager.GetSetting("BotDisplayName");
        }

        /// <summary>
        /// Generate groups and send notifications.
        /// </summary>
        /// <returns>The number of groups that were made</returns>
        public async Task<int> MakeGroupsAndNotifyAsync()
        {
            this.telemetryClient.TrackTrace("Making groups");

            // Recall all the teams where we have been added
            // For each team where bot has been added:
            //     Pull the roster of the team
            //     Remove the members who have opted out of groups
            //     Match each member with others
            //     Save this group
            // Now notify each group found and ask them to reach out to the group
            // When contacting the user, give them the button to opt-out
            var installedTeamsCount = 0;
            var groupsNotifiedCount = 0;
            var usersNotifiedCount = 0;
            var dbMembersCount = 0;

            try
            {
                var teams = await this.dataProvider.GetInstalledTeamsAsync();
                installedTeamsCount = teams.Count;
                this.telemetryClient.TrackTrace($"Generating pairs for {installedTeamsCount} teams");

                // Fetch all db users opt-in status/lookup
                var dbMembersLookup = await this.dataProvider.GetAllUsersOptInStatusAsync();
                dbMembersCount = dbMembersLookup.Count;

                foreach (var team in teams)
                {
                    this.telemetryClient.TrackTrace($"Pairing members of team {team.Id}");

                    try
                    {
                        var teamName = await this.conversationHelper.GetTeamNameByIdAsync(this.botAdapter, team);
                        var optedInUsers = await this.GetOptedInUsersAsync(dbMembersLookup, team);

                        foreach (var group in this.MakeGroups(optedInUsers).Take(this.maxPairUpsPerTeam))
                        {
                            usersNotifiedCount += await this.NotifyGroupAsync(team, teamName, group, default(CancellationToken));
                            groupsNotifiedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.telemetryClient.TrackTrace($"Error pairing up team members: {ex.Message}", SeverityLevel.Warning);
                        this.telemetryClient.TrackException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error making pairups: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            // Log telemetry about the pairups
            var properties = new Dictionary<string, string>
            {
                { "InstalledTeamsCount", installedTeamsCount.ToString() },
                { "groupsNotifiedCount", groupsNotifiedCount.ToString() },
                { "UsersNotifiedCount", usersNotifiedCount.ToString() },
                { "DBMembersCount", dbMembersCount.ToString() },
            };
            this.telemetryClient.TrackEvent("ProcessedPairups", properties);

            this.telemetryClient.TrackTrace($"Made {groupsNotifiedCount} pairups, {usersNotifiedCount} notifications sent");
            return groupsNotifiedCount;
        }

        /// <summary>
        /// Notify a pairup.
        /// </summary>
        /// <param name="teamModel">DB team model info.</param>
        /// <param name="teamName">MS-Teams team name</param>
        /// <param name="group">The group to notify</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>Number of users notified successfully</returns>
        private async Task<int> NotifyGroupAsync(TeamInstallInfo teamModel, string teamName, List<ChannelAccount> group, CancellationToken cancellationToken)
        {
            // Get the default culture info to use in resource files.
            var cultureName = CloudConfigurationManager.GetSetting("DefaultCulture");
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

            var tasks = new List<Task<bool>>();
            var teamsGroup = group.Select(x => JObject.FromObject(x).ToObject<TeamsChannelAccount>());
            foreach (TeamsChannelAccount teamsPerson in group)
            {
                this.telemetryClient.TrackTrace($"Sending grouping notification to {teamsPerson.Id}");

                var restOfGroup = new List<TeamsChannelAccount>(teamsGroup);
                restOfGroup.Remove(teamsPerson);
                var card = PairUpNotificationAdaptiveCard.GetCard(teamName, teamsPerson, restOfGroup, this.botDisplayName);
                tasks.Add(
                    this.conversationHelper.NotifyUserAsync(this.botAdapter, teamModel.ServiceUrl, teamModel.TeamId, MessageFactory.Attachment(card), teamsPerson, teamModel.TenantId, cancellationToken));
            }

            // Send notifications and return the number that was successful
            var notifyResults = await Task.WhenAll(tasks);
            return notifyResults.Count(wasNotified => wasNotified);
        }

        /// <summary>
        /// Get list of opted in users to start matching process
        /// </summary>
        /// <param name="dbMembersLookup">Lookup of DB users opt-in status</param>
        /// <param name="teamInfo">The team that the bot has been installed to</param>
        /// <returns>Opted in users' channels</returns>
        private async Task<List<ChannelAccount>> GetOptedInUsersAsync(Dictionary<string, bool> dbMembersLookup, TeamInstallInfo teamInfo)
        {
            // Pull the roster of specified team and then remove everyone who has opted out explicitly
            var members = await this.conversationHelper.GetTeamMembers(this.botAdapter, teamInfo);

            this.telemetryClient.TrackTrace($"Found {members.Count} in team {teamInfo.TeamId}");

            return members
                .Where(member => member != null)
                .Where(member =>
                {
                    var memberObjectId = this.GetChannelUserObjectId(member);
                    return !dbMembersLookup.ContainsKey(memberObjectId) || dbMembersLookup[memberObjectId];
                })
                .ToList();
        }

        /// <summary>
        /// Extract user Aad object id from channel account
        /// </summary>
        /// <param name="account">User channel account</param>
        /// <returns>Aad object id Guid value</returns>
        private string GetChannelUserObjectId(ChannelAccount account)
        {
            return JObject.FromObject(account).ToObject<TeamsChannelAccount>()?.AadObjectId;
        }

        /// <summary>
        /// Pair list of users into groups of 2 users per group
        /// </summary>
        /// <param name="users">Users accounts</param>
        /// <returns>List of pairs</returns>
        private List<List<ChannelAccount>> MakeGroups(List<ChannelAccount> users)
        {
            this.Randomize(users);

            var groups = new List<List<ChannelAccount>>();
            int i = 0;
            while (i <= users.Count - this.groupSize)
            {
                groups.Add(users.GetRange(i, this.groupSize));
                i += this.groupSize;
            }

            if (i <= users.Count - 2)
            {
                groups.Add(users.GetRange(i, users.Count - i));
                i = users.Count;
            }

            if (groups.Count > 0)
            {
                this.telemetryClient.TrackTrace($"Made {groups.Count} groups among {users.Count} users");
            }
            else
            {
                this.telemetryClient.TrackTrace($"Groups could not be made because there is only 1 user in the team");
            }

            return groups;
        }

        /// <summary>
        /// Randomize list of users
        /// </summary>
        /// <typeparam name="T">Generic item type</typeparam>
        /// <param name="items">List of users to randomize</param>
        private void Randomize<T>(IList<T> items)
        {
            Random rand = new Random(Guid.NewGuid().GetHashCode());

            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Count - 1; i++)
            {
                int j = rand.Next(i, items.Count);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }
}