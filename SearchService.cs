using EnvDTE;
using Lucene.Net.Analysis;
using Lucene.Net.Documents.Extensions;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Flexible.Core.Config;
using Lucene.Net.QueryParsers.Flexible.Standard;
using Lucene.Net.QueryParsers.Flexible.Standard.Config;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HandyTools
{
    public struct SearchQuery
    {
        public string text_;
        public bool caseSensitive_;
        public SearchWindowControl.SearchMethod method_;
    }

    public struct SearchResult
    {
        public int hits_;
        public int totalHits_;
        public long totalTime_;
    }

    public interface ISearchService
    {
        System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken);
        System.Threading.Tasks.Task IndexingAsync();
        System.Threading.Tasks.Task UpdateAsync();
        System.Threading.Tasks.Task UpdateAsync(EnvDTE.ProjectItem projectItem);
        System.Threading.Tasks.Task<SearchResult?> SearchAsync(SearchWindowControl control, SearchQuery searchQuery);
        void ClearPathCache();
    }

    public interface SSearchService
    {
    }

    public class SearchService : SSearchService, ISearchService
    {
        public const Lucene.Net.Util.LuceneVersion AppLuceneVersion = Lucene.Net.Util.LuceneVersion.LUCENE_48;
        public const int MaxBufferDocuments = 8*1024;
        public const string FieldContent = "content";
        public const string FieldContentLow = "content_low";

        public static long UtcNow(DateTime time)
        {
            return (long)time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static long UtcNow()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public bool InIndexing
        {
            get {
                lock(lock_) {
                    return inIndexing_;
                }
            }

            set {
                lock(lock_) {
                    inIndexing_ = value;
                }
            }
        }

        public long LastIndexUpdated
        {
            get {
                lock(lock_) {
                    return lastIndexUpdated_;
                }
            }

            set {
                lock(lock_) {
                    lastIndexUpdated_ = value;
                }
            }
        }

        public SearchService(Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider)
        {
            serviceProvider_ = serviceProvider;
        }

        public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken)
        {
            await InitializeInternalAsync();
        }

        private async System.Threading.Tasks.Task<bool> InitializeInternalAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return false;
            }
            string solutionFileName = dte2.Solution.FileName;
            if(string.IsNullOrEmpty(solutionFileName)) {
                return false;
            }
            string solutionDirectory = System.IO.Path.GetDirectoryName(solutionFileName);
            string fileInfoPath = System.IO.Path.Combine(solutionDirectory, ".vs", "esinfo.db");
            string indexPath = System.IO.Path.Combine(solutionDirectory, ".vs", "esindex");

            if(!System.IO.Directory.Exists(indexPath)) {
                try {
                    System.IO.Directory.CreateDirectory(indexPath);
                } catch {
                    return false;
                }
            }

            try {
                fileInfoDb_ = new LiteDB.LiteDatabase(fileInfoPath);
                indexDirectory_ = FSDirectory.Open(indexPath);
                analyzer_ = new ExAnalyzer(AppLuceneVersion, false);
                lowerAnalyzer_ = new ExAnalyzer(AppLuceneVersion, true);
            } catch(Exception e) {
                await Log.OutputAsync(string.Format("HandyTools: Initialize {0}\n", e));
                return false;
            }
            return true;
        }

        private async Task<bool> CheckInitializeAsync()
        {
            if(null == indexDirectory_) {
                return await InitializeInternalAsync();
            }
            return true;
        }

        public class PathDate
        {
            public string Id { get; set; }
            public long Date { get; set; }
        };

        private async Task<int> IndexFilesAsync(List<Tuple<string, string>> items)
        {
            int indexFileCount = 0;
            IndexWriterConfig indexWriterConfig = new IndexWriterConfig(AppLuceneVersion, analyzer_);
            indexWriterConfig.OpenMode = OpenMode.CREATE_OR_APPEND;
            LiteDB.ILiteCollection<PathDate> pathdates = fileInfoDb_.GetCollection<PathDate>("pathdate");
            pathdates.EnsureIndex(x => x.Id, unique: true);
            using(IndexWriter indexWriter = new IndexWriter(indexDirectory_, indexWriterConfig))
            using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
#if DEBUG
                Stopwatch stopwatchFiltering = Stopwatch.StartNew();
#endif
                IndexSearcher indexSearcher = new IndexSearcher(indexReader);

                List<PathDate> updateQueries = new List<PathDate>(128);
                {//Deleting
                    List<Query> deleteQueries = new List<Query>(128);
                    for(int i = 0; i < items.Count;) {
                        if(!System.IO.File.Exists(items[i].Item2)) {
                            deleteQueries.Add(new TermQuery(new Term("path", items[i].Item1)));
                            pathdates.Delete(items[i].Item1);
                            items.RemoveAt(i);
                            continue;
                        }
                        try {
                            System.IO.FileInfo fileInfo = new System.IO.FileInfo(items[i].Item2);
                            long currentLastWriteTime = UtcNow(fileInfo.LastWriteTimeUtc);

                            try {
                                string path = items[i].Item1;
                                var result = pathdates.FindOne(x => x.Id == path);
                                if(null != result) {
                                    if(currentLastWriteTime<=result.Date) {
                                        items.RemoveAt(i);
                                        continue;
                                    }
                                }
                            } catch(Exception e) {
                                await Log.OutputAsync(string.Format("HandyTools: find {0}\n", e));
                            }
                            deleteQueries.Add(new TermQuery(new Term("path", items[i].Item1)));
                            updateQueries.Add(new PathDate { Id = items[i].Item1, Date = currentLastWriteTime });
                            ++i;
                        } catch {
                            items.RemoveAt(i);
                        }
                    } //for(int i = start;
                    if(0 < deleteQueries.Count) {
                        indexWriter.DeleteDocuments(deleteQueries.ToArray());
                        indexWriter.Commit();
                    }
                }
#if DEBUG
                stopwatchFiltering.Stop();
                await Log.OutputAsync(string.Format("HandyTools: filtering files {0} milliseconds\n", stopwatchFiltering.ElapsedMilliseconds));
#endif
                List<Lucene.Net.Documents.Document> documents = new List<Lucene.Net.Documents.Document>(1024);
                int indexLineCount = 0;
                for(int i = 0; i < items.Count; ++i) {
                    Tuple<string, string> file = items[i];
                    try {
                        int lineCount = 0;
                        {
                            foreach(string line in System.IO.File.ReadAllLines(file.Item2)) {
                                Lucene.Net.Documents.Document document = new Lucene.Net.Documents.Document();
                                Lucene.Net.Documents.StringField pathField = new Lucene.Net.Documents.StringField("path", file.Item1, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(pathField);
                                Lucene.Net.Documents.Field lineField = new Lucene.Net.Documents.Int32Field("line", lineCount, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(lineField);
                                Lucene.Net.Documents.Field content = new Lucene.Net.Documents.TextField(FieldContent, line, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(content);
                                Lucene.Net.Documents.Field content_low = new Lucene.Net.Documents.TextField(FieldContentLow, line.ToLower(), Lucene.Net.Documents.Field.Store.YES);
                                document.Add(content_low);
                                documents.Add(document);

                                ++lineCount;
                                ++indexLineCount;
                            }
                            if(MaxBufferDocuments<= ++indexLineCount) {
                                indexLineCount = 0;
                                indexWriter.AddDocuments(documents);
                                indexWriter.Commit();
                                documents.Clear();
                                await System.Threading.Tasks.Task.Yield();
                            }
                            ++indexFileCount;
                        }
                        //await Log.OutputAsync(string.Format("HandyTools: index {0} {1} {2}\n", file.Item1, file.Item2, lineCount));
                    } catch(Exception e) {
                        await Log.OutputAsync(string.Format("HandyTools: Indexing {0}\n", e));
                    }
                } //for(int i = start; i < end; ++i)
                try {
                    if(0 < documents.Count) {
                        indexWriter.AddDocuments(documents);
                        indexWriter.Commit();
                        documents.Clear();
                    }
                } catch {

                }
                if(0 < updateQueries.Count) {
                    foreach(PathDate pathDate in updateQueries) {
                        pathdates.Upsert(pathDate);
                    }
                }
            }
            return indexFileCount;
        }

        public async System.Threading.Tasks.Task IndexingAsync()
        {
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return;
            }
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if(null == dte2.Solution.Projects) {
                return;
            }

            HandyToolsPackage package = await serviceProvider_.GetServiceAsync(typeof(HandyToolsPackage)) as HandyToolsPackage;
            if(null == package) {
                return;
            }
            string solutionPath = dte2.Solution.FileName;
            if(string.IsNullOrEmpty(solutionPath)) {
                return;
            }
            OptionPageHandyTools dialog = package.Options;
            HashSet<string> extensionSet = dialog.ExtensionSet;
            bool debugLog = dialog.OutputDebugLog;
            Stopwatch stopwatch = Stopwatch.StartNew();

#if DEBUG
            Stopwatch stopwatchFileGather = Stopwatch.StartNew();
#endif
            List<Tuple<string, string>> items = new List<Tuple<string, string>>(1024);
            string path = System.IO.Path.GetFileNameWithoutExtension(solutionPath);
            foreach(EnvDTE.Project project in dte2.Solution.Projects) {
                string projectRoot = System.IO.Path.Combine(path, project.Name);
                IndexingTraverse(items, projectRoot, project.ProjectItems, extensionSet);
            }
#if DEBUG
            stopwatchFileGather.Stop();
            await Log.OutputAsync(string.Format("HandyTools: indexing gathering file {0} in {1} milliseconds\n", items.Count, stopwatchFileGather.ElapsedMilliseconds));
#endif
            int indexFileCount = await IndexFilesAsync(items);

            stopwatch.Stop();
            if(debugLog) {
                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                try {
                    using(IndexWriter indexWriter = new IndexWriter(indexDirectory_, new IndexWriterConfig(AppLuceneVersion, analyzer_)))
                    using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
                        IndexSearcher indexSearcher = new IndexSearcher(indexReader);
                        CollectionStatistics stats = indexSearcher.CollectionStatistics(FieldContent);
                        await Log.OutputAsync(string.Format("HandyTools: indexing {0}/{1} in {2} milliseconds, {3} docs in db\n", indexFileCount, items.Count, elapsedMilliseconds, stats.DocCount));
                    }
                } catch(Exception e) {
                    await Log.OutputAsync(string.Format("HandyTools: indexing {0}\n", e));
                }
            }
        }

        private void IndexingTraverse(List<Tuple<string, string>> items, string path, EnvDTE.ProjectItems projectItems, HashSet<string> extensionSet)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            foreach(EnvDTE.ProjectItem projectItem in projectItems) {
                switch(projectItem.Kind) {
                case EnvDTE.Constants.vsProjectItemKindPhysicalFile:
                    if(0 < projectItem.FileCount) {
                        string filepath = projectItem.FileNames[0];
                        string extension = System.IO.Path.GetExtension(filepath);
                        if(!string.IsNullOrEmpty(extension) && 0 < extension.Length && '.' == extension[0]) {
                            extension = extension.Substring(1, extension.Length - 1);
                        }
                        if(extensionSet.Contains(extension)) {
                            items.Add(new Tuple<string, string>(System.IO.Path.Combine(path, projectItem.Name), filepath));
                        }
                    }
                    break;
                case EnvDTE.Constants.vsProjectItemKindPhysicalFolder: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindVirtualFolder: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindSolutionItems: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.SubProject && null != projectItem.SubProject.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.SubProject.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindSubProject: {
                    string root = System.IO.Path.Combine(path, projectItem.Name);
                    if(null != projectItem.SubProject && null != projectItem.SubProject.ProjectItems) {
                        IndexingTraverse(items, root, projectItem.SubProject.ProjectItems, extensionSet);
                    }
                }
                break;
                case EnvDTE.Constants.vsProjectItemKindMisc:
                    break;
                }
            }
        }

        private void GetProjectPaths(HashSet<string> items)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return;
            }
            if(null == dte2.Solution.Projects) {
                return;
            }
            string path = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FileName);
            foreach(EnvDTE.Project project in dte2.Solution.Projects) {
                GetProjectPathsTraverse(items, path, project);
            }
        }

        private void GetProjectPathsTraverse(HashSet<string> items, string path, EnvDTE.Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            switch(project.Kind) {
            case ProjectTypes.ProjectFolders:
            case ProjectTypes.SolutionFolder:
                path = System.IO.Path.Combine(path, project.Name);
                break;
            default:
                path = System.IO.Path.Combine(path, project.Name);
                items.Add(path);
                return;
            }
            if(null != project.Collection) {
                foreach(EnvDTE.Project child in project.Collection) {
                    GetProjectPathsTraverse(items, path, child);
                }
            }

            if(null != project.ProjectItems) {
                foreach(EnvDTE.ProjectItem projectItem in project.ProjectItems) {
                    GetProjectPathsTraverse(items, path, projectItem);
                }
            }
        }

        private void GetProjectPathsTraverse(HashSet<string> items, string path, EnvDTE.ProjectItem projectItem)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            switch(projectItem.Kind) {
            case EnvDTE.Constants.vsProjectItemKindPhysicalFolder:
            case EnvDTE.Constants.vsProjectItemKindVirtualFolder:
                path = System.IO.Path.Combine(path, projectItem.Name);
                break;
            case EnvDTE.Constants.vsProjectItemKindSolutionItems:
                if(null != projectItem.ProjectItems) {
                    path = System.IO.Path.Combine(path, projectItem.Name);
                    items.Add(path);
                } else if(null != projectItem.Collection) {
                    path = System.IO.Path.Combine(path, projectItem.Name);
                    items.Add(path);
                } else {
                    return;
                }
                break;
            case EnvDTE.Constants.vsProjectItemKindSubProject:
                path = System.IO.Path.Combine(path, projectItem.Name);
                items.Add(path);
                break;
            default:
                break;
            }
            EnvDTE.ProjectItems projectItems = projectItem.ProjectItems;
            if(null == projectItems) {
                return;
            }
            projectItems = projectItem.Collection;
            if(null == projectItems) {
                return;
            }
            foreach(EnvDTE.ProjectItem child in projectItems) {
                GetProjectPathsTraverse(items, path, child);
            }
        }

        public async System.Threading.Tasks.Task UpdateAsync()
        {
            if(false == await CheckInitializeAsync()) {
                return;
            }
            HandyToolsPackage package = await serviceProvider_.GetServiceAsync(typeof(HandyToolsPackage)) as HandyToolsPackage;
            if(null == package) {
                return;
            }
            LiteDB.ILiteCollection<PathDate> pathdates = fileInfoDb_.GetCollection<PathDate>("pathdate");
            pathdates.EnsureIndex(x => x.Id, unique: true);
            OptionPageHandyTools dialog = package.Options;
            int updateMinInterval = dialog.UpdateMinInterval;
            long now = UtcNow();
            lock(lock_) {
                if(inIndexing_) {
                    return;
                }
                long lastUpdate = lastIndexUpdated_ + updateMinInterval;
                if(now < lastUpdate) {
                    return;
                }
                inIndexing_ = true;
            }
            await IndexingAsync();
            now = UtcNow();
            lock(lock_) {
                lastIndexUpdated_ = now;
                inIndexing_ = false;
            }
        }

        public async System.Threading.Tasks.Task UpdateAsync(EnvDTE.ProjectItem projectItem)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if(false == await CheckInitializeAsync()) {
                return;
            }

            HandyToolsPackage package = await serviceProvider_.GetServiceAsync(typeof(HandyToolsPackage)) as HandyToolsPackage;
            if(null == package) {
                return;
            }
            OptionPageHandyTools dialog = package.Options;
            string realPath = projectItem.Document.FullName;
            if(!System.IO.File.Exists(realPath)) {
                return;
            }
            string path = GetProjectPath(projectItem);
            if(string.IsNullOrEmpty(path)) {
                return;
            }
            string extension = System.IO.Path.GetExtension(realPath);
            if(0 < extension.Length && '.' == extension[0]) {
                extension = extension.Substring(1);
            }

            if(!dialog.ExtensionSet.Contains(extension)) {
                return;
            }

            lock(lock_) {
                if(inIndexing_) {
                    return;
                }
                inIndexing_ = true;
            }
            IndexWriterConfig indexWriterConfig = new IndexWriterConfig(AppLuceneVersion, analyzer_);
            indexWriterConfig.OpenMode = OpenMode.CREATE_OR_APPEND;
            LiteDB.ILiteCollection<PathDate> pathdates = fileInfoDb_.GetCollection<PathDate>("pathdate");
            pathdates.EnsureIndex(x => x.Id, unique: true);
            using(IndexWriter indexWriter = new IndexWriter(indexDirectory_, indexWriterConfig)) {
                bool indexing = false;
                long currentLastWriteTime = 0;
                try {
                    System.IO.FileInfo fileInfo = new System.IO.FileInfo(realPath);
                    currentLastWriteTime = UtcNow(fileInfo.LastWriteTimeUtc);

                    var result = pathdates.FindOne(x => x.Id == path);
                    if(null != result) {
                        if(result.Date < currentLastWriteTime) {
                            indexWriter.DeleteDocuments(new TermQuery(new Term("path", path)));
                            indexWriter.Commit();
                            indexing = true;
                        }
                    }
                } catch {
                }
                if(indexing) {
                    List<Lucene.Net.Documents.Document> documents = new List<Lucene.Net.Documents.Document>(1024);
                    try {
                        int lineCount = 0;
                        {
                            foreach(string line in System.IO.File.ReadAllLines(realPath)) {
                                Lucene.Net.Documents.Document document = new Lucene.Net.Documents.Document();
                                Lucene.Net.Documents.StringField pathField = new Lucene.Net.Documents.StringField("path", path, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(pathField);
                                Lucene.Net.Documents.Field lineField = new Lucene.Net.Documents.Int32Field("line", lineCount, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(lineField);
                                Lucene.Net.Documents.Field content = new Lucene.Net.Documents.TextField(FieldContent, line, Lucene.Net.Documents.Field.Store.YES);
                                document.Add(content);
                                Lucene.Net.Documents.Field content_low = new Lucene.Net.Documents.TextField(FieldContentLow, line.ToLower(), Lucene.Net.Documents.Field.Store.YES);
                                document.Add(content_low);
                                documents.Add(document);

                                ++lineCount;
                            }
                            indexWriter.AddDocuments(documents);
                            documents.Clear();
                        }
                        pathdates.Upsert(new PathDate { Id = path, Date = currentLastWriteTime });
                        indexWriter.Commit();
                    } catch(Exception e) {
                        await Log.OutputAsync(string.Format("HandyTools: Indexing {0}\n", e));
                    }
                }
            }
            lock(lock_) {
                lastIndexUpdated_ = UtcNow();
                inIndexing_ = false;
            }
        }

        public async System.Threading.Tasks.Task<SearchResult?> SearchAsync(SearchWindowControl control, SearchQuery searchQuery)
        {
            HandyToolsPackage package = await serviceProvider_.GetServiceAsync(typeof(HandyToolsPackage)) as HandyToolsPackage;
            if(null == package) {
                return null;
            }
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            RebuildPachCache();

            if(false == await CheckInitializeAsync()) {
                return null;
            }
            OptionPageHandyTools dialog = package.Options;
            int maxSearchItems = dialog.MaxSearchItems;

            Stopwatch stopwatch = Stopwatch.StartNew();
            SearchResult searchResult = new SearchResult();
            try {
                control.Results.Clear();
                using(DirectoryReader indexReader = DirectoryReader.Open(indexDirectory_)) {
                    IndexSearcher indexSearcher = new IndexSearcher(indexReader);
                    Analyzer analyzer = searchQuery.caseSensitive_? analyzer_ : lowerAnalyzer_;
                    string field = searchQuery.caseSensitive_ ? FieldContent : FieldContentLow;

                    Query query = null;
                    switch(searchQuery.method_) {
                    case SearchWindowControl.SearchMethod.Simple: {
                        StandardQueryParser standardQueryParser = new StandardQueryParser(analyzer);
                        QueryConfigHandler config = standardQueryParser.QueryConfigHandler;
                        config.Set(ConfigurationKeys.DEFAULT_OPERATOR, StandardQueryConfigHandler.Operator.AND);
                        //config.Set(ConfigurationKeys.ALLOW_LEADING_WILDCARD, true);
                        query = standardQueryParser.Parse(searchQuery.text_, field);
                    }
                    break;
                    case SearchWindowControl.SearchMethod.Fuzzy: {
                        query = new FuzzyQuery(new Term(field, searchQuery.text_), dialog.FuzzyMinSimilarity);
                    }
                    break;
                    }
                    TopDocs result = indexSearcher.Search(query, maxSearchItems);
                    if(null == result || null == result.ScoreDocs) {
                        return searchResult;
                    }
                    searchResult.hits_ = result.ScoreDocs.Length;
                    searchResult.totalHits_ = result.TotalHits;
                    int count = 0;
                    foreach(ScoreDoc scoreDoc in result.ScoreDocs) {
                        Lucene.Net.Documents.Document doc = indexSearcher.Doc(scoreDoc.Doc);
                        control.Results.Add(new SearchWindowControl.SearchResult {
                            Path = doc.GetField("path").GetStringValue(),
                            Content = doc.GetField(field).GetStringValue(),
                            Line = doc.GetField("line").GetInt32ValueOrDefault()
                        });
                        if(1024 <= ++count) {
                            await System.Threading.Tasks.Task.Yield();
                        }
                    }
                }
            } catch(Exception e) {
                await Log.OutputAsync(string.Format("HandyTools: Searching {0}\n", e));
            }
            stopwatch.Stop();
            searchResult.totalTime_ = stopwatch.ElapsedMilliseconds;
            control.TextStatus = string.Format("Hits:{0}/{1} {2} ms", searchResult.hits_, searchResult.totalHits_, searchResult.totalTime_);
            return searchResult;
        }

        public static string GetRealPath(string itemPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if(string.IsNullOrEmpty(itemPath)) {
                return string.Empty;
            }
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return string.Empty;
            }
            if(null == dte2.Solution.Projects) {
                return string.Empty;
            }
            string[] sections = itemPath.Split('/', '\\');
            if(null == sections || sections.Length < 2) {
                return string.Empty;
            }
            string path = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FileName);
            if(sections[0] != path) {
                return string.Empty;
            }
            foreach(EnvDTE.Project project in dte2.Solution.Projects) {
                if(project.Name != sections[1]) {
                    continue;
                }
                path = GetRealPathTraverse(2, sections, project.ProjectItems);
                if(!string.IsNullOrEmpty(path)) {
                    return path;
                }
            }
            return string.Empty;
        }

        private static string GetRealPathTraverse(int count, string[] sections, ProjectItems projectItems)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            foreach(EnvDTE.ProjectItem projectItem in projectItems) {
                if(projectItem.Name != sections[count]) {
                    continue;
                }

                switch(projectItem.Kind) {
                case EnvDTE.Constants.vsProjectItemKindPhysicalFile:
                    return 0 < projectItem.FileCount ? projectItem.FileNames[0] : string.Empty;
                default:
                    break;
                }
                if(null != projectItem.ProjectItems) {
                    string path = GetRealPathTraverse(count + 1, sections, projectItem.ProjectItems);
                    if(!string.IsNullOrEmpty(path)) {
                        return path;
                    }
                } else if(null != projectItem.SubProject && null != projectItem.SubProject.ProjectItems) {
                    string path = GetRealPathTraverse(count + 1, sections, projectItem.SubProject.ProjectItems);
                    if(!string.IsNullOrEmpty(path)) {
                        return path;
                    }
                }
            }
            return string.Empty;
        }

        public static string GetProjectPath(ProjectItem projectItem)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            EnvDTE80.DTE2 dte2 = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if(null == dte2 || null == dte2.Solution) {
                return string.Empty;
            }
            if(null == dte2.Solution.Projects) {
                return string.Empty;
            }
            string projectPath = System.IO.Path.GetFileNameWithoutExtension(dte2.Solution.FileName);
            foreach(EnvDTE.Project project in dte2.Solution.Projects) {
                if(null == project.ProjectItems) {
                    continue;
                }
                string path = GetProjectPathTraverse(System.IO.Path.Combine(projectPath, project.Name), projectItem, project.ProjectItems);
                if(!string.IsNullOrEmpty(path)) {
                    return path;
                }
            }
            return string.Empty;
        }

        public static string GetProjectPathTraverse(string projectPath, ProjectItem item, ProjectItems projectItems)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            foreach(ProjectItem projectItem in projectItems) {
                switch(projectItem.Kind) {
                case EnvDTE.Constants.vsProjectItemKindPhysicalFile:
                    if(item == projectItem) {
                        return System.IO.Path.Combine(projectPath, projectItem.Name);
                    }
                    break;
                default:
                    break;
                }
                if(null != projectItem.ProjectItems) {
                    string path = GetProjectPathTraverse(System.IO.Path.Combine(projectPath, projectItem.Name), item, projectItem.ProjectItems);
                    if(!string.IsNullOrEmpty(path)) {
                        return path;
                    }
                } else if(null != projectItem.SubProject && null != projectItem.SubProject.ProjectItems) {
                    string path = GetProjectPathTraverse(System.IO.Path.Combine(projectPath, projectItem.Name), item, projectItem.SubProject.ProjectItems);
                    if(!string.IsNullOrEmpty(path)) {
                        return path;
                    }
                }
            }
            return string.Empty;
        }

        public void ClearPathCache()
        {
            pathCache_ = null;
        }

        private void RebuildPachCache()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            if(null == pathCache_) {
                pathCache_ = new HashSet<string>(64);
                GetProjectPaths(pathCache_);
            }
        }


        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider_;
        private LiteDB.LiteDatabase fileInfoDb_;
        private FSDirectory indexDirectory_;
        private Analyzer analyzer_;
        private Analyzer lowerAnalyzer_;
        private HashSet<string> pathCache_;

        private object lock_ = new object();
        private bool inIndexing_ = false;
        private long lastIndexUpdated_;
    }
}

