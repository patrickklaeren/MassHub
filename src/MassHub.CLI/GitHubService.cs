using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Serilog;
using static MassHub.CLI.ResponseHelper;

namespace MassHub.CLI
{
    internal class GitHubService
    {
        private readonly GitHubClient _client;
        private readonly string _organisationName;

        internal GitHubService(string gitHubToken, string productHeader, string organisation)
        {
            if(string.IsNullOrWhiteSpace(gitHubToken))
                throw new ArgumentNullException(nameof(gitHubToken));
            
            if(string.IsNullOrWhiteSpace(productHeader))
                throw new ArgumentNullException(nameof(productHeader));
            
            var client = new GitHubClient(new ProductHeaderValue(productHeader));
            var tokenAuth = new Credentials(gitHubToken);
            client.Credentials = tokenAuth;
            
            _client = client;
            _organisationName = organisation ?? throw new InvalidOperationException(nameof(organisation));
        }

        internal async Task UpdateRepositories()
        {
            Console.WriteLine("Getting repositories for organisation");
            
            var repositories = await _client.Repository.GetAllForOrg(_organisationName);
            
            Log.Debug("Got repositories");
            
            Log.Debug("Found {NumberOfRepositories} repositories", repositories.Count);

            Console.WriteLine("Enter a comma separated list of repositories to ignore during the update, press enter to ignore no repositories");

            var response = Console.ReadLine();
            
            var ignoredRepositories = new List<string>();

            if (!string.IsNullOrWhiteSpace(response))
            {
                ignoredRepositories = response.Split(',').ToList();
                Log.Debug("Ignoring {numberOfRepositories} repositories: {ignoredRepositories}", ignoredRepositories.Count, ignoredRepositories);
            }

            Console.WriteLine("Configure global settings");
            Console.WriteLine("Respond Y or yes to enable, N or no to disable and just enter to leave a setting as is on the target repository");
            Console.WriteLine("Default: leave setting as is");

            var isPrivate = AskYesNoOrDefaultResponse("Is private");
            var enableIssues = AskYesNoOrDefaultResponse("Enable issues");
            var enableWiki = AskYesNoOrDefaultResponse("Enable wiki");
            var enableDownloads = AskYesNoOrDefaultResponse("Enable downloads");
            var allowMergeCommits = AskYesNoOrDefaultResponse("Enable merge commits");
            var allowRebaseMerge = AskYesNoOrDefaultResponse("Enable rebase merges");
            var allowSquashMerge = AskYesNoOrDefaultResponse("Enable squash merges");

            Log.Debug(
                $"Updating repositories with private: {isPrivate}, " +
                $"enable issues: {enableIssues}, " +
                $"enable wiki: {enableWiki}, " +
                $"enable downloads: {enableDownloads}, " +
                $"allow merge commits: {allowMergeCommits}, " +
                $"allow rebase merge: {allowRebaseMerge}, " +
                $"allow squash merge: {allowSquashMerge}");

            Console.WriteLine("Processing...");

            foreach (var repository in repositories)
            {
                if (ignoredRepositories.Any(x => x.Equals(repository.Name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Log.Debug("Ignoring repository {RepositoryName}", repository.Name);
                    continue;
                }
                
                Log.Debug("Processing update for repository {RepositoryName}", repository.Name);
                
                await _client.Repository.Edit(repository.Id, new RepositoryUpdate(repository.Name)
                {
                    Private = isPrivate,
                    HasIssues = enableIssues,
                    HasWiki = enableWiki,
                    HasDownloads = enableDownloads,
                    AllowMergeCommit = allowMergeCommits,
                    AllowRebaseMerge = allowRebaseMerge,
                    AllowSquashMerge = allowSquashMerge,
                });
                
                Log.Debug("Finished processing update for repository {RepositoryName}", repository.Name);

                Console.WriteLine($"Processed {repository.Name}");
            }

            Console.WriteLine("Finished processing repositories!");
        }

        internal async Task UpdateBranches()
        {
            Console.WriteLine("Enter a repository name to update a single repository or * for all repositories");
            var response = Console.ReadLine();
            
            Log.Debug("Getting repositories for organisation {Organisation}", _organisationName);

            var branches = new List<(long repositoryId, string repositoryName, string branchName)>();

            Console.WriteLine("Getting branches...");

            if (response != "*")
            {
                Console.WriteLine("Continuing with single repository");
                Log.Debug("Updating single repository {RepositoryName}", response);
                var repo = await _client.Repository.Get(_organisationName, response);
                await FetchBranchesInRepository(repo.Id, repo.Name);
            }
            else
            {
                Console.WriteLine("Continuing with all repositories");
                Log.Debug("Updating all repositories, fetching repositories");
                var repositories = await _client.Repository.GetAllForOrg(_organisationName);

                foreach (var repository in repositories)
                {
                    await FetchBranchesInRepository(repository.Id, repository.Name);
                }
            }
            
            Console.WriteLine("Enter a comma separated list of branches to update or * for all branches in a given repository");
            var branchUpdateResponse = Console.ReadLine();

            var branchesToUpdate = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(branchUpdateResponse))
            {
                branchesToUpdate = branchUpdateResponse.Split(',').ToList();
                Log.Debug("Updating {NumberOfBranches} branches: {IgnoredRepositories}", branchesToUpdate.Count, branchesToUpdate);
            }
            else
            {
                Log.Debug("Updating all branches in the target repositories");
            }
            
            Console.WriteLine("Configure global settings");
            Console.WriteLine("Respond Y or yes to enable, N or no to disable and just enter to leave a setting as is on the target repository");
            Console.WriteLine("Default: leave setting as is");

            var isStrict = AskYesNoOrDefaultResponse("Require strict PR reviews (branch must be up to date with merge target)");
            var enableStaleReviews = AskYesNoOrDefaultResponse("Enable reset approval on stale branch (reset approvals on PRs on every new change)");
            var enableCodeOwners = AskYesNoOrDefaultResponse("Enable require code owner review (code owners must review PRs)");
            var enforceAdmins = AskYesNoOrDefaultResponse("Enable admin enforcement (enforce some rules on administrators of the repository)");

            Console.WriteLine("Other settings, these DO NOT require Y (yes) or N (no)");
            
            var numberOfReviewsRequired = AskIntResponse("Specify number of reviews required for a PR before it can be merged");

            const string DELETE_ALL_TEAMS_COMMAND = "DELETE_ALL_TEAMS";
            var teamsToAdd = AskListResponse($"Enter a comma separated list of teams to set to the protection levels, enter {DELETE_ALL_TEAMS_COMMAND} to remove all current teams, press enter to leave empty and existing teams");

            if (teamsToAdd.Count > 1 
                && teamsToAdd.Any(x => x.Equals(DELETE_ALL_TEAMS_COMMAND, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new InvalidOperationException("Cannot delete all teams when there is more than one team specified, to override teams provide just the list of teams that are desired");
            }

            var teamsToSetAsBranchProtectors = new List<string>();

            if (teamsToAdd.Any(x => !x.Equals(DELETE_ALL_TEAMS_COMMAND, StringComparison.CurrentCultureIgnoreCase)))
            {
                teamsToSetAsBranchProtectors.AddRange(teamsToAdd);
            }

            Console.WriteLine("Processing updates to branches...");

            Log.Debug(
                $"Updating branches with require strict reviews: {isStrict}, " +
                $"reset approval: {enableStaleReviews}, " +
                $"code owners: {enableCodeOwners}, " +
                $"enforce on admins: {enforceAdmins}, " +
                $"number of reviews: {numberOfReviewsRequired}, " +
                $"teams: {string.Join(", ", teamsToSetAsBranchProtectors)}");

            foreach (var (branchRepositoryId, branchRepositoryName, branchName) in branches)
            {
                if (branchesToUpdate.Any() 
                    && !branchesToUpdate.Any(x => x.Equals(branchName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Log.Debug("Ignoring branch {BranchName} in repository {RepositoryId}", branchName, branchRepositoryId);
                    continue;
                }
                
                Log.Debug("Processing update for branch {BranchName} in repository {RepositoryId}", branchName, branchRepositoryId);
                
                var currentProtection = new BranchProtectionSettings(new BranchProtectionRequiredStatusChecks(false, new List<string>()), 
                    new BranchProtectionPushRestrictions(new List<Team>(), new List<User>()), 
                    new BranchProtectionRequiredReviews(), 
                    new EnforceAdmins(false));

                try
                {
                    currentProtection = await _client.Repository.Branch.GetBranchProtection(branchRepositoryId, branchName);
                }
                catch (Exception)
                {
                    // There may not be protection on the branch
                }

                var statusChecks = new BranchProtectionRequiredStatusChecksUpdate(isStrict ?? currentProtection.RequiredStatusChecks.Strict, currentProtection.RequiredStatusChecks.Contexts);
                
                var pullRequestRequirements = new BranchProtectionRequiredReviewsUpdate(
                    enableStaleReviews ?? currentProtection.RequiredPullRequestReviews.DismissStaleReviews,
                    enableCodeOwners ?? currentProtection.RequiredPullRequestReviews.RequireCodeOwnerReviews,
                    numberOfReviewsRequired ?? currentProtection.RequiredPullRequestReviews.RequiredApprovingReviewCount);
                
                BranchProtectionPushRestrictionsUpdate? pushRestrictions = null;

                if (teamsToSetAsBranchProtectors.Any())
                {
                    pushRestrictions= new BranchProtectionPushRestrictionsUpdate(new BranchProtectionTeamCollection(teamsToSetAsBranchProtectors));   
                }
                
                var enforceBranchProtectionOnAdmins = enforceAdmins ?? currentProtection.EnforceAdmins.Enabled;
                
                await _client.Repository.Branch.UpdateBranchProtection(branchRepositoryId, 
                    branchName, new BranchProtectionSettingsUpdate(statusChecks,
                        pullRequestRequirements, 
                        pushRestrictions, 
                        enforceBranchProtectionOnAdmins));
                
                Log.Debug("Processed update for branch {BranchName} in repository {RepositoryId}", branchName, branchRepositoryId);

                Console.WriteLine($"Processed {branchRepositoryName}/{branchName}");
            }

            Console.WriteLine("Finished processing branches!");

            async Task FetchBranchesInRepository(long repositoryId, string repositoryName)
            {
                var branchesInRepository = await _client.Repository.Branch.GetAll(repositoryId);
                branches.AddRange(branchesInRepository.Select(x => (repositoryId, repositoryName, x.Name)));
            }
        }

        internal async Task UpdateTeamRepositories()
        {
            var teamIdResponse = AskAndWaitForIntResponse("Enter a team ID to update");
            
            Console.WriteLine("Enter a repository name to update a single repository or * for all repositories");
            var repositoryResponse = Console.ReadLine();

            var acceptedTerms = new[] {"READ", "WRITE", "ADMIN"};
            Permission teamPermission;

            while (true)
            {
                Console.WriteLine("Enter permission for team on given repositories one of READ/WRITE/ADMIN");
                var permissionResponse = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(permissionResponse) 
                    || !acceptedTerms.Any(x => x.Equals(permissionResponse, StringComparison.CurrentCultureIgnoreCase))) 
                    continue;
                
                teamPermission = permissionResponse.ToUpper() switch
                {
                    "READ" => Permission.Pull,
                    "WRITE" => Permission.Push,
                    "ADMIN" => Permission.Admin,
                    _ => throw new ArgumentOutOfRangeException(nameof(permissionResponse))
                };
                    
                break;
            }

            Console.WriteLine("Updating repositories for team");
            
            Log.Debug("Getting repositories for organisation {Organisation}", _organisationName);

            if (!string.IsNullOrWhiteSpace(repositoryResponse) && repositoryResponse != "*")
            {
                Log.Debug("Updating single repository {RepositoryName}", repositoryResponse);
                await AddRepositoryToTeam(repositoryResponse);
            }
            else
            {
                Log.Debug("Updating all repositories, fetching repositories");
                var repositories = await _client.Repository.GetAllForOrg(_organisationName);

                foreach (var repository in repositories)
                {
                    await AddRepositoryToTeam(repository.Name);
                }
            }
            
            Console.WriteLine("Finished processing team!");

            async Task AddRepositoryToTeam(string repositoryName)
            {
                await _client.Organization.Team.AddRepository(teamIdResponse, _organisationName, repositoryName, new RepositoryPermissionRequest(teamPermission));
            }
        }
    }
}