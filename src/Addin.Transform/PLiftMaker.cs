using System.Diagnostics;
using System.IO;
using System.Reflection;
using Palaso.Data;
using WeSay.LexicalModel;
using WeSay.Project;

namespace Addin.Transform
{
	public class PLiftMaker
	{
		//private string MakePLiftTempFile(IEnumerable<LexEntry> entries, ViewTemplate template, IFindEntries finder)
		//{
		//    string path = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
		//    WeSayLiftWriter exporter = new WeSayLiftWriter(path);
		//    exporter.SetUpForPresentationLiftExport(template, finder);
		//    foreach (LexEntry entry in entries)
		//    {
		//        exporter.Add(entry);
		//    }
		//    exporter.End();
		//    return path;
		//}

		public void  MakePLiftTempFile(string outputPath, LexEntryRepository lexEntryRepository, ViewTemplate template)
		{
			PLiftExporter exporter = new PLiftExporter(outputPath, lexEntryRepository, template);
			ResultSet<LexEntry> recordTokens =
					lexEntryRepository.GetAllEntriesSortedByHeadword(template.HeadwordWritingSystem);
			foreach (RecordToken<LexEntry> token in recordTokens)
			{
				int homographNumber = 0;
				if ((bool) token["HasHomograph"])
				{
					homographNumber = (int) token["HomographNumber"];
				}
				exporter.Add(token.RealObject, homographNumber);
			}

			exporter.End();
		}

		public static Stream GetXsltStream(string xsltName)
		{
			Stream stream =
					Assembly.GetExecutingAssembly().GetManifestResourceStream("Addin.Publish." +
																			  xsltName);
			Debug.Assert(stream != null);
			return stream;
		}
	}
}