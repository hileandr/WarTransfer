using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text.RegularExpressions;
using WarTransfer;
using WarTransfer.FileTypes;

namespace Tests
{
    [TestClass]
    public class FileTests
    {
        public const string SimpleVersionNumberRegex = "\\d+\\.\\d+(?:\\.\\d+)*";
        public const string VersionNumberRegexWithLetter = "\\d+\\.?\\d+[a-zA-Z]?";

        [TestMethod]
        public void W3iReadTest_Format31()
        {
            string testFile = "../../TestFiles/test31.w3i";

            try
            {
                using (FileStream stream = new FileStream(testFile, FileMode.Open))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        W3iFile file = W3iFile.Read(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        [TestMethod]
        public void W3iReadTest_Format33()
        {
            string testFile = "../../TestFiles/test33.w3i";

            try
            {
                using (FileStream stream = new FileStream(testFile, FileMode.Open))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        W3iFile file = W3iFile.Read(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }
    }
}
