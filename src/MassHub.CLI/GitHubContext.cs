using System;
using Octokit;

namespace MassHub.CLI
{
    internal class GitHubContext
    {
        public readonly GitHubClient Client;
        public readonly string OrganisationName;
        
        public GitHubContext(string gitHubToken, string productHeader, string organisationName)
        {
            
            if(string.IsNullOrWhiteSpace(gitHubToken))
                throw new ArgumentNullException(nameof(gitHubToken));
            
            if(string.IsNullOrWhiteSpace(productHeader))
                throw new ArgumentNullException(nameof(productHeader));
            
            var client = new GitHubClient(new ProductHeaderValue(productHeader));
            var tokenAuth = new Credentials(gitHubToken);
            client.Credentials = tokenAuth;

            Client = client;
            OrganisationName = organisationName;
        }
    }
}