using System;
using System.Collections.Generic;

namespace AITagger.Model
{
    public class AITaggerSettings
    {
        /// <summary>
        /// Indicates if file watcher should automatically tag new files in subscribed directories
        /// </summary>
        public bool AutoTaggingEnabled { get; set; }

        /// <summary>
        /// Indicates the max amount of tags to apply to a file
        /// </summary>
        public int MaxTagCount { get; set; }

        /// <summary>
        /// List of tags to ignore across all directories
        /// </summary>
        public List<string> IgnoredTags { get; set; }

        /// <summary>
        /// List of file extensions to pick up
        /// </summary>
        public List<string> FileExtensionsToTag { get; set; }

        /// <summary>
        /// Animate menu bar icon while processing
        /// </summary>
        public bool AnimateMenuBar { get; set; }

        /// <summary>
        /// Indicates if total count of items to handle should be shown in menu bar
        /// </summary>
        public bool ShowCountInMenuBar { get; set; }
    }
}

