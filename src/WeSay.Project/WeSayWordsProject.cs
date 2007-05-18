using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Reporting;
using WeSay.Foundation;
using WeSay.Language;
using WeSay.LexicalModel;

namespace WeSay.Project
{
	public class WeSayWordsProject : BasilProject
	{
		//private string _lexiconDatabaseFileName = null;
		private IList<ITask> _tasks;
		private ViewTemplate _defaultViewTemplate;
		private IList<ViewTemplate> _viewTemplates;
		private Dictionary<string, OptionsList> _optionLists;
		private string _pathToLiftFile;
		private string _cacheLocationOverride;
		private FileStream _liftFileStreamForLocking;
		private LiftUpdateService _liftUpdateService;

		public WeSayWordsProject()
		{
			_optionLists = new Dictionary<string, OptionsList>();
		}

		public IList<ITask> Tasks
		{
			get
			{
				return this._tasks;
			}
			set
			{
				this._tasks = value;
			}
		}

		public static new WeSayWordsProject Project
		{
			get
			{
				if (Singleton == null)
				{
				  throw new InvalidOperationException("WeSayWordsProject Not initialized. For tests, call BasilProject.InitializeForTests().");
				}
				return (WeSayWordsProject) Singleton;
			}
		}


		/// <summary>
		/// See comment on BasilProject.InitializeForTests()
		/// </summary>
		public static new void InitializeForTests()
		{
			WeSayWordsProject project = new WeSayWordsProject();
			string s = Path.Combine(GetPretendProjectDirectory(),"wesay");
			s = Path.Combine(s,"pretend.lift");

			//jdh added, amidst some confusion about why it was suddenly needed, on april 17,2007
			LiftIO.Utilities.CreateEmptyLiftFile(s, "InitializeForTests()", true);

			project.SetupProjectDirForTests(s);
		}

		public void SetupProjectDirForTests(string pathToLift)
		{
			_projectDirectoryPath = Directory.GetParent(pathToLift).Parent.FullName;
			PathToLiftFile = pathToLift;
//            if (!Directory.Exists(pathToLift))
//            {
//                Directory.CreateDirectory(pathToLift);
//            }
			if (File.Exists(PathToConfigFile))
			{
				File.Delete(PathToConfigFile);
			}
			string configName = Path.GetFileName(Project.PathToConfigFile);
			File.Copy(Path.Combine(ApplicationTestDirectory, configName), Project.PathToConfigFile, true);

			ErrorReporter.OkToInteractWithUser = false;
			LoadFromProjectDirectoryPath(_projectDirectoryPath);
			StringCatalogSelector = "en";
		}


		public bool LoadFromLiftLexiconPath(string liftPath)
		{
			try
			{
				PathToLiftFile = liftPath;

				if (!File.Exists(liftPath))
				{
				  ErrorReporter.ReportNonFatalMessage(
					  String.Format(
						  "WeSay tried to find the lexicon at '{0}', but could not find it.\r\n\r\nTry opening the LIFT file by double clicking on it.",
						  liftPath));
				  return false;
				}
				try
				{
				  using (FileStream fs = File.OpenWrite(liftPath))
				  {
					fs.Close();
				  }
				}
				catch (UnauthorizedAccessException)
				{
				  ErrorReporter.ReportNonFatalMessage(
					  String.Format(
						  "WeSay was unable to open the file at '{0}' for writing, because the system won't allow it. Check that 'ReadOnly' is cleared, otherwise investigate your user permissions to write to this file.",
								  liftPath));
				  return false;
				}
				catch (IOException)
				{
				  ErrorReporter.ReportNonFatalMessage(
					  String.Format(
						  "WeSay was unable to open the file at '{0}' for writing, probably because it is locked by some other process on your computer (maybe a recently crashed run of WeSay?). If you can't figure out what has it locked, restart your computer.",
						  liftPath));
				  return false;
				}

				if (!File.Exists(PathToConfigFile))
				{
				  ErrorReporter.ReportNonFatalMessage(
					  String.Format(
						  "WeSay tried to find the WeSay configuration file at '{0}', but could not find it.\r\n\r\nTry using the configuration Tool to create one.",
						  PathToConfigFile));
				  return false;
				}
				try
				{
				  using (FileStream fs = File.OpenRead(PathToConfigFile))
				  {
					fs.Close();
				  }
				}
				catch (UnauthorizedAccessException)
				{
				  ErrorReporter.ReportNonFatalMessage(
					  String.Format(
						  "WeSay was unable to open the file at '{0}' for reading, because the system won't allow it. Investigate your user permissions to write to this file.",
								  PathToConfigFile));
				  return false;
				}
				catch (IOException e)
				{
				  ErrorReporter.ReportNonFatalMessage(
					  String.Format(
						  "WeSay was unable to open the file at '{0}' for reading. \n Further information: {1}",
						  PathToConfigFile, e.Message));
				  return false;
				}


				//walk up from file to /wesay to /<project>
				_projectDirectoryPath = Directory.GetParent(Directory.GetParent(liftPath).FullName).FullName;

				if (CheckLexiconIsInValidProjectDirectory(liftPath))
				{
					this._projectDirectoryPath = ProjectDirectoryPath;

					/*if (GetCacheIsOutOfDate(liftPath))
					{
						throw new ApplicationException(
							"Possible programming error. The cache should be up-to-date before calling this method.");
					}
					*/
					base.LoadFromProjectDirectoryPath(
						ProjectDirectoryPath);
					return true;
				}
				else
				{
					PathToLiftFile = null;
					_projectDirectoryPath = null;
					return false;
				}
			}
			catch (Exception e)
			{
				ErrorReporter.ReportNonFatalMessage(e.Message);
				return false;
			}
		}

//        public void LoadFromConfigFilePath(string path)
//        {
//            DirectoryInfo weSayDirectoryInfo = Directory.GetParent(path);
//
//        }

		private IList<ViewTemplate> InitializeViewTemplatesFromProjectFiles()
		{
			if (_viewTemplates == null)
			{
				List<ViewTemplate> viewTemplates = new List<ViewTemplate>();
				ViewTemplate fullUpToDateTemplate = ViewTemplate.MakeMasterTemplate(WritingSystems);

				try
				{
					XmlDocument projectDoc = GetProjectDoc();
					if (projectDoc != null)
					{
						XmlNodeList nodes = projectDoc.SelectNodes("tasks/components/viewTemplate");
						foreach (XmlNode node in nodes)
						{
							ViewTemplate template = new ViewTemplate();
							template.LoadFromString(node.OuterXml);
							ViewTemplate.SynchronizeInventories(fullUpToDateTemplate, template);
							if (template.Id == "Default View Template")
							{
								_defaultViewTemplate = template;
							}
							viewTemplates.Add(template);
						}
					}
				}
				catch (Exception error)
				{
					MessageBox.Show(
							"There may have been a problem reading the field template xml. A default template will be created." +
							error.Message);
				}
				if(_defaultViewTemplate == null)
				{
					_defaultViewTemplate = fullUpToDateTemplate;
				}
				_viewTemplates = viewTemplates;

			}
			return _viewTemplates;
		}


		private XmlDocument GetProjectDoc()
		{
			XmlDocument projectDoc = null;
			if (File.Exists(PathToConfigFile))
			{
				try
				{
					projectDoc = new XmlDocument();
					projectDoc.Load(Project.PathToConfigFile);
				}
				catch (Exception e)
				{
					ErrorReporter.ReportNonFatalMessage("There was a problem reading the task xml. " + e.Message);
					projectDoc = null;
				}
			}
			return projectDoc;
		}


		static private bool CheckLexiconIsInValidProjectDirectory(string liftPath)
		{
			DirectoryInfo lexiconDirectoryInfo = Directory.GetParent(liftPath);
			DirectoryInfo projectRootDirectoryInfo = lexiconDirectoryInfo.Parent;
			string lexiconDirectoryName = lexiconDirectoryInfo.Name;
			if (Environment.OSVersion.Platform != PlatformID.Unix)
			{
				//windows
				lexiconDirectoryName = lexiconDirectoryName.ToLowerInvariant();
			}

			if (projectRootDirectoryInfo == null ||
				lexiconDirectoryName != "wesay" ||
				(!IsValidProjectDirectory(projectRootDirectoryInfo.FullName)))
			{
				string message = "WeSay cannot open the lexicon, because it is not in a proper WeSay/Basil project structure.";
				ErrorReporter.ReportNonFatalMessage(message);
				return false;
			}
			return true;
		}

		public override void CreateEmptyProjectFiles(string projectDirectoryPath)
		{
			base.CreateEmptyProjectFiles(projectDirectoryPath);
			Directory.CreateDirectory(PathToWeSaySpecificFilesDirectoryInProject);
			_defaultViewTemplate = ViewTemplate.MakeMasterTemplate(WritingSystems);
			_viewTemplates = new List<ViewTemplate>();
			_viewTemplates.Add(_defaultViewTemplate);
		   // this._lexiconDatabaseFileName = this.Name+".words";


			if (!File.Exists(PathToLiftFile))
			{
				LiftIO.Utilities.CreateEmptyLiftFile(PathToLiftFile, LiftExporter.ProducerString, false);
			}

		}

		public static bool IsValidProjectDirectory(string dir)
		{
			string[] requiredDirectories = new string[] { "common", "wesay" };
			foreach (string s in requiredDirectories)
			{
				if (!Directory.Exists(Path.Combine(dir, s)))
					return false;
			}
			return true;
		}

		public string PathToConfigFile
		{
			get
			{
				string name = Path.GetFileNameWithoutExtension(PathToLiftFile);
				return Path.Combine(PathToWeSaySpecificFilesDirectoryInProject, name + ".WeSayConfig");
			}
		}

		/// <summary>
		/// used for upgrading
		/// </summary>
		public string PathToOldProjectTaskInventory
		{
			get
			{
				return Path.Combine(PathToWeSaySpecificFilesDirectoryInProject, "tasks.xml");
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			if (LiftIsLocked)
			{
				ReleaseLockOnLift();
			}
		}

		/// <remark>
		/// The protection provided by this simple opproach is obviously limitted;
		/// it will keep the lift file safe normally... but could lead to non-data-loosing crashes
		/// if some automated process was sitting out there, just waiting to open as soon as we realease
		/// </summary>
		public void ReleaseLockOnLift()
		{
			Debug.Assert(_liftFileStreamForLocking != null);
			_liftFileStreamForLocking.Close();
			_liftFileStreamForLocking.Dispose();
			_liftFileStreamForLocking = null;
		}

		public bool LiftIsLocked
		{
			get
			{
				return _liftFileStreamForLocking != null;
			}
		}

		public void LockLift()
		{
			Debug.Assert(_liftFileStreamForLocking == null);
			 _liftFileStreamForLocking = File.OpenRead(PathToLiftFile);
		}

		public string PathToLiftFile
		{
			get
			{
				if (String.IsNullOrEmpty(_pathToLiftFile))
				{
					_pathToLiftFile =
						Path.Combine(PathToWeSaySpecificFilesDirectoryInProject, Path.GetFileName(ProjectDirectoryPath ) + ".lift");
				}
				return _pathToLiftFile;
			}

			set
			{
				_pathToLiftFile = value;
				if (value == null)
				{
					_projectDirectoryPath = null;
				}
				else
				{
					_projectDirectoryPath = Directory.GetParent(value).Parent.FullName;
				}
			}
		}

		public string PathToLiftBackupDir
		{
			get
			{
				return PathToLiftFile + " incremental xml backup";
			}
		}

		public string PathToCache
		{
			get
			{
				if (_cacheLocationOverride != null)
				{
				   return _cacheLocationOverride;
				}
				else
				{
					 return GetPathToCacheFromPathToLift(PathToLiftFile);
				}
			}
		}

		private static string GetPathToCacheFromPathToLift(string pathToLift)
		{
			return Path.Combine(Path.GetDirectoryName(pathToLift), "Cache");
		}

		public string PathToDb4oLexicalModelDB
		{
			get
			{
				return GetPathToDb4oLexicalModelDBFromPathToLift(PathToLiftFile);
			}
		}

		public string GetPathToDb4oLexicalModelDBFromPathToLift(string pathToLift)
		{
				return Path.Combine(GetPathToCacheFromPathToLift(pathToLift), Path.GetFileNameWithoutExtension(pathToLift) + ".words");
		}




		public string PathToWeSaySpecificFilesDirectoryInProject
		{
			get
			{
				return Path.Combine(ProjectDirectoryPath, "wesay");
			}
		}

		public ViewTemplate DefaultViewTemplate
		{
			get
			{
				if(_defaultViewTemplate==null)
				{
					InitializeViewTemplatesFromProjectFiles();
				}
				return _defaultViewTemplate;
			}
		}

		public IList<ViewTemplate> ViewTemplates
		{
			get
			{
				if (_viewTemplates == null)
				{
					InitializeViewTemplatesFromProjectFiles();
				}
				return _viewTemplates;
			}
		}

		public IEnumerable OptionFieldNames
		{
			get
			{
				List<string> names = new List<string>();

				foreach (Field field in DefaultViewTemplate.Fields)
				{
					if(field.DataTypeName=="Option")
					{
						names.Add(field.FieldName);
					}
				}
				return names;
			}
		}

		public IEnumerable OptionCollectionFieldNames
		{
			get
			{
				List<string> names = new List<string>();

				foreach (Field field in DefaultViewTemplate.Fields)
				{
					if (field.DataTypeName == "OptionCollection")
					{
						names.Add(field.FieldName);
					}
				}
				return names;
			}
		}

		//used when building the cache, so we can build it in a temp directory
		public string CacheLocationOverride
		{
			set
			{
				_cacheLocationOverride = value;
			}
		}

		public LiftUpdateService LiftUpdateService
		{
			get
			{
				return _liftUpdateService;
			}
			set
			{
				_liftUpdateService = value;
			}
		}


		public OptionsList GetOptionsList(Field field)
		{
			if (String.IsNullOrEmpty(field.OptionsListFile))
			{
				throw new Reporting.ConfigurationException("The administrator needs to declare an options list file for the field {0}. This can be done under the Fields tab of the WeSay Configuration Tool.", field.FieldName);
			}
			OptionsList list;
			if(_optionLists.TryGetValue(field.OptionsListFile, out list))
			{
				return list;
			}

			string pathInProject = Path.Combine(PathToWeSaySpecificFilesDirectoryInProject, field.OptionsListFile);
			if (File.Exists(pathInProject))
			{
				LoadOptionsList(pathInProject);
			}
			else
			{
				string pathInProgramDir = Path.Combine(ApplicationCommonDirectory, field.OptionsListFile);
				if (!File.Exists(pathInProgramDir))
				{
					throw new Reporting.ConfigurationException("Could not find the optionsList file {0}. Expected to find it at: {1} or {2}", field.OptionsListFile, pathInProject, pathInProgramDir);
				}
				LoadOptionsList(pathInProgramDir);
			}

			return _optionLists[field.OptionsListFile];
	   }

		private void LoadOptionsList(string pathToOptionsList)
		{
			string name = Path.GetFileName(pathToOptionsList);
			OptionsList list = new OptionsList();
			list.LoadFromFile(pathToOptionsList);
			_optionLists.Add(name, list);
		}

		/// <summary>
		/// Used with xml export, e.g. with LIFT to set the proper "range" for option fields
		/// </summary>
		/// <returns></returns>
		public  Dictionary<string, string> GetFieldToOptionListNameDictionary()
		{
			Dictionary<string, string> fieldToOptionListName = new Dictionary<string, string>();
			foreach (Field field in DefaultViewTemplate.Fields)
			{
				if (field.OptionsListFile != null && field.OptionsListFile.Trim() != "")
				{
					fieldToOptionListName.Add(field.FieldName, GetListNameFromFileName(field.OptionsListFile));
				}
			}
			return fieldToOptionListName;
		}

		static private string GetListNameFromFileName(string file)
		{
			return file.Substring(0, file.IndexOf(".xml"));
		}

		public void MakeFieldNameChange(Field field, string oldName)
		{
			//NB: we're just using regex, here, not xpaths which in this case
			//would be nice (e.g., "name" is a pretty generic thing to be changing)
		   if (File.Exists(PathToLiftFile))
			{
				//traits
			   if(field.DataTypeName == Field.BuiltInDataType.Option.ToString()
				   || field.DataTypeName == Field.BuiltInDataType.OptionCollection.ToString())
			   {
				   GrepLift(PathToLiftFile, string.Format("name\\s*=\\s*[\"']{0}[\"']", oldName),
							string.Format("name=\"{0}\"", field.FieldName));
			   }
			   else
				{
					//<field>s
					GrepLift(PathToLiftFile, string.Format("tag\\s*=\\s*[\"']{0}[\"']", oldName),
							 string.Format("tag=\"{0}\"", field.FieldName));
				}
			}
		}

		public void MakeWritingSystemIdChange(WritingSystem ws, string oldId)
		{
			//Todo: WS-227 Before changing a ws id in a lift file, ensure that it isn't already in use

			WritingSystems.IdOfWritingSystemChanged(ws, oldId);

			foreach (Field field in DefaultViewTemplate)
			{
				field.ChangeWritingSystemId(oldId, ws.Id);
			}

			if (File.Exists(PathToLiftFile))
			{
				//todo: expand the regular expression here to account for all reasonable patterns
				GrepLift(PathToLiftFile, string.Format("lang\\s*=\\s*[\"']{0}[\"']", oldId), string.Format("lang=\"{0}\"", ws.Id));
			}
		}

		static private void GrepLift(string inputPath, string pattern, string replaceWith)
		{
			Regex regex = new Regex(pattern, RegexOptions.Compiled);
			string tempPath = inputPath + ".tmp";

			using (StreamReader reader = File.OpenText(inputPath))
			{
				using (StreamWriter writer = new StreamWriter(tempPath))
				{
					while (!reader.EndOfStream)
					{
						writer.WriteLine(regex.Replace(reader.ReadLine(), replaceWith));
					}
					writer.Close();
				}
				reader.Close();
			}
		   string backupPath = GetUniqueFileName(inputPath);
		   File.Replace(tempPath, inputPath, backupPath);
		}

		public bool LiftHasMatchingElement(string element, string attribute, string attributeValue)
		{
			using (XmlReader reader = XmlReader.Create(PathToLiftFile))
			{
				while (reader.ReadToFollowing(element))
				{
					string v = reader.GetAttribute(attribute);
					if (!String.IsNullOrEmpty(v) && v == attributeValue)
					{
						return true; //found it
					}
				}
			}
			return false;
		}

		private static string GetUniqueFileName(string path)
		{
			int i = 1;
			while (File.Exists(path + "old" + i))
			{
				++i;
			}
			return path + "old" + i;
		}


		/// <summary>
		/// Files to process when backing up or checking in
		/// </summary>
		/// <param name="pathToProjectRoot"></param>
		/// <returns></returns>
		public static string[] GetFilesBelongingToProject(string pathToProjectRoot)
		{
			List<String> files = new List<string>();
			string wesay = Path.Combine(pathToProjectRoot, "wesay");
			string[] allFiles =Directory.GetFiles(pathToProjectRoot, "*", SearchOption.AllDirectories);
			string[] antipatterns = {"Cache", "cache", ".bak", ".old"};

			foreach (string file in allFiles)
			{
			   if(!Matches(file, antipatterns))
				{
					files.Add(file);
				}
			}
			return files.ToArray();
		}

		private static bool Matches(string file, string[] antipatterns)
		{
			foreach (string s in antipatterns)
			{
				if (file.Contains(s))
					return true;
			}
			return false;
		}
	}
}
