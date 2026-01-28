using System;
using System.IO;
using System.Xml.Linq;
using WixSharp;

namespace Nekomata.Installer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var projectDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\"));
            var publishDir = Path.Combine(projectDir, @"dist\Nekomata-win-x64-unpacked");
            var iconPath = Path.Combine(projectDir, @"src\Nekomata.Installer\Assets\nkproj.ico");

            if (!Directory.Exists(publishDir))
            {
                Console.WriteLine($"Publish directory not found: {publishDir}");
                Console.WriteLine("Please run 'dotnet publish' first.");
                return;
            }

            var project = new Project("Nekomata",
                new Dir(@"%ProgramFiles%\SeaMoon Craft\Nekomata",
                    new DirFiles(Path.Combine(publishDir, "*.*"), f => !f.EndsWith("Nekomata.UI.exe", StringComparison.OrdinalIgnoreCase)),
                    new WixSharp.File(Path.Combine(publishDir, "Nekomata.UI.exe"),
                        new FileAssociation("nkproj")
                        {
                            ContentType = "application/nekomata-project",
                            Description = "Nekomata Project",
                            // We set the Icon attribute manually via Attributes to avoid invalid ID generation from path
                            Attributes = { { "Icon", "NkProjIcon" } } 
                        }
                    ),
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
            project.Version = new Version("0.2.0");
            project.ControlPanelInfo.Manufacturer = "SeaMoon Craft";
            project.OutDir = Path.Combine(projectDir, "dist");
            project.OutFileName = "Nekomata-Setup";
            project.LicenceFile = Path.Combine(projectDir, @"src\Nekomata.Installer\Assets\License.rtf");
            project.UI = WUI.WixUI_InstallDir;

            // Manually inject the Icon element to fix the ProgId/@Icon attribute error
            project.WixSourceGenerated += (doc) =>
            {
                var ns = doc.Root.Name.Namespace;
                var product = doc.Root.Element(ns + "Product");
                product.Add(new XElement(ns + "Icon",
                    new XAttribute("Id", "NkProjIcon"),
                    new XAttribute("SourceFile", iconPath)));
            };

            // Ensure the dist folder exists for the MSI output
            Directory.CreateDirectory(project.OutDir);

            Console.WriteLine("Building MSI...");
            Compiler.BuildMsi(project);
            Console.WriteLine($"MSI created at: {Path.Combine(project.OutDir, project.OutFileName + ".msi")}");
        }
    }
}
