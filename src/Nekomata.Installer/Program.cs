using System;
using System.IO;
using WixSharp;

namespace Nekomata.Installer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var projectDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\"));
            var publishDir = Path.Combine(projectDir, @"dist\Nekomata-win-x64-unpacked");

            if (!Directory.Exists(publishDir))
            {
                Console.WriteLine($"Publish directory not found: {publishDir}");
                Console.WriteLine("Please run 'dotnet publish' first.");
                return;
            }

            var project = new Project("Nekomata",
                new Dir(@"%ProgramFiles%\SeaMoon Craft\Nekomata",
                    new DirFiles(Path.Combine(publishDir, "*.*")),
                    new ExeFileShortcut("Nekomata", "[INSTALLDIR]Nekomata.UI.exe", "")
                    {
                        WorkingDirectory = "[INSTALLDIR]"
                    },
                    new ExeFileShortcut("Uninstall Nekomata", "[System64Folder]msiexec.exe", "/x [ProductCode]")
                ),
                new Dir(@"%ProgramMenu%\SeaMoon Craft\Nekomata",
                    new ExeFileShortcut("Nekomata", "[INSTALLDIR]Nekomata.UI.exe", "")
                    {
                        WorkingDirectory = "[INSTALLDIR]"
                    },
                    new ExeFileShortcut("Uninstall Nekomata", "[System64Folder]msiexec.exe", "/x [ProductCode]")
                )
            );

            project.GUID = new Guid("6f330b47-2577-43ad-9095-1861ba25889b");
            project.Version = new Version("0.1.0");
            project.ControlPanelInfo.Manufacturer = "SeaMoon Craft";
            project.OutDir = Path.Combine(projectDir, "dist");
            project.OutFileName = "Nekomata-Setup";
            project.LicenceFile = Path.Combine(projectDir, @"src\Nekomata.Installer\Assets\License.rtf");
            project.UI = WUI.WixUI_InstallDir;

            // Ensure the dist folder exists for the MSI output
            Directory.CreateDirectory(project.OutDir);

            Console.WriteLine("Building MSI...");
            Compiler.BuildMsi(project);
            Console.WriteLine($"MSI created at: {Path.Combine(project.OutDir, project.OutFileName + ".msi")}");
        }
    }
}
