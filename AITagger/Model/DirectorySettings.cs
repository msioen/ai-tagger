using System;
using System.Collections.Generic;

namespace AITagger.Model
{
    public class DirectorySettings
    {
        /// <summary>
        /// Path on disk of a directory to watch by AI Tagger
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// List of tags to ignore for this specific directory - this is in addition to IgnoredTags in general settings
        /// </summary>
        public List<string> IgnoredTags { get; set; }
    }
}

