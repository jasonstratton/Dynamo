using System;
using System.IO;
using System.Reflection;
using Dynamo.Utilities;
using NUnit.Framework;

namespace DynamoUtilitiesTests
{
    [NonParallelizable]
    public class AssemblyHelperTests
    {
        private static string GetTestDependenciesPath()
        {
            // Mirror logic from Setup.cs to locate test/test_dependencies
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var moduleRootFolder = new DirectoryInfo(assemblyPath).Parent;
            return Path.Combine(moduleRootFolder.Parent.Parent.Parent.FullName, "test", "test_dependencies");
        }

        private static string CopyDependencyTo(string dependencyFileName, string destinationDirectory, string targetFileName)
        {
            Directory.CreateDirectory(destinationDirectory);
            var sourcePath = Path.Combine(GetTestDependenciesPath(), dependencyFileName);
            var destPath = Path.Combine(destinationDirectory, targetFileName);
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }

        [Test, Category("AssemblyHelper"), Category("UnitTests"), Order(1)]
        public void ResolveAssembly_LoadsFromModuleRoot_WhenFileExists()
        {
            var tempRoot = Directory.CreateTempSubdirectory();
            try
            {
                // Place a valid assembly in the moduleRoot with an arbitrary name
                var targetAssemblyLogicalName = "FooBar";
                var targetFileName = targetAssemblyLogicalName + ".dll";
                var expectedPath = CopyDependencyTo("EmbeddedInterop.dll", tempRoot.FullName, targetFileName);

                var helper = new AssemblyHelper(tempRoot.FullName, Array.Empty<string>(), testMode: true);
                var args = new ResolveEventArgs(targetAssemblyLogicalName);

                var resolved = helper.ResolveAssembly(sender: null, args: args);

                Assert.IsNotNull(resolved);
                Assert.AreEqual(expectedPath, resolved.Location, "Assembly should be loaded from module root path");
            }
            finally
            {
                try
                {
                    tempRoot.Delete(true);
                }
                catch (IOException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
            }
        }

        [Test, Category("AssemblyHelper"), Category("UnitTests"), Order(2)]
        public void ResolveAssembly_LoadsFromAdditionalPaths_WhenNotInModuleRoot()
        {
            var tempRoot = Directory.CreateTempSubdirectory();
            var tempAdditional = Directory.CreateTempSubdirectory();
            try
            {
                var targetAssemblyLogicalName = "BazQux";
                var targetFileName = targetAssemblyLogicalName + ".dll";
                // Use a different assembly identity than the first test to avoid load-context reuse.
                var expectedPath = CopyDependencyTo("Microsoft.CodeAnalysis.CSharp.dll", tempAdditional.FullName, targetFileName);

                var helper = new AssemblyHelper(tempRoot.FullName, new[] { tempAdditional.FullName }, testMode: true);
                var args = new ResolveEventArgs(targetAssemblyLogicalName);

                var resolved = helper.ResolveAssembly(sender: null, args: args);

                Assert.IsNotNull(resolved);
                Assert.AreEqual(expectedPath, resolved.Location, "Assembly should be loaded from additional resolution path");
            }
            finally
            {
                try
                {
                    tempAdditional.Delete(true);
                }
                catch (IOException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }

                try
                {
                    tempRoot.Delete(true);
                }
                catch (IOException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
            }
        }

        [Test, Category("AssemblyHelper"), Category("UnitTests"), Order(3)]
        public void ResolveAssembly_ReturnsNull_WhenFileNotFound()
        {
            var tempRoot = Directory.CreateTempSubdirectory();
            var tempAdditional = Directory.CreateTempSubdirectory();
            try
            {
                var helper = new AssemblyHelper(tempRoot.FullName, new[] { tempAdditional.FullName }, testMode: true);
                var args = new ResolveEventArgs("NonExistentAssemblyName");

                var resolved = helper.ResolveAssembly(sender: null, args: args);

                Assert.IsNull(resolved, "Should return null when assembly file cannot be resolved");
            }
            finally
            {
                try
                {
                    tempAdditional.Delete(true);
                }
                catch (IOException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }

                try
                {
                    tempRoot.Delete(true);
                }
                catch (IOException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup failures due to Windows file locks on loaded assemblies
                }
            }
        }
    }
}


