using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PBITUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                // If only one argument is specified, select the action based on the item type (file or folder).
                var fileAttributes = File.GetAttributes(Path.GetFullPath(args[0]));
                if (fileAttributes.HasFlag(FileAttributes.Directory))
                {
                    Generate(Path.GetFullPath(args[0]).TrimEnd('\\'));
                    return;
                }
                else
                {
                    Export(Path.GetFullPath(args[0]));
                    return;
                }
            }
            else if (args.Length == 2)
            {
                switch (args[0])
                {
                    case "-e":
                        Export(Path.GetFullPath(args[1]));
                        return;
                    case "-g":
                        Generate(Path.GetFullPath(args[1]).TrimEnd('\\'));
                        return;
                }
            }

            Console.WriteLine("Usage:");
            Console.WriteLine("\t\"pbitutility -e <file.pbit>\" exports the contents of the PBIT file to flat files.");
            Console.WriteLine("\t\"pbitutility -g <folder.pbit.contents>\" re-generates a PBIT file from the flat files.");
        }

        private static void Export(string pbitFilePath)
        {
            // Check that the file path is valid.
            if (!File.Exists(pbitFilePath))
            {
                Console.WriteLine($"The file {pbitFilePath} does not exist.");
                return;
            }
            else if(!pbitFilePath.EndsWith(".pbit"))
            {
                Console.WriteLine($"{pbitFilePath} must be a .pbit file.");
                return;
            }

            // Delete and re-create the destination folder suffixed with .contents.
            var folderPath = $"{pbitFilePath}.contents";
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
            Directory.CreateDirectory(folderPath);

            // Open the PBIT package and browse its parts.
            var customVisuals = new List<string>();
            using (var package = Package.Open(pbitFilePath, FileMode.Open))
            {
                foreach (var part in package.GetParts())
                {
                    using (var stream = part.GetStream())
                    {
                        // Process the part's URI to create the appropriate sub-folders.
                        var uri = part.Uri.ToString().Replace("/", @"\");
                        var uriPart = uri;
                        var subFolderPath = folderPath;
                        while (uriPart.Substring(1).Contains(@"\"))
                        {
                            var charIndex = uriPart.Substring(1).IndexOf(@"\");
                            subFolderPath += $@"\{uriPart.Substring(1, charIndex)}";

                            // Rename the long names of custom visuals folders to incremental indexes.
                            // This prevents creating very long paths that may be refused by Git.
                            var subFolderToCreate = subFolderPath;
                            if (subFolderPath.StartsWith($@"{folderPath}\Report\CustomVisuals\"))
                            {
                                var customVisualName = subFolderPath.Substring($@"{folderPath}\Reports\CustomVisuals\".Length - 1);
                                if (customVisualName.IndexOf(@"\") > 0)
                                {
                                    customVisualName = customVisualName.Substring(0, customVisualName.IndexOf(@"\"));
                                }

                                if (!customVisuals.Contains(customVisualName))
                                {
                                    customVisuals.Add(customVisualName);
                                }
                                subFolderToCreate = subFolderPath.Replace(customVisualName, customVisuals.IndexOf(customVisualName).ToString());
                                uri = uri.Replace(customVisualName, customVisuals.IndexOf(customVisualName).ToString());
                            }
                            
                            if (!Directory.Exists(subFolderToCreate))
                                Directory.CreateDirectory(subFolderToCreate);

                            uriPart = uriPart.Substring(charIndex + 1);
                        }

                        // Write the data from the part to a file (eventually add an extension).
                        var extension = GetExtensionForUri(part.Uri.ToString());
                        var filePath = $"{folderPath}{uri}{extension}";
                        using (var fileStream = File.Open(filePath, FileMode.Create))
                        {
                            // If the part is encoded in UTF-16 in the PBIT package, convert it to UTF-8 (encoding better handled by file comparison tools).
                            CopyStream(part.GetStream(), fileStream, IsUTF16(part.Uri.ToString()) ? EncodingConversion.UTF16ToUTF8 : EncodingConversion.None);
                        }

                        // If the exported file is a JSON, beautify it.
                        if (extension == ".json")
                        {
                            var beautifiedJSON = BeautifyJSON(File.ReadAllText(filePath));
                            File.WriteAllText(filePath, beautifiedJSON);
                        }
                    }
                }
            }

            // Add the ReadMe file.
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().Single(x => x.EndsWith("ReadMe.txt"));
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var fileStream = File.Create($@"{folderPath}\ReadMe.txt"))
                {
                    CopyStream(stream, fileStream);
                }
            }

            // Add the TableOfContents.json file for renamed custom visual folders.
            if (customVisuals.Any())
            {
                File.WriteAllText($@"{folderPath}\Report\CustomVisuals\TableOfContents.json", JsonSerializer.Serialize(customVisuals));
            }
        }

        private static void Generate(string folderPath)
        {
            // Check that the folder path is valid.
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"The folder {folderPath} does not exist.");
                return;
            }
            else if (!folderPath.EndsWith(".pbit.contents"))
            {
                Console.WriteLine($"{folderPath} must end with \".pbit.contents\".");
                return;
            }

            // Delete the destination PBIT package if it already exists.
            var pbitFilePath = folderPath.Substring(0, folderPath.Length - ".contents".Length);
            if (File.Exists(pbitFilePath))
            {
                File.Delete(pbitFilePath);
            }

            // If there is a table of contents for renamed custom visual folders, read it first.
            var customVisuals = new List<string>();
            if (File.Exists($@"{folderPath}\Report\CustomVisuals\TableOfContents.json"))
            {
                customVisuals = JsonSerializer.Deserialize<List<string>>(File.ReadAllText($@"{folderPath}\Report\CustomVisuals\TableOfContents.json"));
            }

            // Browse the files in the specified folder to re-generate the PBIT package.
            var filePaths = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).ToList();
            using (var package = Package.Open(pbitFilePath, FileMode.Create))
            {
                foreach (var filePath in filePaths)
                {
                    // Skip the ReadMe file, TableOfContents.json and SecurityBindings.
                    // IMPORTANT: We noticed that ignoring the SecurityBindings file prevents corruption of the regenerated PBIT file with altered JSON files.
                    //            Power BI seems to re-create this file properly on the next save, but since this is not documented, there may be some unexpected side effects.
                    if (filePath.EndsWith(@"\ReadMe.txt") || filePath.EndsWith(@"\Report\CustomVisuals\TableOfContents.json") || filePath.EndsWith(@"\SecurityBindings"))
                    {
                        continue;
                    }

                    // Remove the eventual file extension added during export.
                    var fileName = filePath.Replace(folderPath, "");
                    var extension = GetExtensionForUri(fileName);
                    var fileNameWithoutExtension = fileName.Substring(0, fileName.Length - extension.Length);

                    // If the current file is a JSON, create and import a minified version instead.
                    var filePathToImport = filePath;
                    if (extension == ".json")
                    {
                        filePathToImport = $"{filePath}_minified";
                        var minifiedJSON = MinifyJSON(File.ReadAllText(filePath));
                        File.WriteAllText(filePathToImport, minifiedJSON);
                    }

                    // Restore the custom visual name replaced by an incremental index.
                    var uriName = fileNameWithoutExtension;
                    if (uriName.Contains(@"\Report\CustomVisuals\"))
                    {
                        for (var i = 0; i < customVisuals.Count; i++)
                        {
                            uriName = uriName.Replace($@"\{i}\", $@"\{customVisuals[i]}\").Replace($@"\{i}.pbiviz.json", $@"\{customVisuals[i]}.pbiviz.json");
                        }
                    }

                    // Initialize the part by determing its URI based on its location within the arborescence of folders.
                    var uri = PackUriHelper.CreatePartUri(new Uri(uriName, UriKind.Relative));
                    PackagePart part = package.CreatePart(uri, GetContentTypeForUri(uri), CompressionOption.Normal);

                    // Write the data from the file to the part.
                    using (var fileStream = File.Open(filePathToImport, FileMode.Open, FileAccess.Read))
                    {
                        // If the part must be encoded in UTF-16 in the PBIT package, convert it back to UTF-16.
                        CopyStream(fileStream, part.GetStream(), IsUTF16(fileName) ? EncodingConversion.UTF8ToUTF16 : EncodingConversion.None);
                    }

                    // If created, delete the temporary minified JSON file.
                    if (filePathToImport != filePath)
                    {
                        File.Delete(filePathToImport);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the MIME content type of the specified URI.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetContentTypeForUri(Uri uri)
        {
            switch (uri.ToString())
            {
                case "/Metadata":
                    return "application/json";
                case "/Settings":
                    return "application/json";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Returns the extension of the specified URI.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetExtensionForUri(string uri)
        {
            switch (uri)
            {
                case "/Connections":
                case @"\Connections.json":
                    return ".json";
                case "/DataModelSchema":
                case @"\DataModelSchema.json":
                    return ".json";
                case "/DiagramLayout":
                case @"\DiagramLayout.json":
                    return ".json";
                case "/DiagramState":
                case @"\DiagramState.json":
                    return ".json";
                case "/Metadata":
                case @"\Metadata.json":
                    return ".json";
                case "/Settings":
                case @"\Settings.json":
                    return ".json";
                case "/Version":
                case @"\Version.txt":
                    return ".txt";
                case "/Report/Layout":
                case @"\Report\Layout.json":
                    return ".json";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Returns true if the specified URI is encoded in UTF-16 in a PBIT package.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsUTF16(string uri)
        {
            switch (uri)
            {
                case "/DataModelSchema":
                case @"\DataModelSchema.json":
                    return true;
                case "/DiagramLayout":
                case @"\DiagramLayout.json":
                    return true;
                case "/DiagramState":
                case @"\DiagramState.json":
                    return true;
                case "/Metadata":
                case @"\Metadata.json":
                    return true;
                case "/Settings":
                case @"\Settings.json":
                    return true;
                case "/Version":
                case @"\Version.txt":
                    return true;
                case "/Report/Layout":
                case @"\Report\Layout.json":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns a beautified JSON string.
        /// </summary>
        /// <param name="minifiedJSON">The minified JSON</param>
        private static string BeautifyJSON(string minifiedJSON)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(minifiedJSON);

            return JsonSerializer.Serialize(jsonElement, options);
        }

        /// <summary>
        /// Returns a minified JSON string.
        /// </summary>
        /// <param name="beautifiedJSON">The beautified JSON</param>
        private static string MinifyJSON(string beautifiedJSON)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(beautifiedJSON);

            return JsonSerializer.Serialize(jsonElement, options);
        }

        /// <summary>
        /// Copies data from the source stream to the target stream.
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <param name="target">Target stream</param>
        /// <param name="encodingConversion">Text encoding conversion to apply when copying stream</param>
        private static void CopyStream(Stream source, Stream target, EncodingConversion encodingConversion = EncodingConversion.None)
        {
            const int BUFFER_SIZE = 0x1000;

            byte[] bufferRead = new byte[BUFFER_SIZE];
            int size = 0;
            while ((size = source.Read(bufferRead, 0, BUFFER_SIZE)) > 0)
            {
                var bufferToWrite = bufferRead;

                // If requested, convert encoding.
                if (encodingConversion == EncodingConversion.UTF16ToUTF8)
                {
                    var unicode = new UnicodeEncoding(false, false);
                    var content = unicode.GetString(bufferRead, 0, size);

                    bufferToWrite = Encoding.UTF8.GetBytes(content);
                    size = bufferToWrite.Length;
                }
                else if (encodingConversion == EncodingConversion.UTF8ToUTF16)
                {
                    var content = Encoding.UTF8.GetString(bufferRead, 0, size);

                    var unicode = new UnicodeEncoding(false, false);
                    bufferToWrite = unicode.GetBytes(content);
                    size = bufferToWrite.Length;
                }

                target.Write(bufferToWrite, 0, size);
            }
        }

        private enum EncodingConversion
        {
            UTF16ToUTF8,
            UTF8ToUTF16,
            None
        }
    }
}
