using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/* Functionality defined: 
 *
 *      // Divides a file into segments and writes each segment to the file system at the given output path
 *      FileDivider.SplitFile("inputFilePath", 100, Unit.MB, "outputFilePath")
 *
 *      // Joins file segments and writes each segment to the file system at the given output path
 *      FileDivider.JoinFile("inputFile.001", "outputFile")
 *
 * author: scstauf@gmail.com */

namespace DividerLib {
    public class FileDivider {
        #region Members
        public enum Unit {
            Null = 0,
            B = 1,
            Kb = 125,
            KB = 1000,
            Mb = KB * Kb,
            MB = KB * KB,
            Gb = MB * Kb,
            GB = MB * KB
            //  Tb = GB * Kb
            //  TB = GB * KB
        }

        public enum Operation {
            Unknown = 0,
            Split = 1,
            Join = 2
        }

        private bool _noStdOut;
        #endregion

        #region Constructors
        public FileDivider() {
            _noStdOut = false;
        }

        public FileDivider(bool noStdOut) {
            _noStdOut = noStdOut;
        }
        #endregion

        #region Private instance methods
        /// <summary>Try to delete a file</summary>
        /// <param name="fileName">File to delete</param>
        /// <returns>Returns true if the file was deleted successfully, false otherwise</returns>
        private bool DeleteFile(string fileName) {
            try {
                File.Delete(fileName);
            } catch (Exception e) {
                LogError(e, false, "Could not delete {0}\n", fileName);
            }
            return !File.Exists(fileName);
        }

        /// <summary>Ensure a file exists</summary>
        /// <param name="fileName">File name to create</param>
        /// <returns>Returns true if the file was created successfully</returns>
        private bool EnsureFileExists(string fileName) {
            try {
                if (!File.Exists(fileName)) {
                    using (var fs = File.Create(fileName)) { fs.Close(); }
                }
            } catch (Exception e) {
                LogError(e, false, "Could not create {0}", fileName);
            }
            return File.Exists(fileName);
        }

        /// <summary>Returns a list of relevant files according to the target, excluding the output file</summary>
        /// <param name="input">The input file</param>
        /// <param name="output">The output file</param>
        /// <param name="target">The target file</param>
        /// <returns>List of relevant files according to the target, excluding the output file</returns>
        private IEnumerable<string> GetRelevantFiles(string input, string output, string target) {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(input))) {
                // Skip the output file
                if (file.EndsWith(output)) continue;
                // Only use the files that are 
                if (file.Contains(target)) {
                    yield return file;
                }
            }
        }

        /// <summary>Returns a formatted segmentation string</summary>
        /// <param name="segCount">The current segmentation</param>
        /// <returns>A string from "001" to "999"</returns>
        private string GetSegmentationString(int segCount) {
            return String.Format("{0,3}", segCount).Replace(' ', '0');
        }

        /// <summary>Log an error</summary>
        /// <param name="e">An exception object to extract useful information for future bug fixes or insertion into bug database</param>
        /// <param name="rethrow">Provides the option to rethrow the passed exception</param>
        /// <param name="messageFormat">A formatted string containing the message</param>
        /// <param name="args">An array of objects to be interpolated in the message</param>
        public void LogError(Exception e, bool rethrow, string messageFormat = "", params object[] args) {
            if (!_noStdOut) {
                try {
                    Console.Error.WriteLine(messageFormat, args);
                } catch {
                    Console.Error.WriteLine(e.Message);
                }
            }
            if (rethrow) throw e;
        }

        /// <summary>Write to the console</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        private void Log(string format, params object[] args) {
            if (_noStdOut) return;
            Console.WriteLine(format, args);
        }

        /// <summary>Write data from one stream to another.</summary>
        /// <param name="outStream">Output stream</param>
        /// <param name="inStream">Input stream</param>
        private void WriteBuffer(Stream outStream, Stream inStream) {
            var buffer = new byte[inStream.Length];
            int count;
            while ((count = inStream.Read(buffer, 0, buffer.Length)) > 0) {
                outStream.Write(buffer, 0, count);
            }
        }

        /// <summary>Writes a segmentation file</summary>
        /// <param name="output">Output file name</param>
        /// <param name="segCount">Reference to current segmentation count</param>
        /// <param name="seg">Reference to segmentation buffer</param>
        private void WriteSegmentation(string output, ref int segCount, ref byte[] seg) {
            var segFile = String.Format("{0}.{1}", output, GetSegmentationString(segCount));
            File.WriteAllBytes(segFile, seg);
            Log("{0,-25}{1}, size: {2} bytes", segFile, output, seg.Length);
            segCount++;
        }
        #endregion

        #region Public static methods
        /// <summary>Convert a string to units of memory if possible</summary>
        /// <param name="input">Input string</param>
        /// <returns>Converted unit. If no match found, returns Unit.Null</returns>
        public static Unit GetUnit(string input) {
            var unit = Unit.Null;
            foreach (var e in Enum.GetNames(typeof(Unit))) {
                if (e.Equals(input)) {
                    unit = (Unit)Enum.Parse(typeof(Unit), input, false);
                    break;
                }
            }
            return unit;
        }

        /// <summary>Parse operation from string.</summary>
        /// <param name="arg">String containing operation</param>
        /// <returns>Default value is Operation.Unknown</returns>
        public static Operation ParseOperation(string arg) {
            var op = Operation.Unknown;
            if (arg.Length > 1) {
                switch (arg[1]) {
                    case 's':
                        op = Operation.Split;
                        break;
                    case 'j':
                        op = Operation.Join;
                        break;
                    default:
                        return Operation.Unknown;
                }
            }
            return op;
        }
        #endregion

        #region Public instance methods
        /// <summary>Joins segment files.</summary>
        /// <param name="input">The first segment file.</param>
        public void JoinFile(string input, string output = "") {
            //  Only handle files that end in this format: *.001
            if (!input.EndsWith("001"))
                throw new Exception(String.Format("{0,-25}{1}", "Invalid input file:", input));
            else {
                //  file.ext.001, file.ext, \path\to\file.ext
                var segFile = Path.GetFileName(input);
                var target = Path.GetFileNameWithoutExtension(input);
                bool errored = false;
                output = Path.GetDirectoryName(input) + "\\" + (output.Length == 0 ? target : output);

                //  Proceed only if the output file does not already exist
                if (File.Exists(output))
                    throw new Exception(String.Format("{0,-25}{1}", "File already exists:", output));
                else {
                    //  Try to create the file
                    if (!EnsureFileExists(output)) 
                        throw new Exception(String.Format("Could not create: ", output));

                    Log("{0,-25}{1}\n{2,-25}{3}\n", "Join:", input, "Output file:", output);

                    //  Open a filestream to output file
                    using (var writer = new FileStream(output, FileMode.Append, FileAccess.Write)) {
                        //  Find the segment files
                        foreach (var file in GetRelevantFiles(input, output, target)) {
                            //  Open filestream to input file
                            try {
                                using (var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read)) {
                                    //  Write the data to the output stream 
                                    WriteBuffer(writer, inputStream);
                                    Log("Joining {0}, current size: {1} bytes", file, writer.Length);
                                }
                            } catch (Exception e) {
                                LogError(e, false, "Fatal error occurred, reverting...");
                                errored = true;
                                break;
                            }
                        }
                        //  Flush out final bytes and close the stream
                        if (!errored) writer.Flush();
                        writer.Close();
                    }
                    //  If an error occurred, try to revert
                    if (errored) DeleteFile(output);
                }
            }
        }

        /// <summary>Splits a file.</summary>
        /// <param name="size">An integer representing size</param>
        /// <param name="unit">The unit of size to use during segmentation</param>
        /// <param name="input">Path to input file</param>
        /// <param name="output">Path to output file</param>
        public void SplitFile(int size, Unit unit, string input, string output = "") {
            int segSize = (int)unit * size, //  Segmentation size
                current = 0,                //  A limit sentinel to control segmentation.
                segCount = 0,               //  The number of segmentations
                fileSize = (int)new FileInfo(input).Length;

            if (fileSize < segSize)
                throw new Exception("Segmentation size is larger than input file.");

            var seg = new byte[segSize];    //  Segmentation buffer
            var files = new List<string>(); //  List of files created for reversion
            bool errored = false;

            //  set the output file
            output = !String.IsNullOrEmpty(output) ? output : input;

            try {
                //  "Segmented:\t{0,15}"
                Log("{0,-25}{1}\n" + 
                    "{2,-25}{3}\n" + 
                    "{4,-25}{5}\n", 
                    "Split:", input, 
                    "Output file:", output,
                    "Segment size:", String.Format("{0}{1}", size, unit.ToString()));

                //  Iterate the bytes in the input file
                foreach (byte byt in File.ReadAllBytes(input)) {
                    //  If the segmentation limit is reached
                    if (current == segSize) {
                        fileSize -= segSize;    //  Subtract from original size
                        //  Write the segmentation
                        WriteSegmentation(output, ref segCount, ref seg);
                        //  Reset the buffer
                        Array.Clear(seg, 0, seg.Length);
                        Array.Resize(ref seg, fileSize < segSize ? fileSize : segSize);
                        current = 0;            //  Reset segmentation
                    }
                    //  If the segmentation limit has not been reached yet
                    if (current < segSize) {
                        //  Add the current byte to the segmentation buffer
                        seg[current] = byt;
                        current++;
                    }
                }
                //  Write final bytes
                WriteSegmentation(output, ref segCount, ref seg);
            } catch (Exception e) {
                LogError(e, false, "Fatal error occurred, reverting...");
                errored = true;
            }

            //  If an error occurred, try to revert 
            if (errored) {
                foreach (var file in files) {
                    DeleteFile(file);
                }
            }
        }
        #endregion
    }
}
