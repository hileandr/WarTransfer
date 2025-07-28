using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;
using WarTransfer;

namespace Tests
{
    [TestClass]
    public class VersioningTests
    {
        public const string SimpleVersionNumberRegex = "\\d+\\.\\d+(?:\\.\\d+)*";
        public const string VersionNumberRegexWithLetter = "\\d+\\.?\\d+[a-zA-Z]?";

        [TestMethod]
        public void NoVersionTest()
        {
            VersionUtility.SetVersionNumberPattern(SimpleVersionNumberRegex);

            string simpleMapNameNoVersion = "test.w3x";
            string version = VersionUtility.ParseVersion(simpleMapNameNoVersion, true);

            Assert.IsTrue(version == "");

            string simpleStringNoVersion = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua";
            version = VersionUtility.ParseVersion(simpleStringNoVersion, false);

            Assert.IsTrue(version == "");
        }

        [TestMethod]
        public void SimpleVersionTest()
        {
            VersionUtility.SetVersionNumberPattern(SimpleVersionNumberRegex);

            string simpleMapName = "test1.08.w3x";
            string version = VersionUtility.ParseVersion(simpleMapName, true);

            Assert.IsTrue(version == "1.08");

            string simpleString = "Lorem ipsum dolor sit amet, consectetur 1.08 adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua";
            version = VersionUtility.ParseVersion(simpleString, false);

            Assert.IsTrue(version == "1.08");
        }

        [TestMethod]
        public void VersionInMiddleTest()
        {
            VersionUtility.SetVersionNumberPattern(SimpleVersionNumberRegex);

            string simpleMapName = "test1.09_suffix.w3x";
            string version = VersionUtility.ParseVersion(simpleMapName, true);

            Assert.IsTrue(version == "1.09");

            string simpleString = "Lorem ipsum dolor sit amet, consectetur 1.07_hello adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua";
            version = VersionUtility.ParseVersion(simpleString, false);

            Assert.IsTrue(version == "1.07");
        }

        [TestMethod]
        public void SimpleVersionReplacementTest()
        {
            string simpleMapName = "test1.08.w3x";
            string simpleMapNameOldVers = "anotherTest0.9.w3x";
            VersionUtility.UpdateVersion(simpleMapName, SimpleVersionNumberRegex);

            string updatedMapName = VersionUtility.ChangeFileNameVersion(simpleMapNameOldVers);

            Assert.IsTrue(updatedMapName == "anotherTest1.08.w3x");

            simpleMapName = "test1.09_somethingElse.w3x";
            simpleMapNameOldVers = "anotherTest1.05_someText.w3x";
            VersionUtility.UpdateVersion(simpleMapName, SimpleVersionNumberRegex);

            updatedMapName = VersionUtility.ChangeFileNameVersion(simpleMapNameOldVers);

            Assert.IsTrue(updatedMapName == "anotherTest1.09_someText.w3x");
        }

        [TestMethod]
        public void SimpleVersionReplacementTest2()
        {
            string simpleMapName = "BF1.06b_Blightwalk.w3x";
            string simpleMapNameOldVers = "BF1.06a_DesertStorm.w3x";
            VersionUtility.UpdateVersion(simpleMapName, VersionNumberRegexWithLetter);

            string updatedMapName = VersionUtility.ChangeFileNameVersion(simpleMapNameOldVers);

            Assert.IsTrue(updatedMapName == "BF1.06b_DesertStorm.w3x");
        }
    }
}
