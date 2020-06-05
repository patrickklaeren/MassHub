using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Serilog;
using static MassHub.CLI.ResponseHelper;

namespace MassHub.CLI.Services
{
    internal class GitHubLabelService : IGitHubService
    {
        private readonly GitHubContext _gitHubContext;

        internal GitHubLabelService(GitHubContext context)
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
            
            var operationType = AskAndWaitForStringResponse("Type R or rem to remove, type A or add to add");
            

            if (operationType.Equals("R", StringComparison.CurrentCultureIgnoreCase) 
                || operationType.Equals("rem", StringComparison.CurrentCultureIgnoreCase))
            {
                WarnAboutUnicode();
                var labelName = AskAndWaitForStringResponse("Type the name of the label you want to remove:");
                await RemoveLabel(labelName);
            }
            else if (operationType.Equals("A", StringComparison.CurrentCultureIgnoreCase) 
                || operationType.Equals("add", StringComparison.CurrentCultureIgnoreCase))
            {
                WarnAboutUnicode();
                var labelName = AskAndWaitForStringResponse("Type the name of the label you want to add:");
                var labelColour = AskAndWaitForStringResponse("Type the hex colour for the label (without #):");
                await AddLabel(labelName, labelColour);
            }
            else
            {
                Console.WriteLine("No operation matched, cancelling any label updates!");
            }

            async Task RemoveLabel(string labelName)
            {
                Console.WriteLine($"THIS WILL REMOVE {labelName} FROM THE REPOSITORY");
                var confirmation = AskYesNoOrDefaultResponse("ARE YOU SURE YOU WANT TO CONTINUE?");

                if (confirmation != true)
                {
                    Console.WriteLine("Cancelled removing label!");
                    return;
                }

                Console.WriteLine("Processing...");

                foreach (var repository in repositories)
                {
                    if (ignoredRepositories.Any(x => x.Equals(repository.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        Log.Debug("Ignoring repository {RepositoryName}", repository.Name);
                        continue;
                    }
                
                    Log.Debug("Removing label {LabelName} for repository {RepositoryName}", labelName, repository.Name);
                
                    await _gitHubContext.Client.Issue.Labels.Delete(repository.Id, labelName);
                
                    Log.Debug("Removed label {LabelName} for repository {RepositoryName}", labelName, repository.Name);

                    Console.WriteLine($"Processed {repository.Name}");
                }

                Console.WriteLine("Finished processing repositories!");
            }

            async Task AddLabel(string labelName, string labelColour)
            {
                Console.WriteLine($"THIS WILL ADD {labelName} TO ALL REPOSITORIES, EXCEPT THOSE IGNORED");
                var confirmation = AskYesNoOrDefaultResponse("ARE YOU SURE YOU WANT TO CONTINUE?");

                if (confirmation != true)
                {
                    Console.WriteLine("Cancelled adding label!");
                    return;
                }

                Console.WriteLine("Processing...");

                foreach (var repository in repositories)
                {
                    if (ignoredRepositories.Any(x => x.Equals(repository.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        Log.Debug("Ignoring repository {RepositoryName}", repository.Name);
                        continue;
                    }
                
                    Log.Debug("Adding label {LabelName} for repository {RepositoryName}", labelName, repository.Name);
                
                    await _gitHubContext.Client.Issue.Labels.Create(repository.Id, new NewLabel($"{labelName}", labelColour.Replace("#", string.Empty)));
                
                    Log.Debug("Added label {LabelName} for repository {RepositoryName}", labelName, repository.Name);

                    Console.WriteLine($"Processed {repository.Name}");
                }

                Console.WriteLine("Finished processing repositories!");
            }

            static void WarnAboutUnicode()
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("IF THE LABEL CONTAINS AN EMOJI, INSERT THE UNICODE INSTEAD OF THE SYMBOL IN THE FOLLOWING FORMAT: \"\\U23F0\" ");
                Console.ResetColor();
            }
        }
    }
}