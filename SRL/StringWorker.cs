﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SRL {
    partial class StringReloader {

        /// <summary>
        /// Detect the correct string reader method, and load it
        /// </summary>
        /// <param name="Pointer">The pointer to the string</param>
        /// <param name="Decode">if False, return the hex of the string</param>
        /// <returns>The String</returns>
        internal static string GetString(IntPtr Pointer, bool Decode = true) {
            if (Modifier != null)
                return Modifier.Call("Modifier", "GetString", Pointer, Decode);

            if (Unicode)
                return GetStringW(Pointer, Decode);
            else
                return GetStringA(Pointer, Decode);
        }

        /// <summary>
        /// Gen a Null Terminated String
        /// </summary>
        /// <param name="String">The string</param>
        /// <returns>The Pointer to the new String</returns>
        internal static IntPtr GenString(string String) {
            byte[] buffer;
            if (Modifier != null) {
                buffer = Modifier.Call("Modifier", "GenString", String);
            } else {
                int len = WriteEncoding.GetByteCount(String + "\x0");
                buffer = new byte[len];
                WriteEncoding.GetBytes(String, 0, String.Length, buffer, 0);
#if LEAKING
                IntPtr Pointer = LastGenerated;
                if (LastGenerated == IntPtr.Zero) {
                    Pointer = Marshal.AllocHGlobal(buffer.Length);
                } else {
                    if (AllocLen < buffer.Length) {
                        while (AllocLen < buffer.Length)
                            AllocLen++;
                        Pointer = Marshal.ReAllocHGlobal(Pointer, new IntPtr(AllocLen));
                    }
                }
                LastGenerated = Pointer            
#endif
            }

            IntPtr Pointer = Marshal.AllocHGlobal(buffer.Length);

            Marshal.Copy(buffer, 0, Pointer, buffer.Length);

            if (LogString) {
                string New = GetString(Pointer);
                Log("Old: {0}\nNew: {1}\nHex: {2}", false, String, New, ParseBytes(buffer));
            }

            return Pointer;
        }

        /// <summary>
        /// Get a null terminated String
        /// </summary>
        /// <param name="Pointer">Pointer to the string</param>
        /// <param name="Decode">if False, return the hex of the string</param>
        /// <returns></returns>
        internal static string GetStringA(IntPtr Pointer, bool Decode = true) {
            int len = 0;
            while (Marshal.ReadByte(Pointer, len) != 0)
                ++len;

            byte[] buffer = new byte[len];
            Marshal.Copy(Pointer, buffer, 0, buffer.Length);

            if (LogString) {
                Log("Input: {0}", true, ParseBytes(buffer));
            }

            if (Unicode) {
                return Encoding.Default.GetString(buffer);
            } else {
                if (Decode)
                    return ReadEncoding.GetString(buffer);
                else
                    return ParseBytes(buffer);
            }
        }

        /// <summary>
        /// Get WideByte null terminated String
        /// </summary>
        /// <param name="Pointer">Pointer to the string</param>
        /// <param name="Decode">if False, return the hex of the string</param>
        /// <returns></returns>
        internal static string GetStringW(IntPtr Pointer, bool Decode = true) {
            int len = 0;
            while (Marshal.ReadInt16(Pointer, len) != 0)
                len += 2;
            byte[] buffer = new byte[len];
            Marshal.Copy(Pointer, buffer, 0, buffer.Length);

            if (LogString) {
                Log("Input: {0}", true, ParseBytes(buffer));
            }

            if (Decode)
                return ReadEncoding.GetString(buffer);
            else
                return ParseBytes(buffer);
        }


        /// <summary>
        /// Check if a Char is in the User Acceptable Range
        /// </summary>
        /// <param name="Char">The Char to Check</param>
        /// <returns>If the char is in the acceptable range, return true</returns>
        internal static bool InRange(char Char) {
            foreach (Range Range in Ranges)
                if (Char >= Range.Min && Char <= Range.Max)
                    return true;
            return false;
        }

        /// <summary>
        /// Restore the content removed by the Trim
        /// </summary>
        /// <param name="String">String to Restore</param>
        /// <param name="Original">Original Template</param>
        internal static void TrimWorker(ref string String, string Original) {
            String = TrimString(String);

            string Test = TrimStart(Original);
            int Diff = Original.Length - Test.Length;

            String = Original.Substring(0, Diff) + String;
            Test = TrimEnd(Original);

            Diff = Original.Length - Test.Length;
            String += Original.Substring(Original.Length - Diff, Diff);
        }

        /// <summary>
        /// Add a Reply to the Cache
        /// </summary>
        /// <param name="Str">The Reply String</param>
        internal static void CacheReply(string Str) {
            string Reply = SimplfyMatch(Str);
            if (ReplyPtr > 100)
                ReplyPtr = 0;
            Replys.Insert(ReplyPtr++, Reply);

            if (Debugging)
                Log("\"{0}\" Added to the cache at {1}", false, Reply, ReplyPtr - 1);
        }


        /// <summary>
        /// Minify a String at the max.
        /// </summary>
        /// <param name="Str">The string to Minify</param>
        /// <returns>The Minified String</returns>
        internal static string SimplfyMatch(string Str) {
            if (SpecialLineBreaker) {
                Str = Str.Replace(GameLineBreaker, "\n");
            }

            string Output = TrimString(Str);
            for (int i = 0; i < MatchDel.Length; i++)
                Output = Output.Replace(MatchDel[i], "");
            return Output;
        }

        /// <summary>
        /// Replace all user matchs in the string
        /// </summary>
        /// <param name="Input">The String to Replace</param>
        /// <returns>The Result String</returns>
        internal static string ReplaceChars(string Input) {
            string Output = Input;
            for (int i = 0; i < Replaces.Length; i += 2)
                Output = Output.Replace(Replaces[i], Replaces[i + 1]);
            return Output;
        }


        /// <summary>
        /// Trim a String
        /// </summary>
        /// <param name="Txt">The String to Trim</param>
        /// <returns>The Result</returns>
        internal static string TrimString(string Input) {
            string Result = Input;
            Result = TrimStart(Result);
            Result = TrimEnd(Result);
            return Result;
        }

        /// <summary>
        /// Trim the Begin of the String
        /// </summary>
        /// <param name="Txt">The String to Trim</param>
        /// <returns>The Result</returns>
        internal static string TrimStart(string Txt) {
            string rst = Txt;
            foreach (string str in TrimChars) {
                if (string.IsNullOrEmpty(str))
                    continue;
                while (rst.StartsWith(str)) {
                    rst = rst.Substring(str.Length, rst.Length - str.Length);
                }
            }

            if (TrimRangeMissmatch && Ranges != null) {
                while (!string.IsNullOrEmpty(rst) && !InRange(rst[0])) {
                    rst = rst.TrimStart(rst[0]);
                }
            }

            if (rst != Txt)
                rst = TrimStart(rst);

            return rst;
        }

        /// <summary>
        /// Trim the End of the String
        /// </summary>
        /// <param name="Txt">The String to Trim</param>
        /// <returns>The Result</returns>
        internal static string TrimEnd(string Txt) {
            string rst = Txt;
            foreach (string str in TrimChars) {
                if (string.IsNullOrEmpty(str))
                    continue;
                while (rst.EndsWith(str)) {
                    rst = rst.Substring(0, rst.Length - str.Length);
                }
            }
            
            if (TrimRangeMissmatch && Ranges != null) {
                while (!string.IsNullOrEmpty(rst) && !InRange(rst[rst.Length-1])) {
                    rst = rst.TrimEnd(rst[rst.Length - 1]);
                }
            }

            if (rst != Txt)
                rst = TrimEnd(rst);

            return rst;
        }

        /// <summary>
        /// Convert a Byte Array to Hex
        /// </summary>
        /// <param name="Arr">Byte Array to Convert</param>
        /// <returns>The Result Hex</returns>
        internal static string ParseBytes(byte[] Arr) {
            string Result = "0x";
            foreach (byte b in Arr)
                Result += string.Format("{0:X2}", b);
            return Result;
        }      

        /// <summary>
        /// Check if the string looks a dialog line
        /// </summary>
        /// <param name="Str">The String</param>
        /// <param name="Trim">Internal Paramter, don't change it.</param>
        /// <returns>If looks a dialog, return true, else return false.</returns>
        static bool IsDialog(string Str, bool Trim = false) {
            string String = TrimString(Str);

            if (SpecialLineBreaker)
                String = String.Replace(GameLineBreaker, "\n");

            bool Status = !string.IsNullOrWhiteSpace(String);
            Status &= !string.IsNullOrWhiteSpace(TrimString(String).Trim('.', '?', '!'));
            int Process = 0;
            string Minified = SimplfyMatch(String);
            while (Status) {
                switch (Process) {
                    default:
                        goto ExitWhile;
                    case 0:
                        Status = !ContainsOR(Minified, "@,§,$,_,<,>,/");
                        break;
                    case 1:
                        Status = NumberLimiter(String, String.Length / 4);
                        break;
                    case 2:
                        Status = Minified.Length >= 3 || EndsWithOr(String, ".,!,?") || String.StartsWith(".");
                        break;
                    case 3:
                        Status = String.Contains(((char)32).ToString()) || EndsWithOr(String, ".\",!\",?\",.,!,?") || String.StartsWith(".");
                        if (!Status) {
                            if (Ranges != null) {
                                Status = true;
                                uint Miss = 0;
                                foreach (char c in Minified) {
                                    if (!InRange(c))
                                        Miss++;
                                }
                                if (Miss >= Minified.Length - 3)
                                    Status = true;
                            }
                        }
                        break;
                    case 4:
                        if (String.Length > 3) {
                            if (String[String.Length - 4] == '.' && !String.Substring(String.Length - 3, 3).Contains(".")) {
                                Status = false;
                            }
                        }
                        break;
                    case 5:
                        Status = ContainsOR(String.ToLower(), "a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,x,w,y,z");
                        break;
                }
                Process++;
            }
            ExitWhile:
            ;

            if (!Status && !Trim) {
                return IsDialog(String, true);
            }
            return Status;
        }

        /// <summary>
        /// Try get the encoding by name/id
        /// </summary>
        /// <param name="Name">Name/Id of the encoding</param>
        /// <returns>Result Encoding, Thrown if fails.</returns>
        static Encoding ParseEncodingName(string Name) {
            switch (Name.ToLower().Trim()) {
                case "default":
                    return Encoding.Default;

                case "shift-jis":
                case "sjis":
                    return Encoding.GetEncoding(932);

                case "utf8":
                    return Encoding.UTF8;

                case "unicode":
                case "utf16le":
                case "utf16":
                case "leutf16":
                    return Encoding.Unicode;

                case "beutf16":
                case "utf16be":
                    return Encoding.BigEndianUnicode;

                default:
                    int ID = 0;
                    if (int.TryParse(Name, out ID)) {
                        return Encoding.GetEncoding(ID);
                    }
                    return Encoding.GetEncoding(Name);
            }
        }

        /// <summary>
        /// Massive EndsWith Operation
        /// </summary>
        /// <param name="text">The String</param>
        /// <param name="v">The list of strings to match</param>
        /// <returns></returns>
        static bool EndsWithOr(string text, string v) {
            string[] letters = v.Split(',');
            foreach (string letter in letters)
                if (text.EndsWith(letter))
                    return true;
            return false;
        }

        /// <summary>
        /// Return true if the string contains a certain too many numbers
        /// </summary>
        /// <param name="text">The String</param>
        /// <param name="val">The Number Limit</param>
        /// <returns></returns>
        static bool NumberLimiter(string text, int val) {
            int min = '0', max = '9', total = 0;
            int asmin = '０', asmax = '９';
            foreach (char chr in text)
                if (chr >= min && chr <= max)
                    total++;
                else if (chr >= asmin && chr <= asmax)
                    total++;
            return total < val;
        }

        /// <summary>
        /// Massive Contains Operation
        /// </summary>
        /// <param name="text">The Text</param>
        /// <param name="MASK">List of substrings to match, in this format: Str1,Str2,Str3</param>
        /// <returns></returns>
        static bool ContainsOR(string text, string MASK) {
            string[] entries = MASK.Split(',');
            foreach (string entry in entries)
                if (text.Contains(entry))
                    return true;
            return false;
        }
    }
}

