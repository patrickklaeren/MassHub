using System;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Serilog;

namespace MassHub.CLI.Services
{
    internal class GitHubTeamService : IGitHubService
    {
        private readonly GitHubContext _gitHubContext;

        public GitHubTeamService(GitHubContext gitHubContext)
        {
            _gitHubContext = gitHubContext;
        }

        public async Task Run()
        {
            var teamIdResponse = ResponseHelper.AskAndWaitForIntResponse("Enter a team ID to update");
            
            Console.WriteLine("Enter a repository name to update a single repository or * for all repositories");
            var repositoryResponse = Console.ReadLine();

            var acceptedTerms = new[] {"READ", "WRITE", "ADMIN"};
            Permission teamPermission;

            while (true)
            {
                Console.WriteLine("Enter permission for team on given repositories one of READ/WRITE/ADMIN");
                var permissionResponse = Console.ReadLine();

                if (!acceptedTerms.Any(x => x.Equals(permissionResponse, StringComparison.CurrentCultureIgnoreCase))) 
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
            
            Log.Debug("Getting repositories for organisation {Organisation}", _gitHubContext.OrganisationName);

            if (repositoryResponse != "*")
            {
                Log.Debug("Updating single repository {RepositoryName}", repositoryResponse);
                await AddRepositoryToTeam(repositoryResponse);
            }
            else
            {
                Log.Debug("Updating all repositories, fetching repositories");
                var repositories = await _gitHubContext.Client.Repository.GetAllForOrg(_gitHubContext.OrganisationName);

                foreach (var repository in repositories)
                {
                    await AddRepositoryToTeam(repository.Name);
                }
            }
            
            Console.WriteLine("Finished processing team!");

            async Task AddRepositoryToTeam(string repositoryName)
            {
                await _gitHubContext.Client.Organization.Team.AddRepository(teamIdResponse, _gitHubContext.OrganisationName, repositoryName, new RepositoryPermissionRequest(teamPermission));
            }
        }
    }
}