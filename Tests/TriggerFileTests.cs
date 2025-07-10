using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text.RegularExpressions;
using WarFiles;
using WarTransfer;

namespace Tests
{
    [TestClass]
    public class TriggerFileTests
    {
        const string WTGFileName = "../../TestFiles/test.wtg";
        const string WTCFileName = "../../TestFiles/testX.wct";
        const string TriggerDataFile = "../../TestFiles/triggerdata.txt";

        [TestMethod]
        public void TriggerDataTest()
        {
            TriggerData triggerData = new TriggerData();
            using (FileStream stream = new FileStream(TriggerDataFile, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    triggerData.Deserialize(reader);
                }
            }

            Assert.IsTrue(triggerData.EventDefs.Count == 38);
            Assert.IsTrue(triggerData.ConditionDefs.Count == 32);
            Assert.IsTrue(triggerData.ActionDefs.Count == 677);
            Assert.IsTrue(triggerData.CallDefs.Count == 571);
        }

        [TestMethod]
        public void ReadWTGFile()
        {
            TriggerData triggerData = new TriggerData();
            using (FileStream stream = new FileStream(TriggerDataFile, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    triggerData.Deserialize(reader);
                }
            }

            TriggerFile triggers = new TriggerFile();
            using (FileStream stream = new FileStream(WTGFileName, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    triggers.Deserialize(reader, triggerData);
                }
            }

            Assert.IsTrue(triggers.Categories.Count == 66);
            Assert.IsTrue(triggers.DeletedCategoryIds.Length == 0);
            Assert.IsTrue(triggers.DeletedCommentIds.Length == 32);
            Assert.IsTrue(triggers.DeletedLibraryIds.Length == 0);
            Assert.IsTrue(triggers.DeletedMapIds.Length == 0);
            Assert.IsTrue(triggers.DeletedScriptIds.Length == 1);
            Assert.IsTrue(triggers.DeletedTriggerIds.Length == 630);
            Assert.IsTrue(triggers.DeletedVariableIds.Length == 279);
            Assert.IsTrue(triggers.FormatVersion == 7);
            Assert.IsTrue(triggers.SubVersion == 2);
            Assert.IsTrue(triggers.Triggers.Count == 270);
            Assert.IsTrue(triggers.Variables.Count == 3);
        }

        [TestMethod]
        public void ReadWctFile()
        {
            TriggerData triggerData = new TriggerData();
            using (FileStream stream = new FileStream(TriggerDataFile, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    triggerData.Deserialize(reader);
                }
            }

            TriggerFile triggers = new TriggerFile();
            using (FileStream stream = new FileStream(WTGFileName, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    triggers.Deserialize(reader, triggerData);
                }
            }

            ScriptFile scriptFile = new ScriptFile();
            using (FileStream stream = new FileStream(WTCFileName, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    scriptFile.Deserialize(reader, triggers);
                }
            }
        }
    }
}