﻿using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VstsSyncMigrator.Engine;

namespace VstsSyncMigrator.Core.Execution.OMatics
{
    public class AttachmentOMatic
    {
        private WorkItemServer _server;
        private string _exportBasePath;
        private string _exportWiPath;

        public AttachmentOMatic(WorkItemServer workItemServer, string exportBasePath)
        {
            _server = workItemServer;
            _exportBasePath = exportBasePath;
        }

        public void ProcessAttachemnts(WorkItem sourceWorkItem, WorkItem targetWorkItem, bool save = true)
        {
            _exportWiPath = Path.Combine(_exportBasePath, sourceWorkItem.Id.ToString());
            if (System.IO.Directory.Exists(_exportWiPath))
            {
                System.IO.Directory.Delete(_exportWiPath, true);
            }
            System.IO.Directory.CreateDirectory(_exportWiPath);
            foreach (Attachment wia in sourceWorkItem.Attachments)
            {
                try
                {
                    string filepath = null;
                    filepath = ExportAttachment(sourceWorkItem, wia, _exportWiPath);
                    if (filepath != null)
                    {
                        ImportAttachemnt(targetWorkItem, filepath, save);
                    }
                    Trace.WriteLine("...done");
                }
                catch (Exception)
                {
                    Trace.WriteLine(string.Format(" ERROR: Unable to process atachment from source wi {0} called {1}", sourceWorkItem.Id, wia.Name));
                }

            }
            if (save)
            {
                targetWorkItem.Fields["System.ChangedBy"].Value = "Migration";
                targetWorkItem.Save();
                CleanUpAfterSave(targetWorkItem);
            }           

        }

        public void CleanUpAfterSave(WorkItem targetWorkItem)
        {
            if (_exportWiPath != null && System.IO.Directory.Exists(_exportWiPath))
            {
                try
                {
                    System.IO.Directory.Delete(_exportWiPath, true);
                    _exportWiPath = null;
                }
                catch (Exception)
                {
                    Trace.WriteLine(string.Format(" ERROR: Unable to delete folder {0}", targetWorkItem.Id));
                }
            }            
        }

        private string ExportAttachment(WorkItem wi, Attachment wia, string exportpath)
        {
            string fname = GetSafeFilename(wia.Name);
            Trace.Write("-");
            Trace.Write(fname);

            string fpath = Path.Combine(exportpath, fname);
            if (!File.Exists(fpath))
            {
                Trace.Write(string.Format("...downloading {0} to {1}", fname, exportpath));
                try
                {
                    var fileLocation = _server.DownloadFile(wia.Id);
                    File.Copy(fileLocation, fpath, true);
                    Trace.Write("...done");
                }
                catch (Exception ex)
                {
                    Telemetry.Current.TrackException(ex);
                    Trace.Write($"\r\nException downloading attachements {ex.Message}");
                    return null;
                }

            }
            else
            {
                Trace.Write("...already downloaded");
            }
            return fpath;
        }

        private void ImportAttachemnt(WorkItem targetWorkItem, string filepath, bool save = true)
        {
            var filename = System.IO.Path.GetFileName(filepath);
            var attachments = targetWorkItem.Attachments.Cast<Attachment>();
            var attachment = attachments.Where(a => a.Name == filename).FirstOrDefault();
            if (attachment == null)
            {
                Attachment a = new Attachment(filepath);
                targetWorkItem.Attachments.Add(a);
            }
            else
            {
                Trace.WriteLine(string.Format(" [SKIP] WorkItem {0} already contains attachment {1}", targetWorkItem.Id, filepath));
            }

        }

        public string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
