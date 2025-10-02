using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WarTransfer.FileTypes;

namespace WarTransfer
{
    public static class W3iUtility
    {
        private const int REF_COUNT = 4;
        private const int STARTING_BYTE_INDEX = 28;

        public static W3iFile ReadFile(IWorkflowHandler e, string filePath)
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

            W3iFile file = null;
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open))
                {
                    using (BinaryReader reader = new BinaryReader(stream, utf8))
                    {
                        file = W3iFile.Read(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                e.ReportError($"An error occured while reading file '{filePath}': {ex.Message}", true);
            }

            return file;
        }

        public static void WriteFile(IWorkflowHandler e, string filePath, W3iFile file)
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(stream, utf8))
                    {
                        file.Serialize(writer);
                    }
                }
            }
            catch (Exception ex)
            {
                e.ReportError($"An error occured while writing file '{filePath}': {ex.Message}", false);
            }
        }
            

        public static string[] GetMapInfoRefs(IWorkflowHandler e, string w3iFile)
        {
            byte[] fileContents = LoadFile(e,w3iFile);

            string[] refs = new string[REF_COUNT];

            if (!e.KillProcess)
            {
                if (fileContents.Length > 0)
                {
                    int byteIndex = STARTING_BYTE_INDEX;
                    for (int i = 0; i < REF_COUNT; i++)
                    {
                        refs[i] = StringUtility.ReadNullTerminatedString(fileContents, byteIndex);
                        byteIndex += refs[i].Length;
                    }
                }
            }

            return refs;
        }

        public static byte[] ReplaceRefs(IWorkflowHandler e, string w3iFile, string[] sourceRefs, string[] targetRefs)
        {
            byte[] sourceFile = LoadFile(e, w3iFile);

            if (!e.KillProcess)
            {
                var outputFileStart = sourceFile.Take(STARTING_BYTE_INDEX);
                List<byte> changedBytes = new List<byte>();

                if (sourceFile.Length > 0)
                {
                    int sourceByteIndex = STARTING_BYTE_INDEX;
                    int targetByteIndex = STARTING_BYTE_INDEX;
                    for (int i = 0; i < REF_COUNT; i++)
                    {
                        string sourceRef = StringUtility.ReadNullTerminatedString(sourceFile, sourceByteIndex);
                        if (sourceRef == sourceRefs[i])
                        {
                            changedBytes.AddRange(Encoding.UTF8.GetBytes(targetRefs[i]));
                            sourceByteIndex += sourceRefs[i].Length;
                            targetByteIndex += sourceRefs[i].Length;
                        }
                        else
                        {
                            e.ReportError(w3iFile + " file reference mismatch!", true);
                        }
                    }

                    return outputFileStart.Concat(changedBytes).Concat(sourceFile.Skip(targetByteIndex)).ToArray();
                }
            }

            return new byte[0];
        }

        private static byte[] LoadFile(IWorkflowHandler e, string filePath)
        {
            byte[] fileContents = null;
            try
            {
                fileContents = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                e.ReportError(ex, true);
            }

            return fileContents;
        }
    }
}
