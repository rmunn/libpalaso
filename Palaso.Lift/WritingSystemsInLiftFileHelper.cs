﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Palaso.IO;
using Palaso.Reporting;
using Palaso.WritingSystems;
using Palaso.Xml;

namespace Palaso.Lift
{
	public class WritingSystemsInLiftFileHelper
	{
		private readonly string _liftFilePath;
		private readonly IWritingSystemRepository _writingSystemRepository;

		public WritingSystemsInLiftFileHelper(IWritingSystemRepository writingSystemRepository, string liftFilePath)
		{
			_writingSystemRepository = writingSystemRepository;
			_liftFilePath = liftFilePath;
		}

		public IEnumerable<string> WritingSystemsInUse
		{
			get
			{
				var uniqueIds = new List<string>();
				using (var reader = XmlReader.Create(_liftFilePath))
				{
					while (reader.Read())
					{
						if (reader.MoveToAttribute("lang"))
						{
							if (!uniqueIds.Contains(reader.Value))
							{
								uniqueIds.Add(reader.Value);
							}
						}
					}
				}
				return uniqueIds;
			}
		}

		public void DeleteWritingSystemId(string id)
		{
			var fileToBeWrittenTo = new IO.TempFile();
			var reader = XmlReader.Create(_liftFilePath, Xml.CanonicalXmlSettings.CreateXmlReaderSettings());
			var writer = XmlWriter.Create(fileToBeWrittenTo.Path, Xml.CanonicalXmlSettings.CreateXmlWriterSettings());
			//System.Diagnostics.Process.Start(fileToBeWrittenTo.Path);
			try
			{
				bool readerMovedByXmlDocument = false;
				while (readerMovedByXmlDocument || reader.Read())
				{
					readerMovedByXmlDocument = false;
					var xmldoc = new XmlDocument();
					if (reader.NodeType == XmlNodeType.Element && reader.Name == "entry")
					{
						var entryFragment = xmldoc.ReadNode(reader);
						readerMovedByXmlDocument = true;
						var nodesWithLangId = entryFragment.SelectNodes(String.Format("//*[@lang='{0}']", id));
						if (nodesWithLangId != null)
						{
							foreach (XmlNode node in nodesWithLangId)
							{
								var parent = node.SelectSingleNode("parent::*");
								if (node.Name == "gloss")
								{
									parent.RemoveChild(node);
								}
								else
								{
									var siblingNodes =
										node.SelectNodes("following-sibling::form | preceding-sibling::form");
									if (siblingNodes.Count == 0)
									{
										var grandParent = parent.SelectSingleNode("parent::*");
										grandParent.RemoveChild(parent);
									}
									else
									{
										parent.RemoveChild(node);
									}
								}
							}
						}
						entryFragment.WriteTo(writer);
					}
					else
					{
						writer.WriteNodeShallow(reader);
					}
					//writer.Flush();
				}
			}
			finally
			{
				reader.Close();
				writer.Close();
			}
			File.Delete(_liftFilePath);
			fileToBeWrittenTo.MoveTo(_liftFilePath);
		}

		public void ReplaceWritingSystemId(string oldId, string newId)
		{
			var fileToBeWrittenTo = new IO.TempFile();
			var reader = XmlReader.Create(_liftFilePath, Xml.CanonicalXmlSettings.CreateXmlReaderSettings());
			var writer = XmlWriter.Create(fileToBeWrittenTo.Path, Xml.CanonicalXmlSettings.CreateXmlWriterSettings());
			//System.Diagnostics.Process.Start(fileToBeWrittenTo.Path);
			try
			{
				bool readerMovedByXmlDocument = false;
				while (readerMovedByXmlDocument || reader.Read())
				{
					readerMovedByXmlDocument = false;
					var xmldoc = new XmlDocument();
					if (reader.NodeType == XmlNodeType.Element && reader.Name == "entry")
					{
						var entryFragment = xmldoc.ReadNode(reader);
						readerMovedByXmlDocument = true;
						var nodesWithLangId = entryFragment.SelectNodes(String.Format("//*[@lang='{0}']", oldId));
						if (nodesWithLangId != null)
						{
							foreach (XmlNode node in nodesWithLangId)
							{
								node.Attributes["lang"].Value = newId;
								var xPathForSiblingsWithIdenticalLangAndContent =
									String.Format(
										"following-sibling::{0}[@lang='{1}' and ./text/text() = '{2}'] | preceding-sibling::{0}[@lang='{1}' and ./text/text() = '{2}']",
										node.Name, node.Attributes["lang"].Value, node.SelectSingleNode("./text/text()").Value);
								var siblingNodesWithNewId = node.SelectNodes(xPathForSiblingsWithIdenticalLangAndContent).Cast<XmlNode>();
								foreach (var identicalNode in siblingNodesWithNewId)
								{
									var parent = identicalNode.SelectSingleNode("parent::*");
									parent.RemoveChild(identicalNode);
								}
							}
						}
						entryFragment.WriteTo(writer);
					}
					else
					{
						writer.WriteNodeShallow(reader);
					}
					//writer.Flush();
				}
			}
			finally
			{
				reader.Close();
				writer.Close();
			}
			File.Delete(_liftFilePath);
			fileToBeWrittenTo.MoveTo(_liftFilePath);
		}

		public void CreateNonExistentWritingSystemsFoundInFile()
		{
			WritingSystemOrphanFinder.FindOrphans(WritingSystemsInUse, ReplaceWritingSystemId, _writingSystemRepository);
		}
	}
}