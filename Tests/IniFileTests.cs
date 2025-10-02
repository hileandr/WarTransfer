using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using WarFiles;

namespace Tests
{
    [TestClass]
    public class IniFileTests
    {
        [TestMethod]
        public void ReadFile()
        {
            string fileName = "../../TestFiles/TestIni.txt";

            IniFile file = new IniFile();

            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(stream, true))
                {
                    file.Deserialize(reader);
                }
            }

            Assert.IsTrue(file.Entries.Count == 5);
            Assert.IsTrue(file.Sections.Count == 2);
        }
    }
}
