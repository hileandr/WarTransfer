using War3CodeUtility.Types;
using WarFiles;

// TO DO
//string wtgFileName = "Files\\war3map.wtg";
//string wtcFileName = "Files\\war3map.wct";
//string triggerDataFile = "Files\\triggerdata.txt";
//string outputFolder = "";

//TriggerData data = new TriggerData();

//TriggerData triggerData = new TriggerData();
//using (FileStream stream = new FileStream(triggerDataFile, FileMode.Open, FileAccess.Read))
//{
//    using (StreamReader reader = new StreamReader(stream))
//    {
//        triggerData.Deserialize(reader);
//    }
//}

//TriggerFile triggers = new TriggerFile();
//using (FileStream stream = new FileStream(wtgFileName, FileMode.Open, FileAccess.Read))
//{
//    using (BinaryReader reader = new BinaryReader(stream))
//    {
//        triggers.Deserialize(reader, triggerData);
//    }
//}

//ScriptFile scriptFile = new ScriptFile();
//using (FileStream stream = new FileStream(wtcFileName, FileMode.Open, FileAccess.Read))
//{
//    using (BinaryReader reader = new BinaryReader(stream))
//    {
//        scriptFile.Deserialize(reader, triggers);
//    }
//}

//TriggerHierarchy hierarchy = TriggerHierarchy.FromFile(triggers, scriptFile);

//hierarchy.WriteToFileSystem(outputFolder);