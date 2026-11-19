using System;
using System.Linq;
using System.Net;
using System.IO;
using System.Collections.Generic;
using Fallout.Common;
using Fallout.Common.CI;
using Fallout.Common.Execution;
using Fallout.Common.IO;
using static Fallout.Common.Tools.DotNet.DotNetTasks;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.NuGet;
using Fallout.Common.Utilities.Collections;
using Serilog;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.IO.PathConstruction;

class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath VersionFile => RootDirectory / "version.txt";
    string Version => File.ReadAllText(VersionFile).Trim();

    // Package names
    private IReadOnlyList<string> Packages => new[]
    {
        "VL.AlchemX",
        "VL.Kairos",
        "VL.Kairos.Fuse",
        "VL.Kairos.Runtime",
        "VL.Kairos.Stride",
        "VL.LayerX",
        "VL.Touchy"
    };

    // AlchemX
    AbsolutePath AlchemXProject => RootDirectory / "VL.AlchemX" / "src" / "AlchemX" / "AlchemX.csproj";

    // Kairos.Runtime
    AbsolutePath KairosRuntimeProject => RootDirectory / "VL.AlchemX" / "src" / "VL.Kairos.Runtime.CSharp" /
                                         "VL.Kairos.Runtime.CSharp.csproj";

    // Touchy
    private AbsolutePath TouchyProject => RootDirectory / "VL.Touchy" / "src" / "VL.Touchy.csproj";

    private Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            // Start with a fresh artifacts directory
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            // Nope
        });

    // Build C# solutions
    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            // Build AlchemX
            DotNetBuild(s => s
                .SetProjectFile(AlchemXProject)
                .SetConfiguration("Release"));

            // Build Kairos.Runtime
            DotNetBuild(s => s
                .SetProjectFile(KairosRuntimeProject)
                .SetConfiguration("Release"));

            // Build Touchy
            DotNetBuild(s => s
                .SetProjectFile(TouchyProject)
                .SetConfiguration("Release"));
        });

    // Create all packages with the same version
    private Target Pack => _ => _
        .DependsOn(Compile)
        .Requires(() => File.Exists(VersionFile))
        .Produces(ArtifactsDirectory / "*.nupkg")
        .Executes(() =>
        {
            Version.NotNullOrEmpty($"{VersionFile} empty or not found");

            foreach (var package in Packages)
            {
                Log.Information($"Attempting to pack {package}");
                try
                {
                    NuGetTasks.NuGetPack(p => p
                        .SetTargetPath(RootDirectory / package / $"deployment/{package}.nuspec")
                        .SetOutputDirectory(ArtifactsDirectory)
                        .SetVersion(Version));
                }
                catch (Exception e)
                {
                    Log.Error(e, $"Failed to pack {package}");
                    throw;
                }
            }
        });
}