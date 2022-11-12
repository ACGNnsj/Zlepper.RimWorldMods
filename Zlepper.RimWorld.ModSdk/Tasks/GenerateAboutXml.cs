using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Zlepper.RimWorld.ModSdk.Models;
using Zlepper.RimWorld.ModSdk.Utilities;

namespace Zlepper.RimWorld.ModSdk.Tasks;

public class GenerateAboutXml : Task
{
    [Required] public string ModName { get; set; } = null!;

    [Required] public string ModPackageId { get; set; } = null!;

    [Required] public string ModAuthors { get; set; } = null!;

    [Required] public string SteamModContentFolder { get; set; } = null!;

    [Required] public string Description { get; set; } = null!;

    [Required] public string CurrentRimWorldVersion { get; set; } = null!;

    [Required] public string ModOutputFolder { get; set; } = null!;

    [Required] public ITaskItem[] ModDependencies { get; set; } = null!;

    [Required] public ITaskItem[] LoadBeforeMods { get; set; } = null!;
    
    [Required] public ITaskItem[] ProjectReferences { get; set; } = null!;

    [Output] public string AboutXmlFileName { get; set; } = "About/About.xml";

    public override bool Execute()
    {
        var about = new ModMetaData()
        {
            Name = ModName,
            PackageId = ModPackageId,
            Description = DescriptionTrimmer.TrimDescription(Description),
        };
        
        AddModAuthors(about);

        AddSupportedVersions(about);

        AddDependencies(about);

        WriteAboutXmlFile(about);

        return !Log.HasLoggedErrors;
    }

    private void AddModAuthors(ModMetaData about)
    {
        var modAuthors = ModAuthors.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
        if (modAuthors.Length > 1)
        {
            about.Authors = modAuthors.ToRimWorldXmlList();
        }
        else
        {
            about.Author = modAuthors[0];
        }
    }

    private void AddSupportedVersions(ModMetaData about)
    {
        about.SupportedVersions.Add(CurrentRimWorldVersion);

        if (Directory.Exists(ModOutputFolder))
        {
            var versions = Directory.EnumerateDirectories(ModOutputFolder)
                .Select(Path.GetFileName)
                .Where(n => Version.TryParse(n, out _))
                .ToList();
            about.SupportedVersions.AddMissing(versions);
        }

        about.SupportedVersions.ListItems.Sort();
    }

    private void AddDependencies(ModMetaData about)
    {
        var modLocator = new RimWorldModLocator(SteamModContentFolder, CurrentRimWorldVersion, Log);
        
        foreach (var modDependency in ModDependencies)
        {
            var steamModPackageId = modDependency.GetMetadata("Identity");
            var modMetadata = modLocator.FindMod(steamModPackageId);

            if (modMetadata == null)
            {
                Log.LogError($"Could not find mod {steamModPackageId}. Do you actually subscribe to the mod on steam? If yes, is the {nameof(SteamModContentFolder)} configured correctly? Right now it's pointing to '{SteamModContentFolder}'.");
                continue;
            }

            var dep = new ModDependencyItem()
            {
                PackageId = modMetadata.PackageId,
                DisplayName = modMetadata.Name,
                SteamWorkshopUrl = $"steam://url/CommunityFilePage/{modMetadata.FileId}",
                DownloadUrl = modMetadata.Url,
            };
            about.AddModDependency(dep);
        }
        
        foreach (var projectReference in ProjectReferences)
        {
            var fullPath = projectReference.GetMetadata("Fullpath");

            var projectFolder = Path.GetDirectoryName(fullPath)!;
            var aboutXmlPath = Path.Combine(projectFolder, "About", "About.xml");
            if (!File.Exists(aboutXmlPath))
            {
                continue;
            }

            var publishedFileIdPath = Path.Combine(projectFolder, "About", "PublishedFileId.txt");
            var publishedFileId = "-1";
            if (File.Exists(publishedFileIdPath))
            {
                publishedFileId = File.ReadAllText(publishedFileIdPath).Trim();
            }

            var projectMetaData = XmlUtilities.DeserializeFromFile<ModMetaData>(aboutXmlPath);

            var dep = new ModDependencyItem()
            {
                PackageId = projectMetaData.PackageId,
                DisplayName = projectMetaData.Name,
                DownloadUrl = projectMetaData.Url,
                SteamWorkshopUrl = $"steam://url/CommunityFilePage/{publishedFileId}"
            };
            
            about.AddModDependency(dep);
        }

        foreach (var loadBeforeMod in LoadBeforeMods)
        {
            var packageId = loadBeforeMod.GetMetadata("Identity");
            
            about.LoadBefore.Add(packageId);
        }
    }

    private void WriteAboutXmlFile(ModMetaData about)
    {
        var ns = new XmlSerializerNamespaces(new[] {XmlQualifiedName.Empty});
        var serializer = new XmlSerializer(typeof(ModMetaData), "");

        EnsureDirectory(Path.GetDirectoryName(AboutXmlFileName)!);

        using var stream = File.Open(AboutXmlFileName, FileMode.Create, FileAccess.Write, FileShare.None);
        using var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings()
        {
            Encoding = Encoding.UTF8,
            Indent = true,
        });
        xmlWriter.WriteComment(" Generated by Zlepper.RimWorld.ModSdk, please do NOT edit by hand. Your changes will be lost on next build. ");
        serializer.Serialize(xmlWriter, about, ns);
    }

    private ModMetaData? ReadAboutXmlFile(string filePath)
    {
        try
        {
            return XmlUtilities.DeserializeFromFile<ModMetaData>(filePath);
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to read About.xml file at '{filePath}': {e}");
            return null;
        }
    }

    private static void EnsureDirectory(string directoryName)
    {
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }
    }
}