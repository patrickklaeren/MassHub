using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Serilog;

namespace MassHub.CLI.Services
{
    internal class GitHubBranchService : IGitHubService
    {
        private readonly GitHubContext _gitHubContext;

        public GitHubBranchService(GitHubContext gitHubContext)
        {
            _gitHubContext = gitHubContext;
        }

        public async Task Run()
        {
            Console.WriteLine("Enter a repository name to update a single repository or * for all repositories");
            var response = Console.ReadLine();
            
            Log.Debug("Getting repositories for organisation {Organisation}", _gitHubContext.OrganisationName);

            var branches = new List<(long repositoryId, string repositoryName, string branchName)>();

            Console.WriteLine("Getting branches...");

            if (response != "*")
            {
                Console.WriteLine("Continuing with single repository");
                Log.Debug("Updating single repository {RepositoryName}", response);
                var repo = await _gitHubContext.Client.Repository.Get(_gitHubContext.OrganisationName, response);
                await FetchBranchesInRepository(repo.Id, repo.Name);
            }
            else
            {
                Console.WriteLine("Continuing with all repositories");
                Log.Debug("Updating all repositories, fetching repositories");
                var repositories = await _gitHubContext.Client.Repository.GetAllForOrg(_gitHubContext.OrganisationName);

                foreach (var repository in repositories)
                {
                    await FetchBranchesInRepository(repository.Id, repository.Name);
                }
            }
            
            Console.WriteLine("Enter a comma separated list of branches to update or * for all branches in a given repository");
            var branchUpdateResponse = Console.ReadLine();

            var branchesToUpdate = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(response))
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

            var isStrict = ResponseHelper.AskYesNoOrDefaultResponse("Require strict PR reviews (branch must be up to date with merge target)");
            var enableStaleReviews = ResponseHelper.AskYesNoOrDefaultResponse("Enable reset approval on stale branch (reset approvals on PRs on every new change)");
            var enableCodeOwners = ResponseHelper.AskYesNoOrDefaultResponse("Enable require code owner review (code owners must review PRs)");
            var enforceAdmins = ResponseHelper.AskYesNoOrDefaultResponse("Enable admin enforcement (enforce some rules on administrators of the repository)");

            Console.WriteLine("Other settings, these DO NOT require Y (yes) or N (no)");
            
            var numberOfReviewsRequired = ResponseHelper.AskIntResponse("Specify number of reviews required for a PR before it can be merged");

            const string DELETE_ALL_TEAMS_COMMAND = "DELETE_ALL_TEAMS";
            var teamsToAdd = ResponseHelper.AskListResponse($"Enter a comma separated list of teams to set to the protection levels, enter {DELETE_ALL_TEAMS_COMMAND} to remove all current teams, press enter to leave empty and existing teams");

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
                    currentProtection = await _gitHubContext.Client.Repository.Branch.GetBranchProtection(branchRepositoryId, branchName);
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
                
                BranchProtectionPushRestrictionsUpdate pushRestrictions = null;

                if (teamsToSetAsBranchProtectors.Any())
                {
                    pushRestrictions= new BranchProtectionPushRestrictionsUpdate(new BranchProtectionTeamCollection(teamsToSetAsBranchProtectors));   
                }
                
                var enforceBranchProtectionOnAdmins = enforceAdmins ?? currentProtection.EnforceAdmins.Enabled;
                
                await _gitHubContext.Client.Repository.Branch.UpdateBranchProtection(branchRepositoryId, 
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
                var branchesInRepository = await _gitHubContext.Client.Repository.Branch.GetAll(repositoryId);
                branches.AddRange(branchesInRepository.Select(x => (repositoryId, repositoryName, x.Name)));
            }
        }
    }
}