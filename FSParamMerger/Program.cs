using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SoulsFormats;
using Spectre.Console;

namespace FSParamMerger
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Welcome to FSParamMerger, this software allows you to merge 2 params from the same game.\nIn order to proceed follow the onscreen instructions:\n   Copy your source param to {Path.GetFullPath("ParamSource")}\\\n  Copy your target param file to {Path.GetFullPath("ParamTarget")}\\\nThen press Enter in order to proceed");
            Console.ReadLine();
            Console.Clear();
            string gameType = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select the Game for which you desire to merge params for, choosing the wrong game might damage your params [red]ENTER[/]")
                        .MoreChoicesText("[grey](Move up and down to reveal more Games)[/]")
                        .AddChoices(new string[] { "SDT", "DS3", "BB", "DS2S", "DS1R", "DS1", "DES" }));

            var targetParamDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParamTarget");
            var sourceParamDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParamSource");

            var targetParams = Directory.GetFiles(targetParamDirPath, "*.dcx");
            var sourceParams = Directory.GetFiles(sourceParamDirPath, "*.dcx");

            string firstSourceParamPath = sourceParams.Any() ? sourceParams.First() : null;
            string firstTargetParamPath = targetParams.Any() ? targetParams.First() : null;

            if (firstSourceParamPath == null || firstTargetParamPath == null)
            {
                if (firstSourceParamPath == null)
                {
                    Console.WriteLine($"There is no source Param file in in: {sourceParamDirPath}");
                }
                else if (firstTargetParamPath == null)
                {
                    Console.WriteLine($"There is no source Param file in in: {targetParamDirPath}");
                }
                goto ExitPoint;
            }
            //else if (Path.GetFileName(firstSourceParamPath) is var fileSourceName && Path.GetFileName(firstSourceParamPath) is var fileTargetName && fileSourceName != fileTargetName)
            //{
            //    Console.WriteLine($"Source Param Name is: {firstSourceParamPath}");
            //    Console.WriteLine($"Target Param Name is: {fileTargetName}");
            //    Console.WriteLine("Source and Target Param Name are not matching, terminating program.");
            //    goto ExitPoint;
            //}

            var (paramSourcePath, paramTargetPath) = args.Length switch
            {
                2 => (args[0], args[1]),
                _ => (firstSourceParamPath, firstTargetParamPath),
            };

            ParamsRW paramSource = new ParamsRW(paramSourcePath, gameType);
            ParamsRW paramTarget = new ParamsRW(paramTargetPath, gameType);

            CompareAndMergeParams(paramTarget, paramSource);

        ExitPoint:
            Console.WriteLine("Press Any Key To Exist...");
            Console.ReadLine();
            return;
        }

        static void CompareAndMergeParams(ParamsRW target, ParamsRW source)
        {
            //var doc = new XDocument(new XElement("Root"));
            var stream = new StreamWriter("MergeResults.txt", false);
            Console.WriteLine("Merging params...");
            foreach (var (keySource, paramSource) in source.paramDictionary)
            {
                //var paramElement = new XElement(Path.GetFileNameWithoutExtension(keySource));
                //doc.Root.Add(paramElement);
                stream.WriteLine(Path.GetFileNameWithoutExtension(keySource));
                if (target.paramDictionary.TryGetValue(keySource, out PARAM paramTarget))
                {
                    Console.WriteLine(paramSource.ParamType);
                    Console.WriteLine(paramTarget.ParamType);

                    for (int iRow = 0; iRow < paramSource.Rows.Count(); iRow++)
                    {
                        var rowSource = paramSource.Rows[iRow];
                        var rowTargetListDummy = paramTarget.Rows.Where(row => row.ID == rowSource.ID);
                        if (rowTargetListDummy.Any())
                        {
                            var rowTarget = rowTargetListDummy.First();
                            for (int iCell = 0; iCell < rowSource.Cells.Count(); iCell++)
                            {
                                var cellSource = rowSource.Cells[iCell];
                                var cellTarget = rowTarget.Cells[iCell];
                                if (cellSource.Value.ToString() != cellTarget.Value.ToString())
                                {
                                    //paramElement.Add(new XElement("Diff", new XAttribute("Row", rowSource.ID), new XAttribute("Cell", iCell)) { Value = $"Source = {cellSource.Value}, Target = {cellTarget.Value}" });
                                    stream.WriteLine($"    Row - Name({rowSource.Name}), ID({rowSource.ID}), Cell({iCell}): Source({cellSource.Value}) => Target({cellTarget.Value})");
                                    cellTarget.Value = cellSource.Value;
                                }
                            }
                        }
                        else
                        {
                            //paramElement.Add(new XElement("Diff", new XAttribute("Row", rowSource.ID), new XAttribute("Cell", -1)) { Value = $"Row not present in Target" });
                            paramTarget.Rows.Add(rowSource);
                            stream.WriteLine($"    Row - Name({rowSource.Name}), ID({rowSource.ID}), Cell({"All"}): Row added to target");
                        }
                    }
                }
                //Console.WriteLine(doc);
            }
            target.Write();
            stream.Close();
            Console.WriteLine($"Param Merging was successful check {Path.GetFullPath("MergeResults.txt")} for details");
        }
        public static string RemoveSpecialCharacters(this string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public class ParamsRW
        {
            private BND4 gameParamBND4;
            private BND3 gameParamBND3;
            private string bndPath;
            public Dictionary<string, PARAM> paramDictionary;

            public ParamsRW(string gameparamBNDPath, string gameType)
            {
                if (BND4.IsRead(gameparamBNDPath, out BND4 paramsBinder4))
                {
                    bndPath = gameparamBNDPath;
                    gameParamBND4 = paramsBinder4;
                    var paramDictionary = new Dictionary<string, PARAM>();
                    var paramDefList = new List<PARAMDEF>();
                    new List<string>(Directory.GetFiles($"Dependencies\\Paramdex\\{gameType}\\Defs", "*.xml")).ForEach(paramDef => paramDefList.Add(PARAMDEF.XmlDeserialize(paramDef)));
                    foreach (var file in paramsBinder4.Files)
                    {
                        if (file.Name.EndsWith(".param"))
                        {
                            PARAM param = PARAM.Read(file.Bytes);
                            if (param.ApplyParamdefCarefully(paramDefList))
                            {
                                paramDictionary.Add(file.Name, param);
                            }
                        }
                    }
                    this.paramDictionary = paramDictionary;
                }
                else if (BND3.IsRead(gameparamBNDPath, out BND3 paramsBinder3))
                {
                    bndPath = gameparamBNDPath;
                    gameParamBND3 = paramsBinder3;
                    var paramDictionary = new Dictionary<string, PARAM>();
                    var paramDefList = new List<PARAMDEF>();
                    new List<string>(Directory.GetFiles($"Dependencies\\Paramdex\\{gameType}\\Defs", "*.xml")).ForEach(paramDef => paramDefList.Add(PARAMDEF.XmlDeserialize(paramDef)));
                    foreach (var file in paramsBinder3.Files)
                    {
                        if (file.Name.EndsWith(".param"))
                        {
                            PARAM param = PARAM.Read(file.Bytes);
                            if (param.ApplyParamdefCarefully(paramDefList))
                            {
                                paramDictionary.Add(file.Name, param);
                            }
                        }
                    }
                    this.paramDictionary = paramDictionary;
                }
            }

            public void Write()
            {
                Write(this.bndPath);
            }

            public void Write(string bndPath)
            {
                if (gameParamBND4 != null)
                {
                    foreach (var file in gameParamBND4.Files)
                    {
                        if (paramDictionary.TryGetValue(file.Name, out PARAM param))
                        {
                            file.Bytes = param.Write();
                        }
                    }
                    gameParamBND4.Write(bndPath);
                }
                else if (gameParamBND3 != null)
                {
                    foreach (var file in gameParamBND3.Files)
                    {
                        if (paramDictionary.TryGetValue(file.Name, out PARAM param))
                        {
                            file.Bytes = param.Write();
                        }
                    }
                    gameParamBND3.Write(bndPath);
                }
            }
        }
    }
}
