# MassHub - GitHub Org Editing en masse

![Release](https://github.com/Inzanit/MassHub/workflows/Release/badge.svg)

## Motivation

GitHub doesn't seem to provide a native solution to modifying many repositories in a single organisation easily. Branch protection, team repository ownership and status checks all need to be done on a per-repository basis. MassHub aims at trying to introduce at least *some* mass editing capabilities while GitHub are hopefully working on introducing this.

This was born out of the need to modify repositories on a work organisation - this may or may not match what you require. But can be easily extended.

## What can it do

This is a CLI application that is purely interactive and cannot be easily automated. User interaction is a must.

Current featureset includes:
- Updating repository options such as:
  - Making repos private/public
  - Toggling issues
  - Toggling wiki
  - Toggling downloads
  - Toggling merge commits
  - Toggling rebase merges
  - Toggling squash merges

- Updating branches in all repositories for:
  - Toggling strict reviews
  - Toggling stale reviews
  - Toggling code owner's review
  - Toggling enforcement on admins
  - Setting the number of reviews required for pull requests
  - Adding and removing teams

- Updating Team repository ownership
- Updating labels in all repositories of an organisation

MassHub attempts to support as many configuration/user driven scenarios as possible, i.e. ignoring certain branches or repositories, but as with anything, there may be an edge case not supported. If you do end up using this tool and your edge case is not supported, feel free to [submit an issue](https://github.com/Inzanit/MassHub/issues/new).

Releases are provided for Windows x64, Linux x64, Linux ARM and MacOS x64. This has only been tested on Windows.

Treat all operations as destructive. There is no rollback functionality.

[Find the latest release here](https://github.com/Inzanit/MassHub/releases).

## Simple use

Get a GitHub access token from [here](https://github.com/settings/tokens)

Although the application is mostly interactive, several startup arguments can be provided, but are optional:

```--token```

GitHub token that will be used to authenticate with GitHub for all requests.

```--org```

Name of GitHub organisation to target for all requests.

```--product-header```

Allows setting of the product header used when contacting GitHub API, by default this will be `mass-hub`.

Example: `--product-header "hello-world"`

```--verbose```

Will trigger all debug logs to be output

Example: `--verbose true`

```--log-file```

Path to the file where you want to have logs output to.

Example: `--log-file C:\MyLog\log.txt`

Most basic use:

```
MassHub.CLI.exe --token 123456789 --org Foobar
```

If you do not provide a token or organisation as an argument, you will be asked to enter this interactively.

## Stack

All code is situated in `MassHub.CLI`, where `Program` is a typical Console structure and `GitHubService` provides the necessary logic to perform updates.

This is a console application targeting .NET Core 3.1. It supports running on Windows x64, Linux x64/ARM and MacOS x64. It makes use of the following dependencies:

- [Nuke](https://nuke.build/) for build pipeline
- [Serilog](https://github.com/serilog/serilog) for logging
- [Octokit.NET](https://github.com/octokit/octokit.net) for GitHub API interaction

## Do with it what you want

MassHub is licensed under MIT, you're free to do with it what you want. Contribute, download it, fork it, steal it, sell it, repurpose it.

## Disclaimer

Due to the nature of what MassHub does, I/MassHub do not take responsibility for any damage or loss of data due to the use of this tool. While MassHub has been tested in a production environment, bugs or other scenarios could lead to irreversible damage which is your own responsbility.

MassHub does **not** delete any repositories, teams or branches.

MassHub does **not** store or upload your GitHub token anywhere, except for in memory while the application is running.