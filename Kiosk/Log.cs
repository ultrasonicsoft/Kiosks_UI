using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.IO;
using System.Data.SqlClient;
using System.IO.Compression;
using System.Reflection;


namespace Kiosk
{
    public struct LogTupple
    {
        public string fileName;
        //public StreamWriter log;
        public String msg;
        
    }
    // =============================================================================================
    // Class LogBook
    // =============================================================================================
    public class LogBook: IThreadable
    {
        private const string m_threadName = "Log";
        private const int LOGBUFFER = 10;

        private ConcurrentDictionary<string, StreamWriter> m_logStreamwriters;
        private ConcurrentQueue<LogTupple> m_logQueue;

        //loop control
        private bool m_exit;
        private TimeSpan m_logInterval;//seconds
        private DateTime m_lastLogged;
        private DateTime m_lastClean;
        private DateTime m_lastArchive;
        
        //data from config
        private TimeSpan m_logLifeTime;//days to keep archives
        private TimeSpan m_logArchiveIntv;//days between archival sessions
        private int m_maxFileSize;//bytes
        private string m_logPath;

        public LogBook()
        {
            m_logStreamwriters = new ConcurrentDictionary<string, StreamWriter>();
            m_logQueue = new ConcurrentQueue<LogTupple>();

            m_exit = false;
            m_logInterval = new TimeSpan(0, 0, 1);//1sec
            m_lastClean = DateTime.MinValue;
            m_lastArchive = DateTime.MinValue;
            m_lastLogged = DateTime.Now; //don't try and log on startup cycle

            m_logArchiveIntv = new TimeSpan(Params.getParam("logarchiveintv", m_threadName, 1),0,0,0);
            m_logLifeTime = new TimeSpan(Params.getParam("loglifespan", m_threadName, 30),0,0,0);
            m_maxFileSize = Params.getParam("maxlogfilesize", m_threadName, 50 * 1024 * 1024);
            m_logPath = LogTools.safeDirectoryName(Params.getParam("logdirectory", "kiosk", @"c:\temp\log"));

        }

        #region IThreadable Members

        public void threadLoop()
        { 
            try
            {
                Thread.CurrentThread.Name = m_threadName;

//                App.AppEventLog.WriteEntry("LogBook Thread is starting");

                while (!m_exit)
                {
                    //write pending messages from the queue
                    if (m_logQueue.Count > LOGBUFFER || (DateTime.Now - m_lastLogged) >= m_logInterval)
                    {
                        List<string> toBeFlushed = new List<string>();
                        //grab current state of Queue (let successive enqueues pile up until next cycle)
                        int count = m_logQueue.Count;
                        for (int i = 0; i < count; i++)
                        {
                            LogTupple toBeLogged;
                            bool success = m_logQueue.TryDequeue(out toBeLogged);
                            if (!success)
                            {
                                continue;//skip for now
                            }
                            if (!toBeFlushed.Contains(toBeLogged.fileName))
                            {
                                toBeFlushed.Add(toBeLogged.fileName);
                            }
                            if (m_logStreamwriters.ContainsKey(toBeLogged.fileName))
                            {
                                if(!String.IsNullOrEmpty(toBeLogged.msg))
                                {m_logStreamwriters[toBeLogged.fileName].WriteLine(toBeLogged.msg);}
                            }
                            else
                            {
                                App.AppEventLog.WriteEntry(LogTools.getStatusString(m_threadName, "threadloop", "Filename = " + toBeLogged.fileName));
                            }
                        }
                        foreach (string fn in toBeFlushed)
                        {
                            m_logStreamwriters[fn].Flush();
                        }
                        m_lastLogged = DateTime.Now;
                    }

                    //handle archival and deletion of old logs
                    if ((DateTime.Now - m_lastArchive) >= m_logArchiveIntv)
                    {
                        List<string> toBeArchived = new List<string>();
                        foreach (KeyValuePair<string, StreamWriter> kvp in m_logStreamwriters)
                        {
                            if (kvp.Value.BaseStream is FileStream)
                            {
                                FileInfo file = new FileInfo(m_logPath + kvp.Key);
                                if (file.Length > m_maxFileSize)
                                {
                                    //close the stream so we can manipulate file
                                    kvp.Value.Close();
                                    toBeArchived.Add(m_logPath + kvp.Key);
                                }
                            }
                        }

                        //cannot remove from inside the m_logStreamwriters foreach
                        foreach(string fileName in toBeArchived)
                        {
                            StreamWriter sw;
                            m_logStreamwriters.TryRemove(Path.GetFileName(fileName), out sw);
                            processArchive(fileName);
                            //recreate logfile
                            FileStream fs = new FileStream(fileName, FileMode.Append);
                            m_logStreamwriters.TryAdd(Path.GetFileName(fileName), new StreamWriter(fs));
                        }
                        m_lastArchive = DateTime.Now;

                        cleanupLogs();
                    }
                        
                    Thread.Sleep(10);
                }
            }
            catch (ThreadAbortException ex)
            {
                App.AppEventLog.WriteEntry(LogTools.getExceptionString(m_threadName,"threadloop",ex,"LogBook Thread is stopping"));
                //todo: rethrow? retry?
                throw ex;
            }
            finally
            {
                foreach (KeyValuePair<string, StreamWriter> kvp in m_logStreamwriters)
                {
                    kvp.Value.Close();
                }
            }

        }

        // ---------------------------------------------------------------------
        // Flag indicating the thread should be stopped.
        // ---------------------------------------------------------------------
        public void terminate()
        {
            m_exit = true;
        }

        #endregion

        /// <summary>
        /// Archives a log file by copying its contents to a compressed archive and
        /// deleting old file.
        /// </summary>
        /// <param name="fileName">the name of the file to compress</param>
        private void processArchive(String fileName)
        {
            //compress file
            try
            {
                String outFileName = fileName.Substring(0, fileName.Length - 4);  //remove ".log" from end of name
                outFileName += "-" + DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss") + ".log.gz";

                FileStream inFile = File.OpenRead(fileName);
                FileStream outFile = File.Create(outFileName);
                GZipStream compress = new GZipStream(outFile, CompressionMode.Compress);

                App.AppEventLog.WriteEntry("File is being archived: " + outFileName);

                //copy contents
                inFile.CopyTo(compress);
                compress.Close();
                inFile.Close();
                outFile.Close();
                File.Delete(fileName);
            }
            catch (Exception e)
            {
                App.AppEventLog.WriteEntry(LogTools.getExceptionString(m_threadName, "processArchive", e, "Could not find filename " + fileName + ""));
                throw;
            }
        }

        /// <summary>
        /// Removes all expired archives from the disk
        /// </summary>
        private void cleanupLogs()
        {
            DateTime expiration = DateTime.Now - m_logLifeTime;
            DirectoryInfo dir = new DirectoryInfo(m_logPath);
            FileInfo[] files = dir.GetFiles("*.gz", SearchOption.TopDirectoryOnly);

            foreach(FileInfo file in files)
            {
                if (file.CreationTime <= expiration)
                {
                    App.AppEventLog.WriteEntry("File is being deleted: " + file.FullName);
                    file.Delete();
                }
            }
        }


        // ---------------------------------------------------------------------
        // Adds item into a queue to be logged.
        // ---------------------------------------------------------------------
        public void log(LogTupple logThis)
        {
            m_logQueue.Enqueue(logThis);
        }


        // ---------------------------------------------------------------------
        // Creates a streamwriter that will write to the given file name.
        // ---------------------------------------------------------------------
        public LogClient requestLog(string filename)
        {
            StreamWriter sw;
            filename = LogTools.safeFileName(filename);
            string filePath = m_logPath + filename;

            try
            {
                if (m_logStreamwriters.ContainsKey(filename))
                {
                    sw = m_logStreamwriters[filename];
                }
                else
                {
                    FileStream fs = new FileStream(filePath, FileMode.Append);
                    sw = new StreamWriter(fs);
                    m_logStreamwriters.TryAdd(filename, sw);
                    

                    //check for dir and create if necessary
                    //todo
                }
            }
            catch (IOException e)
            {
                App.AppEventLog.WriteEntry(LogTools.getExceptionString(m_threadName, "requestLog", e));
                throw e;
            }
            LogClient lc = new LogClient(filename,this);

            return lc;
            //return sw;
        }

    }
    // =============================================================================================
    // Class LogClient
    // =============================================================================================
    public class LogClient
    {
        private string m_fileName;
        private LogBook m_logBook;

        public LogClient(string fileName, LogBook logBook)
        {
            m_fileName = fileName;
            m_logBook = logBook;
        }

        public void log(string msg, bool loggingEnabled = true)
        {
            if (loggingEnabled)
            {
                LogTupple l = new LogTupple();
                l.fileName = m_fileName;
                l.msg = msg;

                m_logBook.log(l);
            }
        }
    }

    public static class LogTools
    {
        /// <summary>
        /// Returns the formatted current time according to the dateTimeFormat
        /// </summary>
        /// <returns>
        /// the string time stamp for right now formatted appropriately
        /// </returns>
        public static string getNowTimestamp()
        {
            //return DateTime.Now.ToString(App.Current.Properties["DATEFORMAT"].ToString());
            return DateTime.Now.ToString();
        }

        /// <summary>
        /// Wraps lines to maxLength, breaking on characters in breakChars.
        /// </summary>
        /// <param name="input">the input string to be broken into lines</param>
        /// <param name="maxLength">the maximum line length</param>
        /// <returns>string wrapped to maxLength</returns>
        public static string wrapLines(string input, int maxLength)
        {
            List<string> msgLines = new List<string>();
            char[] breakChars = {' ','\t','\n','\r','.',';','-','_','(',')','{','}','[',']'};
            bool breakFound = false;

            if (input.Length <= maxLength)
            { return input; }

            //goto end of proposed line and search backwards for a break
            while (input.Length > maxLength)
            {
                breakFound = false;
                //search every position starting from end of line
                for (int n = maxLength; (n > 0 && !breakFound); n--)
                { 
                    //try every character in breakChars
                    for (int i = 0; (i < breakChars.Length && !breakFound); i++)
                    {
                        if (input[n].Equals(breakChars[i])||input.Length<=maxLength)
                        {
                            //pop line
                            msgLines.Add(input.Substring(0, n + 1));
                            input = input.Substring(n + 1);
                            //stop looking for a break
                            breakFound = true;
                        }
                    }
                    //if we didnt find a break spit out the line anyway
                    if (n == 1)
                    {
                        msgLines.Add(input.Substring(0, maxLength));
                        input = input.Substring(maxLength);
                    }
                }
            }
            //print lines
            string output = "";
            foreach(string s in msgLines)
            {   
                output += s + "\n";
            }
            return output;
        }

        /// <summary>
        /// Returns formatted log message for consistency in all the logging.
        /// </summary>
        /// <param name="className">the string class that made the msg</param>
        /// <param name="functionName">the string function name that made msg</param>
        /// <param name="errorMsg">the literal msg to log</param>
        /// <returns>Timestamp Class: className Function: functionName logMsg</returns>
        public static string getLogString(string className, string functionName, string logMsg)
        {
            return getNowTimestamp() + " Class: " + className + " Function: " + functionName + ": " +
                    logMsg; //logger will use writeln, do not append a newline
        }

        public static string getStatusString(string className, string functionName, string statusMsg)
        {
            return getLogString(className, functionName, "STATUS: " + statusMsg);
        }

        public static string getErrorString(string className, string functionName, string errorMsg)
        {
            return getLogString(className, functionName, "ERROR: " + errorMsg);
        }

        public static string getExceptionString(string className, string functionName, Exception e, string synopsis = "")
        {
            return getNowTimestamp() + ": " + synopsis + "\n" + e.ToString();
            //string msg = "An Exception Occurred: ";
            //if (!String.IsNullOrWhiteSpace(synopsis))
            //{
            //    msg += synopsis+": ";
            //}
            //msg += e.Message;

            //return getErrorString(className, functionName, "An Exception Occurred: "+synopsis+" "+e.Message);
        }

        public static string getDebugString(string className, string functionName, string debugMsg)
        {
            string s = "";

            if (App.DEBUG)
            { 
                s = getLogString(className, functionName, "DEBUG: " + debugMsg); 
            }

            return s;
        }

        public static string getCardString(string className, string functionName, string msg)
        {
            if (!String.IsNullOrEmpty(msg))
            {
                string alt_card_string = "alt_card_string";
                string new_CC = "new_CC";
                string dl_m_cardswipe = "alt_card_string";
                string monthlyCC = "monthlyCC";
                string use_dif_CC = "use_dif_CC";
                string card_string = "card_string";

                int indexAltCard = msg.IndexOf(alt_card_string);
                int indexNewCC = msg.IndexOf(new_CC);
                int indexDl = msg.IndexOf(dl_m_cardswipe);
                int indexMonthlyCC = msg.IndexOf(monthlyCC);
                int indexDifCC = msg.IndexOf(use_dif_CC);
                int indexCard_String = msg.IndexOf(card_string);
                if (indexCard_String > 0)
                {
                    msg = msg.Substring(0, indexCard_String + card_string.Length + 1) + "<card string removed>";
                }
                else if (indexAltCard > 0)
                {
                    msg = msg.Substring(0, indexAltCard + alt_card_string.Length + 1) + "<card string removed>";
                }
                else if (indexNewCC > 0)
                {
                    msg = msg.Substring(0, indexNewCC + new_CC.Length + 1) + "<card string removed>";
                }
                else if (indexDl > 0)
                {
                    msg = msg.Substring(0, indexDl + dl_m_cardswipe.Length + 1) + "<card string removed>";
                }
                else if (indexMonthlyCC > 0)
                {
                    msg = msg.Substring(0, indexMonthlyCC + monthlyCC.Length + 1) + "<card string removed>";
                }
                else if (indexDifCC > 0)
                {
                    msg = msg.Substring(0, indexDifCC + use_dif_CC.Length + 1) + "<card string removed>";
                }
                else
                {
                    //continue
                }
            }
            return getStatusString(className, functionName, msg );
        }

        public static string getTelegramString(string direction, string msg, int queueLength)
        {
            return getNowTimestamp() + "  " + direction + " <" + msg + "><" + queueLength.ToString() + ">"; 
        }

        public static string safeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) 
            { fileName = fileName.Replace(c, '_'); }

            return fileName;
        }

        public static string safeDirectoryName(string dirName)
        {
            //App.AppEventLog.WriteEntry("Debug: sanitizing " + dirName);
            //look for single slash, convert to double
            //todo
            //look for unix /, convert to win \
            //todo?
            //look for win \, convert to unix /
            //todo?

            //ensure that directory ends in path delimiter
            if (!dirName.EndsWith("" + Path.DirectorySeparatorChar)) 
            { dirName += Path.DirectorySeparatorChar; }

            //look for other illegal chars
            foreach (char c in Path.GetInvalidPathChars())
            { dirName = dirName.Replace(c, '_'); }

            //check for directory / try and create
            try
            {
                if (!Directory.Exists(dirName))
                { Directory.CreateDirectory(dirName); }
            }
            catch (IOException e)
            {
                App.AppEventLog.WriteEntry(LogTools.getExceptionString("Log","safeDirectoryName", e));
            }

            return dirName;
        }

    }
}
