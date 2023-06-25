using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace SRVModTool.Test
{
    [TestClass]
    public class CampaignTest
    {
        [TestMethod]
        public void LocalizationFileTest()
        {
            var original = File.ReadAllText("test_original.int");

            var c = new CampaignConfiguration("test.int");
            c.Load();
            c.Save();

            var saved = File.ReadAllText("test.int");

            Assert.AreEqual(original.Trim(), saved.Trim());
        }

        [TestMethod]
        public void ExportImportTest()
        {
            var c = new Campaign()
            {
                Name = "Test Campaign",
                Description = "Once upon a time...",
                BaseLevel = "test.lvl",
                Prefix = "TEST",
                GameType = "TestGame.TestGame"
            };

            var export = c.ToString();

            var import = new Campaign(export);

            Assert.AreEqual(c.Name, import.Name);
            Assert.AreEqual(c.Description, import.Description);
            Assert.AreEqual(c.BaseLevel, import.BaseLevel);
            Assert.AreEqual(c.Prefix, import.Prefix);
            Assert.AreEqual(c.GameType, import.GameType);

        }
    }
}
