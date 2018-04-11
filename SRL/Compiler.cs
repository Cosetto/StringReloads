﻿using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SRL {
    static partial class StringReloader {

        /// <summary>
        /// Build the String.srl
        /// </summary>
        internal static void CompileStrMap() {
            var DBAr = new List<SRLDatabase2>();

            var COri = new List<char>();
            var CFak = new List<char>();
            var UOri = new List<char>();

            var UErr = new List<ushort>();
            var ROri = new List<string>();
            var RNew = new List<string>();

            
            if (File.Exists(CharMapSrc)) {
                Log("Compiling Char Reloads...");
                using (TextReader Reader = File.OpenText(CharMapSrc)) {
                    while (Reader.Peek() >= 0) {
                        string line = Reader.ReadLine();
                        if (line.Length == 3 && line[1] == '=') {
                            char cOri = line[0], cFak = line[2];

                            COri.Add(cOri);
                            CFak.Add(cFak);
                        }

                        if (line.Contains("0x") && line.Contains('=')) {
                            string hex = line.Split('=')[1].Split('x')[1];
                            ushort Val = ushort.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                            UOri.Add(line[0]);
                            UErr.Add(Val);
                        }
                    }
                    Reader.Close();
                }
            }

            Log("Generating String Reload Database...");

            //Splited String Dump
            string[] TLMaps = Directory.GetFiles(BaseDir, Path.GetFileName(string.Format(TLMapSrcMsk, "*")));
            foreach (string TLMap in TLMaps) {
                var In = new List<string>();
                var Out = new List<string>();

                ReadDump(TLMap, ref In, ref Out, IgnoreMask: true);

                string DBN = Path.GetFileName(TLMap);
                DBN = MaskReplace(Path.GetFileName(TLMapSrcMsk), DBN, "{0}");

                DBAr.Add(new SRLDatabase2() {
                    Original = In.ToArray(),
                    Replace = Out.ToArray(),
                    Name = DBN
                });

                Log("{0} Found, Importing, Database ID: {1}...", false, Path.GetFileName(TLMap), DBAr.Count-1);
            }

            if (File.Exists(TLMapSrc)) {
                var In = new List<string>();
                var Out = new List<string>();

                ReadDump(TLMapSrc, ref In, ref Out, IgnoreMask: true);
                DBAr.Add(new SRLDatabase2() {
                    Original = In.ToArray(),
                    Replace = Out.ToArray(),
                    Name = Path.GetFileNameWithoutExtension(TLMapSrc)
                });

                Log("{0} Found, Importing, Database ID: {1}...", false, Path.GetFileName(TLMapSrc), DBAr.Count - 1);
            }

            SearchViolations(DBAr.ToArray(), CFak.ToArray());

            Log("{0} Databases Generated.", true, DBAr.Count);
            
            if (File.Exists(ReplLst)) {
                Log("Compiling Replace List...");
                ReadDump(ReplLst, ref ROri, ref RNew);
            }

            Log("Building String Reloads...");
            SRLData3 Data = new SRLData3() {
                Signature = "SRL3",
                Databases = DBAr.ToArray(),
                OriLetters = COri.ToArray(),
                MemoryLetters = CFak.ToArray(),
                UnkChars = UErr.ToArray(),
                UnkReps = UOri.ToArray(),
                RepOri = ROri.ToArray(),
                RepTrg = RNew.ToArray()
            };

            if (File.Exists(TLMap))
                File.Delete(TLMap);

            using (StructWriter Writer = new StructWriter(TLMap)){
                Writer.WriteStruct(ref Data);
                Writer.Close();
            }
            Log("Builded Successfully.");
        }

        private static void SearchViolations(SRLDatabase2[] DBS, char[] Ilegals) {
            Log("Serching Chars Violations...");
            List<string> Violations = new List<string>();

            foreach (SRLDatabase2 Database in DBS) {
                foreach (string String in Database.Replace)
                    foreach (char Ilegal in Ilegals)
                        if (String.Contains(Ilegal))
                            Violations.Add(String);
            }

            if (Violations.LongCount() != 0) {
                Warning("{0} Strings contains a char violation.", Violations.LongCount());
                foreach (string Violation in Violations) {
                    Warning("\"{0}\" contains a char violation.", Violation);
                }
            }
        }
        
        /// <summary>
        /// Load the String.srl Data
        /// </summary>
        static void LoadData() {
            Log("Initializing String Reloads...", true);
            StartPipe();
            var Data = new SRLData3();

            try {
                using (StructReader Reader = new StructReader(TLMap)) {
                    if (Reader.PeekInt() == 0x43424C54) {
                        TLBCParser(Reader);
                        return;
                    }
                    if (Reader.PeekInt() == 0x4C5253) {
                        SRL1Parser(Reader);
                        return;
                    }
                    if (Reader.PeekInt() == 0x324C5253) {
                        SRL2Parser(Reader);
                        return;
                    }
                    if (Reader.PeekInt() != 0x334C5253) {
                        Error("Failed to Initialize - Corrupted Data");
                        Thread.Sleep(5000);
                        Environment.Exit(2);
                    }

                    Reader.Seek(4, 0);
                    if (Reader.ReadUInt32() != 0x00) {
                        Error("Unexpected SRL Database Format");
                        Thread.Sleep(5000);
                        Environment.Exit(2);
                    }
                    Reader.Seek(0, 0);

                    Reader.ReadStruct(ref Data);
                    Reader.Close();
                }

                Log("Processing Char Reloads... 1/2", true);
                CharRld = new Dictionary<ushort, char>();
                for (uint i = 0; i < Data.OriLetters.LongLength; i++) {
                    char cOri = Data.OriLetters[i];
                    char cPrx = Data.MemoryLetters[i];
                    if (!CharRld.ContainsKey(cPrx)) {
                        CharRld.Add(cPrx, cOri);
                        AppendArray(ref Replaces, cOri.ToString());
                        AppendArray(ref Replaces, cPrx.ToString());

                        Range Range = new Range() {
                            Min = cPrx,
                            Max = cPrx
                        };

                        if (!Ranges.Contains(Range))
                            Ranges.Add(Range);
                    }
                }

                Log("Processing Char Reloads... 2/2", true);
                UnkRld = new Dictionary<ushort, char>();
                for (uint i = 0; i < Data.UnkChars.LongLength; i++) {
                    ushort c = Data.UnkChars[i];
                    if (!UnkRld.ContainsKey(c)) {
                        UnkRld.Add(c, Data.UnkReps[i]);
                    }
                }

                Log("Chars Reloads Initialized, Total entries: {0} + {1}", true, UnkRld.Count, CharRld.Count);
                Log("Processing String Reloads...", true);
                List<string> Temp = new List<string>();
                StrRld = new Dictionary<string, string>();
                long ReloadEntries = 0, MaskEntries = 0;
                foreach (SRLDatabase2 Database in Data.Databases) {
                    for (uint i = 0; i < Database.Original.LongLength; i++) {
                        Application.DoEvents();
                        string str = SimplfyMatch(Database.Original[i]);
                        if (!ContainsKey(str, true)) {
                            if (IsMask(Database.Original[i])) {
                                if (LiteralMaskMatch) {
                                    AddEntry(str, ReplaceChars(Database.Replace[i]));
                                    ReloadEntries++;
                                }

                                if (Database.Replace[i].StartsWith(AntiMaskParser)) {
                                    Database.Replace[i] = Database.Replace[i].Substring(AntiMaskParser.Length, Database.Replace[i].Length - AntiMaskParser.Length);
                                } else {
                                    //Prevent Duplicates
                                    if (!Temp.Contains(Database.Original[i]))
                                        Temp.Add(Database.Original[i]);
                                    else
                                        continue;

                                    AddMask(Database.Original[i], ReplaceChars(Database.Replace[i]));
                                    MaskEntries++;
                                    continue;
                                }
                            } else {
                                AddEntry(str, ReplaceChars(Database.Replace[i]));
                                ReloadEntries++;
                            }
                        }
                    }

                    if (MultipleDatabases)
                        FinishDatabase();
                }
                Log("String Reloads Initialized, {0} Databases Created, {1} Reload Entries, {2} Mask Entries", true, Databases.Count-1, ReloadEntries, MaskEntries);
                Log("Initializing Replaces...", true);
                for (uint i = 0; i < Data.RepOri.LongLength; i++) {
                    AppendArray(ref Replaces, Data.RepOri[i]);
                    AppendArray(ref Replaces, Data.RepTrg[i]);
                }


                Log("Registring Databases Name...");                
                DBNames = new Dictionary<long, string>();
                for (long i = 0; i < Data.Databases.LongLength; i++) {
                    DBNames[i] = Data.Databases[i].Name;

                    if (LogAll)
                        Log("Database ID: {0} Named As: {1}", true, i, DBNames[i]);
                }

                Log("Loading Complete.", true);
            } catch (Exception ex) {
                Error("Failed to Execute: {0}\n=========\n{1}", false, ex.Message, ex.StackTrace);
                Thread.Sleep(5000);
                Environment.Exit(2);
            }
        }

        /// <summary>
        /// Convert TLBot Cache to SRL format
        /// </summary>
        /// <param name="Reader">Input Stream</param>
        private static void TLBCParser(StructReader Reader) {
            Log("TLBot Cache detected... Rebuilding...");
            var Cache = new TLBC();
            Reader.ReadStruct(ref Cache);
            Reader.Close();

            if (File.Exists(TLMapSrc))
                File.Delete(TLMapSrc);

            for (uint i = 0; i < Cache.Original.Length; i++) {
                AppendLst(Cache.Original[i], Cache.Replace[i], TLMapSrc);
            }

            File.Delete(TLMap);
            Log("Restarting...");
            Init();
        }

        /// <summary>
        /// Convert SRL1 Database to the SRL2 Format
        /// </summary>
        /// <param name="Reader">Input Stream</param>
        private static void SRL1Parser(StructReader Reader) {
            Log("SRL1 Database Detected... Rebuilding...");
            var DB = new SRLData1();
            Reader.ReadStruct(ref DB);
            Reader.Close();

            if (File.Exists(TLMapSrc))
                File.Delete(TLMapSrc);

            for (uint i = 0; i < DB.Original.Length; i++) {
                AppendLst(DB.Original[i], DB.Replace[i], TLMapSrc);
            }
            if (DB.OriLetters.LongLength + DB.UnkReps.LongLength != 0) {
                Log("Dumping Char Reloads...", true);
                using (TextWriter Output = File.CreateText(CharMapSrc)) {
                    for (uint i = 0; i < DB.OriLetters.LongLength; i++) {
                        Output.WriteLine("{0}={1}", DB.OriLetters[i], DB.MemoryLetters[i]);
                    }
                    for (uint i = 0; i < DB.UnkReps.LongLength; i++) {
                        Output.WriteLine("{0}=0x{1:X4}", DB.UnkReps[i], DB.UnkChars[i]);
                    }
                    Output.Close();
                }
            }

            if (DB.OriLetters.LongLength != 0) {
                Log("Dumping Replaces...", true);

                for (uint i = 0; i < DB.OriLetters.LongLength; i++) {
                    try {
                        string L1 = DB.RepOri[i];
                        string L2 = DB.RepTrg[i];
                        AppendLst(L1, L2, ReplLst);
                    } catch { }
                }
            }


            File.Delete(TLMap);
            Log("Restarting...");
            Init();
        }
        
        /// <summary>
         /// Convert SRL2 Database to the SRL3 Format
         /// </summary>
         /// <param name="Reader">Input Stream</param>
        private static void SRL2Parser(StructReader Reader) {
            Log("SRL2 Database Detected... Rebuilding...");
            var DB = new SRLData2();
            Reader.ReadStruct(ref DB);
            Reader.Close();

            if (File.Exists(TLMapSrc))
                File.Delete(TLMapSrc);

            for (ushort x = 0; x < DB.Databases.Length; x++) {
                Log("Dumping Database Id: {0}", true, x);
                SRLDatabase Database = DB.Databases[x];
                for (uint i = 0; i < Database.Original.Length; i++) {
                    AppendLst(Database.Original[i], Database.Replace[i], string.Format(TLMapSrcMsk, x));
                }
            }

            if (DB.OriLetters.LongLength + DB.UnkReps.LongLength != 0) {
                Log("Dumping Char Reloads...", true);
                using (TextWriter Output = File.CreateText(CharMapSrc)) {
                    for (uint i = 0; i < DB.OriLetters.LongLength; i++) {
                        Output.WriteLine("{0}={1}", DB.OriLetters[i], DB.MemoryLetters[i]);
                    }
                    for (uint i = 0; i < DB.UnkReps.LongLength; i++) {
                        Output.WriteLine("{0}=0x{1:X4}", DB.UnkReps[i], DB.UnkChars[i]);
                    }
                    Output.Close();
                }
            }

            if (DB.OriLetters.LongLength != 0) {
                Log("Dumping Replaces...", true);

                for (uint i = 0; i < DB.OriLetters.LongLength; i++) {
                    try {
                        string L1 = DB.RepOri[i];
                        string L2 = DB.RepTrg[i];
                        AppendLst(L1, L2, ReplLst);
                    } catch { }
                }
            }


            File.Delete(TLMap);
            Log("Restarting...");
            Init();
        }
    }
}