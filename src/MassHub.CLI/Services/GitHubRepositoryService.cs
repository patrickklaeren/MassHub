using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Serilog;
using static MassHub.CLI.ResponseHelper;

namespace MassHub.CLI.Services
{
    internal class GitHubRepositoryService : IGitHubService
    {
        private readonly GitHubContext _gitHubContext;

        internal GitHubRepositoryService(GitHubContext context)
        {
            _gitHubContext = context;
        }

        public async Task Run()
        {
            Console.WriteLine("Getting repositories for organisation");
            
            var repositories = await _gitHubContext.Client.Repository.GetAllForOrg(_gitHubContext.OrganisationName);
            
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
                
                await _gitHubContext.Client.Repository.Edit(repository.Id, new RepositoryUpdate(repository.Name)
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
    }
}