﻿using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToVerifyNuGetReferenceCompat : SdkTest, IClassFixture<DeleteNuGetArtifactsFixture>
    {
        private TestPackageReference _net461PackageReference;

        [Theory]
        [InlineData("net45", "Full", "netstandard1.0 netstandard1.1 net45", true, true)]
        [InlineData("net451", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 net45 net451", true, true)]
        [InlineData("net46", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 net45 net451 net46", true, true)]
        [InlineData("net461", "PartM3", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 net45 net451 net46 net461", true, true)]
        [InlineData("net462", "PartM2", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 net45 net451 net46 net461", true, true)]
        //  Fullframework NuGet versioning on Jenkins infrastructure issue
        //        https://github.com/dotnet/sdk/issues/1041
        //[InlineData("net461", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]
        //[InlineData("net462", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]

        [InlineData("netstandard1.0", "Full", "netstandard1.0", true, true)]
        [InlineData("netstandard1.1", "Full", "netstandard1.0 netstandard1.1", true, true)]
        [InlineData("netstandard1.2", "Full", "netstandard1.0 netstandard1.1 netstandard1.2", true, true)]
        [InlineData("netstandard1.3", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3", true, true)]
        [InlineData("netstandard1.4", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4", true, true)]
        [InlineData("netstandard1.5", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5", true, true)]
        [InlineData("netstandard1.6", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6", true, true)]
        [InlineData("netstandard2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0", true, true)]
        [InlineData("netcoreapp1.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0", true, true)]
        [InlineData("netcoreapp1.1", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1", true, true)]
        [InlineData("netcoreapp2.0", "PartM1", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]
        [InlineData("netcoreapp2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]

        //  OptIn matrix throws an exception for each permutation
        //        https://github.com/dotnet/sdk/issues/1025
        //[InlineData("netstandard2.0", "OptIn", "net45 net451 net46 net461", true, true)]
        //[InlineData("netcoreapp2.0", "OptIn", "net45 net451 net46 net461", true, true)]

        public void Nuget_reference_compat(string referencerTarget, string testDescription, string rawDependencyTargets,
                bool restoreSucceeds, bool buildSucceeds)
        {
            if (UsingFullFrameworkMSBuild &&
                (referencerTarget == "netcoreapp2.0" || referencerTarget == "netstandard2.0"))
            {
                //  Fullframework NuGet versioning on Jenkins infrastructure issue
                //        https://github.com/dotnet/sdk/issues/1041

                //  Disabled on full framework MSBuild until CI machines have VS with bundled .NET Core / .NET Standard versions
                //  See https://github.com/dotnet/sdk/issues/1077
                return;
            }

            string referencerDirectoryNamePostfix = "_" + referencerTarget + "_" + testDescription;

            TestProject referencerProject = GetTestProject(ConstantStringValues.ReferencerDirectoryName, referencerTarget, true);

            //  Skip running test if not running on Windows
            //        https://github.com/dotnet/sdk/issues/335
            if (!(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || referencerProject.BuildsOnNonWindows))
            {
                return;
            }

            foreach (string dependencyTarget in rawDependencyTargets.Split(',', ';', ' ').ToList())
            {
                TestProject dependencyProject = GetTestProject(ConstantStringValues.DependencyDirectoryNamePrefix + dependencyTarget.Replace('.', '_'), dependencyTarget, true);
                TestPackageReference dependencyPackageReference = new TestPackageReference(
                    dependencyProject.Name,
                    "1.0.0",
                    ConstantStringValues.ConstructNuGetPackageReferencePath(dependencyProject));

                //  Skip creating the NuGet package if not running on Windows; or if the NuGet package already exists
                //        https://github.com/dotnet/sdk/issues/335
                if ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || dependencyProject.BuildsOnNonWindows) && !dependencyPackageReference.NuGetPackageExists())
                {
                    referencerProject.PackageReferences.Add(dependencyPackageReference);

                    //  Create the NuGet packages
                    var dependencyTestAsset = _testAssetsManager.CreateTestProject(dependencyProject, ConstantStringValues.TestDirectoriesNamePrefix, ConstantStringValues.NuGetSharedDirectoryNamePostfix);
                    var dependencyRestoreCommand = dependencyTestAsset.GetRestoreCommand(relativePath: dependencyProject.Name).Execute().Should().Pass();
                    var dependencyProjectDirectory = Path.Combine(dependencyTestAsset.TestRoot, dependencyProject.Name);

                    var dependencyPackCommand = new PackCommand(Stage0MSBuild, dependencyProjectDirectory);
                    var dependencyPackResult = dependencyPackCommand.Execute().Should().Pass();
                }
            }

            //  Skip running tests if no NuGet packages are referenced
            //        https://github.com/dotnet/sdk/issues/335
            if (referencerProject.PackageReferences == null)
            {
                return;
            }

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.ShortTargetFrameworkIdentifiers.Contains(ConstantStringValues.NetstandardToken))
            {
                referencerProject.IsExe = true;
            }

            //  Create the referencing app and run the compat test
            var referencerTestAsset = _testAssetsManager.CreateTestProject(referencerProject, ConstantStringValues.TestDirectoriesNamePrefix, referencerDirectoryNamePostfix);
            var referencerRestoreCommand = referencerTestAsset.GetRestoreCommand(relativePath: referencerProject.Name);

            //  Modify the restore command to refer to the created NuGet packages
            foreach (TestPackageReference packageReference in referencerProject.PackageReferences)
            {
                referencerRestoreCommand.AddSource(Path.GetDirectoryName(packageReference.NupkgPath));
            }

            if (restoreSucceeds)
            {
                referencerRestoreCommand.Execute().Should().Pass();
            }
            else
            {
                referencerRestoreCommand.CaptureStdOut().Execute().Should().Fail();
            }

            var referencerBuildCommand = new BuildCommand(Stage0MSBuild, Path.Combine(referencerTestAsset.TestRoot, referencerProject.Name));
            var referencerBuildResult = referencerBuildCommand.Execute();

            if (buildSucceeds)
            {
                referencerBuildResult.Should().Pass();
            }
            else
            {
                referencerBuildResult.Should().Fail().And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }
        }

        [CoreMSBuildAndWindowsOnlyTheory]
        [InlineData("netstandard2.0")]
        [InlineData("netcoreapp2.0")]
        public void Net461_is_implicit_for_Netstandard_and_Netcore_20(string targetFramework)
        {
            var testProjectName = targetFramework.Replace(".", "_") + "implicit_ptf";

            var testProjectTestAsset = CreateTestAsset(testProjectName, targetFramework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(relativePath: testProjectName);
            restoreCommand.AddSource(Path.GetDirectoryName(_net461PackageReference.NupkgPath));
            restoreCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(
                Stage0MSBuild,
                Path.Combine(testProjectTestAsset.TestRoot, testProjectName));
            buildCommand.Execute().Should().Pass();
        }

        [WindowsOnlyTheory]
        [InlineData("netstandard1.6")]
        [InlineData("netcoreapp1.1")]
        public void Net461_is_not_implicit_for_Netstandard_and_Netcore_less_than_20(string targetFramework)
        {
            var testProjectName = targetFramework.Replace(".", "_") + "non_implicit_ptf";

            var testProjectTestAsset = CreateTestAsset(testProjectName, targetFramework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(relativePath: testProjectName);
            restoreCommand.AddSource(Path.GetDirectoryName(_net461PackageReference.NupkgPath));
            restoreCommand.Execute().Should().Fail();
        }

        [CoreMSBuildAndWindowsOnlyTheory]
        public void It_is_possible_to_disabled_net461_implicit_package_target_fallback()
        {
            const string testProjectName = "netstandard20_disabled_ptf";

            var testProjectTestAsset = CreateTestAsset(
                testProjectName,
                "netstandard2.0",
                new Dictionary<string, string> { {"DisableImplicitPackageTargetFallback", "true" } });

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(relativePath: testProjectName);
            restoreCommand.AddSource(Path.GetDirectoryName(_net461PackageReference.NupkgPath));
            restoreCommand.Execute().Should().Fail();
        }

        private TestAsset CreateTestAsset(
            string testProjectName,
            string targetFramework,
            Dictionary<string, string> additionalProperties = null)
        {
            _net461PackageReference = CreateNet461Package();

            var testProject =
                new TestProject
                {
                    Name = testProjectName,
                    TargetFrameworks = targetFramework,
                    IsSdkProject = true
                };

            if (additionalProperties != null)
            {
                foreach (var additionalProperty in additionalProperties)
                {
                    testProject.AdditionalProperties.Add(additionalProperty.Key, additionalProperty.Value);    
                }
            }
            
            testProject.PackageReferences.Add(_net461PackageReference);

            var testProjectTestAsset = _testAssetsManager.CreateTestProject(
                testProject,
                string.Empty,
                $"{testProjectName}_net461");

            return testProjectTestAsset;
        }

        private TestPackageReference CreateNet461Package()
        {
            var net461Project = 
                new TestProject
                {
                    Name = $"net461_pkg",
                    TargetFrameworks = "net461",
                    IsSdkProject = true
                };

            var net461PackageReference =
                new TestPackageReference(
                    net461Project.Name,
                    "1.0.0",
                    ConstantStringValues.ConstructNuGetPackageReferencePath(net461Project));

            if (!net461PackageReference.NuGetPackageExists())
            {
                var net461PackageTestAsset = 
                    _testAssetsManager.CreateTestProject(
                        net461Project,
                        ConstantStringValues.TestDirectoriesNamePrefix,
                        ConstantStringValues.NuGetSharedDirectoryNamePostfix);
                var packageRestoreCommand =
                    net461PackageTestAsset.GetRestoreCommand(relativePath: net461Project.Name).Execute().Should().Pass();
                var dependencyProjectDirectory = Path.Combine(net461PackageTestAsset.TestRoot, net461Project.Name);
                var packagePackCommand =
                    new PackCommand(Stage0MSBuild, dependencyProjectDirectory).Execute().Should().Pass();
            }

            return net461PackageReference;
        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new TestProject()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }
    }
}
