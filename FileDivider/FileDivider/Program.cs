using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DividerLib;

namespace DividerUtil {
    class Program {
        const string helpMessage = "div by scottyeatscode.net\n\nSyntax:\tdiv -[s|j]=[<int_size><Kb|KB|Mb|MB|Gb|GB>] [input_file] [output_file]";

        static void Main(string[] args) {
            //args = new string[] { "-s=5000KB", @"C:\testing\div\test.mkv", @"C:\testing\div\out.mkv"};
            //args = new string[] { "-j", @"C:\testing\div\out.mkv.000" };
            var outputMessage = "";
            try {
                if (args.Length < 2)
                    outputMessage = helpMessage;
                else {
                    if (args[0][0] != '-' && args[0][0] != '/')
                        outputMessage = helpMessage;
                    else {
                        var op = FileDivider.ParseOperation(args[0]);
                        var output = args.Length > 2 ? args[2] : "";
                        int size = 10;

                        //  Do some input validation
                        if (op == FileDivider.Operation.Unknown) outputMessage = String.Format("Unknown operation: {0}", args[0]);
                        if (!File.Exists(args[1])) outputMessage = String.Format("{0}\n{1} does not exist", outputMessage, args[1]);
                        if (File.Exists(output)) outputMessage = String.Format("{0}\n{1} already exists", outputMessage, output);
                        if (!String.IsNullOrEmpty(outputMessage)) return;

                        //  Attempt to change default segment size.
                        //  The parameter should contain an equal sign followed by an integer representing the segment size in bytes.
                        var unit = FileDivider.Unit.MB;
                        if (args[0].Contains('=')) {
                            //  Tokenize to get the size value
                            var tokens = args[0].Split(new char[] { '=' });
                            if (tokens.Length > 1) {
                                if (Int32.TryParse(tokens[1], out size)) {
                                    //  The size is set, representing segment size in megabytes (default)
                                } else {
                                    //  Get the size: 50MB = MB50 = 5M0B = 50 megabytes;
                                    string alphabetic = "", numeric = "";
                                    foreach (char c in tokens[1]) {
                                        if (Char.IsDigit(c)) numeric += c;
                                        else alphabetic += c;
                                    }
                                    // Try to set memory unit
                                    unit = FileDivider.GetUnit(alphabetic);
                                    // Try to set the size
                                    if (Int32.TryParse(numeric, out size)) {}
                                }
                            }
                        }

                        var div = new FileDivider();
                        switch (op) {
                            case FileDivider.Operation.Split:
                                div.SplitFile(size, unit, Path.GetFullPath(args[1]), output);
                                break;
                            case FileDivider.Operation.Join:
                                div.JoinFile(Path.GetFullPath(args[1]), output);
                                break;
                        }
                    }
                }
            } catch (Exception e) {
                Console.Error.WriteLine(e.Message);
            } finally {
                Console.WriteLine(outputMessage);
            }
        }
    }
}
