using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cloudless
{
    public class TagManager
    {
        private static TagManager? _instance;
        private static readonly object _instanceLock = new();

        // Mutex for file I/O synchronization (handles multi-instance access)
        private static readonly System.Threading.Mutex _tagsMutex = new(false, "CloudlessTagsMutex");

        // In-memory index: tag (lowercase) -> set of normalized file paths
        private Dictionary<string, HashSet<string>> _tagToFiles = new();
        // Reverse index: file path -> set of tags (lowercase)
        private Dictionary<string, HashSet<string>> _fileToTags = new();

        private readonly string _tagsFilePath;
        private bool _isLoaded = false;

        // Reserved keywords that cannot be used as tags
        private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "and", "or", "not"
        };

        public static TagManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new TagManager();
                    }
                }
                return _instance;
            }
        }

        private TagManager()
        {
            _tagsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cloudless",
                "tags.json");
        }

        /// <summary>
        /// Load tags from disk (thread-safe).
        /// </summary>
        public void Load()
        {
            _tagsMutex.WaitOne();
            try
            {
                if (!File.Exists(_tagsFilePath))
                {
                    _tagToFiles.Clear();
                    _fileToTags.Clear();
                    _isLoaded = true;
                    return;
                }

                string json = File.ReadAllText(_tagsFilePath);
                var data = JsonSerializer.Deserialize<TagsData>(json);

                _tagToFiles.Clear();
                _fileToTags.Clear();

                if (data?.Tags != null)
                {
                    foreach (var kvp in data.Tags)
                    {
                        string tagLower = kvp.Key.ToLower();
                        var filePaths = new HashSet<string>(kvp.Value.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
                        _tagToFiles[tagLower] = filePaths;

                        foreach (var filePath in filePaths)
                        {
                            if (!_fileToTags.ContainsKey(filePath))
                                _fileToTags[filePath] = new();
                            _fileToTags[filePath].Add(tagLower);
                        }
                    }
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load tags: {ex.Message}");
                _isLoaded = true;
            }
            finally
            {
                _tagsMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Save tags to disk (thread-safe).
        /// </summary>
        public void Save()
        {
            _tagsMutex.WaitOne();
            try
            {
                // Ensure directory exists
                string dir = Path.GetDirectoryName(_tagsFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Build output data structure
                var data = new TagsData
                {
                    Tags = new Dictionary<string, List<string>>()
                };

                foreach (var kvp in _tagToFiles)
                {
                    data.Tags[kvp.Key] = kvp.Value.OrderBy(p => p).ToList();
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_tagsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save tags: {ex.Message}");
            }
            finally
            {
                _tagsMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Add one or more tags to a file (normalized path internally).
        /// Tags are case-insensitive.
        /// </summary>
        public List<string> AddTags(string filePath, params string[] tags)
        {
            EnsureLoaded();

            string normalizedPath = NormalizePath(filePath);
            var messages = new List<string>();

            foreach (var tag in tags)
            {
                string tagLower = tag.ToLower().Trim();

                // Validate tag
                if (string.IsNullOrWhiteSpace(tagLower))
                {
                    messages.Add("Tag cannot be empty.");
                    continue;
                }

                if (ReservedKeywords.Contains(tagLower))
                {
                    messages.Add($"Cannot use reserved keyword '{tag}' as a tag.");
                    continue;
                }

                if (!_tagToFiles.ContainsKey(tagLower))
                    _tagToFiles[tagLower] = new();

                if (_tagToFiles[tagLower].Contains(normalizedPath))
                {
                    messages.Add($"File already has tag '{tag}'.");
                }
                else
                {
                    _tagToFiles[tagLower].Add(normalizedPath);

                    if (!_fileToTags.ContainsKey(normalizedPath))
                        _fileToTags[normalizedPath] = new();
                    _fileToTags[normalizedPath].Add(tagLower);

                    messages.Add($"Added tag '{tag}' to file.");
                }
            }

            // Only write if at least one tag was added
            bool anyAdded = messages.Any(m => m.Contains("Added tag"));
            if (anyAdded)
                Save();

            return messages;
        }

        /// <summary>
        /// Remove one or more tags from a file.
        /// If a tag doesn't exist, it is ignored (no error).
        /// </summary>
        public List<string> RemoveTags(string filePath, params string[] tags)
        {
            EnsureLoaded();

            string normalizedPath = NormalizePath(filePath);
            var messages = new List<string>();
            bool anyRemoved = false;

            foreach (var tag in tags)
            {
                string tagLower = tag.ToLower().Trim();

                if (string.IsNullOrWhiteSpace(tagLower))
                {
                    messages.Add("Tag cannot be empty.");
                    continue;
                }

                if (_tagToFiles.ContainsKey(tagLower) && _tagToFiles[tagLower].Contains(normalizedPath))
                {
                    _tagToFiles[tagLower].Remove(normalizedPath);

                    if (_fileToTags.ContainsKey(normalizedPath))
                        _fileToTags[normalizedPath].Remove(tagLower);

                    messages.Add($"Removed tag '{tag}' from file.");
                    anyRemoved = true;

                    // Clean up empty tag entries
                    if (_tagToFiles[tagLower].Count == 0)
                        _tagToFiles.Remove(tagLower);
                }
                else
                {
                    messages.Add($"File does not have tag '{tag}' (ignored).");
                }
            }

            if (anyRemoved)
                Save();

            return messages;
        }

        /// <summary>
        /// Remove a tag from all files in the system.
        /// </summary>
        public string DestroyTag(string tag)
        {
            EnsureLoaded();

            string tagLower = tag.ToLower().Trim();

            if (!_tagToFiles.ContainsKey(tagLower))
                return $"Tag '{tag}' does not exist.";

            int fileCount = _tagToFiles[tagLower].Count;
            var filePaths = _tagToFiles[tagLower].ToList();

            // Remove tag from all files
            foreach (var filePath in filePaths)
            {
                if (_fileToTags.ContainsKey(filePath))
                    _fileToTags[filePath].Remove(tagLower);
            }

            _tagToFiles.Remove(tagLower);
            Save();

            return $"Destroyed tag '{tag}' from {fileCount} file(s).";
        }

        /// <summary>
        /// Query files by tags using boolean expressions (AND, OR, NOT, parentheses).
        /// Examples:
        ///   "landscape" -> all files with tag "landscape"
        ///   "landscape AND photo" -> files with both tags
        ///   "landscape OR seascape" -> files with either tag
        ///   "landscape AND NOT edited" -> landscape files not marked as edited
        ///   "(landscape OR seascape) AND processed" -> processed landscape or seascape files
        /// </summary>
        public List<string> QueryTags(string query)
        {
            EnsureLoaded();

            try
            {
                var tokens = TokenizeQuery(query);
                if (tokens.Count == 0)
                    return new List<string>();

                var result = EvaluateQuery(tokens);
                return result.OrderBy(p => p).ToList();
            }
            catch
            {
                // Query parsing error; return empty result
                return new List<string>();
            }
        }

        /// <summary>
        /// Get all tags applied to a given file.
        /// </summary>
        public List<string> GetTagsForFile(string filePath)
        {
            EnsureLoaded();

            string normalizedPath = NormalizePath(filePath);
            if (_fileToTags.ContainsKey(normalizedPath))
                return _fileToTags[normalizedPath].OrderBy(t => t).ToList();

            return new List<string>();
        }

        /// <summary>
        /// Get all files with a specific tag.
        /// </summary>
        public List<string> GetFilesWithTag(string tag)
        {
            EnsureLoaded();

            string tagLower = tag.ToLower().Trim();
            if (_tagToFiles.ContainsKey(tagLower))
                return _tagToFiles[tagLower].OrderBy(p => p).ToList();

            return new List<string>();
        }

        /// <summary>
        /// Get all tags currently defined.
        /// </summary>
        public List<string> GetAllTags()
        {
            EnsureLoaded();
            return _tagToFiles.Keys.OrderBy(t => t).ToList();
        }

        /// <summary>
        /// Get count of files with a specific tag.
        /// </summary>
        public int GetTagFileCount(string tag)
        {
            EnsureLoaded();

            string tagLower = tag.ToLower().Trim();
            return _tagToFiles.ContainsKey(tagLower) ? _tagToFiles[tagLower].Count : 0;
        }

        // ---- Private helpers ----

        private void EnsureLoaded()
        {
            if (!_isLoaded)
                Load();
        }

        private string NormalizePath(string filePath)
        {
            return Path.GetFullPath(filePath).ToLower();
        }

        /// <summary>
        /// Tokenize a query string into a list of tokens (tags, operators, parentheses).
        /// Validates reserved keywords are not used outside of boolean operators.
        /// </summary>
        private List<string> TokenizeQuery(string query)
        {
            query = query.Trim();
            if (string.IsNullOrEmpty(query))
                return new List<string>();

            var tokens = new List<string>();
            var current = "";

            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];

                if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current);
                        current = "";
                    }
                }
                else if (c == '(' || c == ')')
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current);
                        current = "";
                    }
                    tokens.Add(c.ToString());
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0)
                tokens.Add(current);

            return tokens;
        }

        /// <summary>
        /// Evaluate a tokenized query and return matching file paths.
        /// </summary>
        private HashSet<string> EvaluateQuery(List<string> tokens)
        {
            var evaluator = new QueryEvaluator(_tagToFiles);
            return evaluator.Evaluate(tokens);
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        private class TagsData
        {
            public Dictionary<string, List<string>> Tags { get; set; } = new();
        }
    }

    /// <summary>
    /// Helper class to evaluate boolean tag queries.
    /// Supports AND, OR, NOT operators and parentheses for grouping.
    /// </summary>
    internal class QueryEvaluator
    {
        private Dictionary<string, HashSet<string>> _tagToFiles;
        private List<string> _tokens;
        private int _position;

        public QueryEvaluator(Dictionary<string, HashSet<string>> tagToFiles)
        {
            _tagToFiles = tagToFiles;
            _tokens = new();
            _position = 0;
        }

        public HashSet<string> Evaluate(List<string> tokens)
        {
            _tokens = tokens;
            _position = 0;
            return ParseOr();
        }

        private HashSet<string> ParseOr()
        {
            var left = ParseAnd();
            while (_position < _tokens.Count && _tokens[_position].Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                _position++;
                var right = ParseAnd();
                left.UnionWith(right);
            }
            return left;
        }

        private HashSet<string> ParseAnd()
        {
            var left = ParseNot();
            while (_position < _tokens.Count && _tokens[_position].Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                _position++;
                var right = ParseNot();
                left.IntersectWith(right);
            }
            return left;
        }

        private HashSet<string> ParseNot()
        {
            if (_position < _tokens.Count && _tokens[_position].Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                _position++;
                var operand = ParseAtom();

                // NOT: complement of operand (all files not in operand)
                var allFiles = new HashSet<string>();
                foreach (var files in _tagToFiles.Values)
                    allFiles.UnionWith(files);

                allFiles.ExceptWith(operand);
                return allFiles;
            }

            return ParseAtom();
        }

        private HashSet<string> ParseAtom()
        {
            if (_position >= _tokens.Count)
                return new HashSet<string>();

            string token = _tokens[_position];

            if (token == "(")
            {
                _position++;
                var result = ParseOr();
                if (_position < _tokens.Count && _tokens[_position] == ")")
                    _position++;
                return result;
            }

            if (token.Equals("OR", StringComparison.OrdinalIgnoreCase)
                || token.Equals("AND", StringComparison.OrdinalIgnoreCase)
                || token.Equals("NOT", StringComparison.OrdinalIgnoreCase)
                || token == ")")
            {
                throw new ArgumentException($"Unexpected token: {token}");
            }

            // It's a tag
            _position++;
            string tagLower = token.ToLower();
            if (_tagToFiles.ContainsKey(tagLower))
                return new HashSet<string>(_tagToFiles[tagLower]);

            return new HashSet<string>();
        }
    }
}
