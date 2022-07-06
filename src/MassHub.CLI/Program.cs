using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MassHub.CLI
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            Console.WriteLine(@"
                                   __  __           __        
 /'\_/`\                          /\ \/\ \         /\ \       
/\      \     __      ____    ____\ \ \_\ \  __  __\ \ \____  
\ \ \__\ \  /'__`\   /',__\  /',__\\ \  _  \/\ \/\ \\ \ '__`\ 
 \ \ \_/\ \/\ \L\.\_/\__, `\/\__, `\\ \ \ \ \ \ \_\ \\ \ \L\ \
  \ \_\\ \_\ \__/.\_\/\____/\/\____/ \ \_\ \_\ \____/ \ \_,__/
   \/_/ \/_/\/__/\/_/\/___/  \/___/   \/_/\/_/\/___/   \/___/ 
                                                              
                                                              
");

            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            Console.WriteLine($"GitHub modification en masse - {version ?? "1.0"}");
            
            var levelSwitch = new LoggingLevelSwitch();

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console();

            var tokenOption = new Option<string?>("--token", () => null, "Set output to be verbose");
            var orgOption = new Option<string?>(new[] { "--org", "-o" }, () => null,
                "Set organisation to use for all requests");
            var productHeaderOption = new Option<string>(new[] { "--product-header" }, () => "mass-hub",
                "Optionally set a custom product header used when interacting with GitHub API");
            var verboseOption = new Option<bool>("--verbose", () => false, "Set output to be verbose");
            var logFileOption = new Option<string?>("--log-file", () => null, "Path to log file");

            var rootCommand = new RootCommand
            {
                tokenOption,
                orgOption,
                productHeaderOption,
                verboseOption,
                logFileOption
            };

            rootCommand.Description = "MassHub - GitHub Management en masse";
            
            rootCommand.SetHandler(async (token, org, productHeader, verbose, logFile) =>
            {
                if (verbose)
                {
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                }

                if (!string.IsNullOrWhiteSpace(logFile))
                {
                    loggerConfiguration
                        .WriteTo.File(logFile!);
                }
                
                Log.Logger = loggerConfiguration.CreateLogger();

                while (string.IsNullOrWhiteSpace(token))
                {
                    Log.Debug("GitHub token not provided during argument compile");
                    
                    Console.WriteLine("GitHub token not provided, please provide a token now to use for authentication, find a token at: https://github.com/settings/tokens");

                    token = Console.ReadLine();
                }

                while (string.IsNullOrWhiteSpace(org))
                {
                    Log.Debug("GitHub organisation not provided during argument compile");
                    
                    Console.WriteLine("GitHub organisation not provided, please provide the name of the organisation to use for all requests");

                    org = Console.ReadLine();
                }

                Log.Debug("Starting GitHub client with provided options");
                
                Log.Information("Contacting GitHub with provided token under product header {Header}", productHeader);

                await RunGitHubService(token!, productHeader, org!);
            }, tokenOption, orgOption, productHeaderOption, verboseOption, logFileOption);
            
            await rootCommand.InvokeAsync(args);
        }

        private static async Task RunGitHubService(string gitHubToken, string productHeader, string organisation)
        {
            var service = new GitHubService(gitHubToken, productHeader, organisation);

            var servicesLookup = new Dictionary<(int index, string operation), Func<Task>>
            {
                [(1, "repos")] = service.UpdateRepositories,
                [(2, "branches")] = service.UpdateBranches,
                [(3, "teamrepos")] = service.UpdateTeamRepositories,
            };

            while (true)
            {
                Console.WriteLine("Select from the following operations, by number or name");
                var dumpedOptions = string.Join(Environment.NewLine, servicesLookup.Select(x => $"[{x.Key.index}] {x.Key.operation}"));
                Console.WriteLine(dumpedOptions);
                
                var response = Console.ReadLine();
                
                Func<Task>? operation;

                if (int.TryParse(response, out var parsedIndex))
                {
                    operation = servicesLookup
                        .Where(x => x.Key.index == parsedIndex)
                        .Select(x => x.Value)
                        .SingleOrDefault();
                }
                else
                {
                    operation = servicesLookup
                        .Where(x => x.Key.operation.Equals(response, StringComparison.CurrentCultureIgnoreCase))
                        .Select(x => x.Value)
                        .SingleOrDefault();
                }

                if (operation is not null)
                {
                    await operation();
                }
            }
        }
    }
}