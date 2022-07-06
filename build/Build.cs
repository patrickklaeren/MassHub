using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions("Release",
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.MacOsLatest,
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPushBranches = new[] { "master" },
    InvokedTargets =
        new[]
        {
            nameof(Clean),
            nameof(Restore),
            nameof(Compile),
            nameof(Publish),
        })]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);
    
    [Solution] 
    readonly Solution Solution;
    
    [GitVersion] 
    readonly GitVersion GitVersion;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("RID for publishing")] 
    readonly string Runtime = "win-x64";

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .SetAuthors("Patrick Klaeren")
                .SetCopyright("Copyright © 2022 Patrick Klaeren. All rights reserved."));
        });

    Target Publish => _ => _
        .DependsOn(Compile)
        .Produces(ArtifactsDirectory / $"self-contained-single-{Runtime}")
        .Produces(ArtifactsDirectory / $"self-contained-{Runtime}")
        .Executes(() =>
        {
            var selfContainedSingleFile = GetBaseSettings()
                .SetOutput(ArtifactsDirectory / $"self-contained-single-{Runtime}")
                .EnableSelfContained()
                .SetProcessArgumentConfigurator(x =>
                    x.Add(
                        "-p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=None"));

            var selfContained = GetBaseSettings()
                .SetOutput(ArtifactsDirectory / $"self-contained-{Runtime}")
                .EnableSelfContained()
                .SetProcessArgumentConfigurator(x => x.Add("-p:PublishTrimmed=true -p:DebugType=None"));

            DotNetPublish(s => selfContainedSingleFile);
            DotNetPublish(s => selfContained);

            DotNetPublishSettings GetBaseSettings()
            {
                return new DotNetPublishSettings()
                    .SetProject(SourceDirectory / "MassHub.CLI" / "MassHub.CLI.csproj")
                    .SetRuntime(Runtime)
                    .SetConfiguration(Configuration);
            }
        });
}
