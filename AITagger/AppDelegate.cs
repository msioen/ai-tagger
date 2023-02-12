using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AITagger.Model;
using AppKit;
using CoreServices;
using Foundation;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Newtonsoft.Json;

namespace AITagger
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private static readonly string _visionSubscriptionKey = "SUBSCRIPTION_KEY";
        private static readonly string _visionEndpoint = "ENDPOINT";

        private static readonly string _donutEndpoint = "http://localhost:55001";
        private static readonly string _donutKey = "";

        private const string XATTR_FILE_TAGS = "com.apple.metadata:_kMDItemUserTags";
        private const string XATTR_AITAGGER_HANDLED = "be.michielsioen.AITagger:HandledAt";

        private readonly NSStatusItem _statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);

        private readonly AITaggerSettings _settings;
        private readonly List<DirectorySettings> _directories;

        private FSEventStream _eventStream;

        private Queue<string> _filesToHandle = new Queue<string>();
        private bool _handlingFile;

        private Timer _animationTimer;

        private const int _iconAnimationFrameTotal = 30;
        private int _iconAnimcationCurrFrame = 1;

        public AppDelegate()
        {
            // TODO - manage through UI/settings instead of manual model

            _settings = new AITaggerSettings()
            {
                AutoTaggingEnabled = true,
                MaxTagCount = 3,
                IgnoredTags = new List<string> { "text", "font", "line", "number" },
                FileExtensionsToTag = new List<string> { ".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG" },
                AnimateMenuBar = true,
                ShowCountInMenuBar = true,
            };

            _directories = new List<DirectorySettings>()
            {
                new DirectorySettings()
                {
                    Path = "/Users/michielsioen/Desktop/screenshots",
                    IgnoredTags = new List<string>()
                }
            };
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            // setup menu
            _statusItem.Button.Image = NSImage.ImageNamed("StatusBarIcon");
            _statusItem.Button.ImagePosition = NSCellImagePosition.ImageLeft;

            //var menu = new NSMenu();

            //var testItem = new NSMenuItem("Analyse File", OnAnalyseFile);
            //menu.AddItem(testItem);

            //_statusItem.Menu = menu;

            // start listening to new events
            InitializeFSEventStream();

            // todo - validate if we should action on old files/events as well
        }

        public override void WillTerminate(NSNotification notification)
        {
        }

        private void InitializeFSEventStream()
        {
            if (_eventStream != null)
            {
                _eventStream.Events -= FSEventStream_Events;
                _eventStream.Dispose();
                _eventStream = null;
            }

            if (_settings.AutoTaggingEnabled && _directories.Count > 0)
            {
                var pathsToWatch = _directories.Select(x => x.Path).ToArray();
                _eventStream = new FSEventStream(pathsToWatch, TimeSpan.FromMilliseconds(250), FSEventStreamCreateFlags.FileEvents);
                _eventStream.Events += FSEventStream_Events;

                _eventStream.ScheduleWithRunLoop(NSRunLoop.Current);
                _eventStream.Start();
            }
        }

        private void FSEventStream_Events(object sender, FSEventStreamEventsArgs args)
        {
            // screenshots are initially created with different filename => ensure to also trigger on rename

            foreach (var ev in args.Events)
            {
                if ((ev.Flags.HasFlag(FSEventStreamEventFlags.ItemCreated) || ev.Flags.HasFlag(FSEventStreamEventFlags.ItemRenamed)) &&
                    ev.Flags.HasFlag(FSEventStreamEventFlags.ItemIsFile) &&
                    _settings.FileExtensionsToTag.Contains(Path.GetExtension(ev.Path)) &&
                    !Path.GetFileName(ev.Path).StartsWith('.'))
                {
                    _filesToHandle.Enqueue(ev.Path);
                }
            }

            HandleFilesQueue();
        }

        private async void HandleFilesQueue()
        {
            if (_settings.ShowCountInMenuBar)
            {
                var count = _filesToHandle.Count;
                if (_handlingFile) count++;
                _statusItem.Button.Title = count > 0 ? count.ToString() : string.Empty;
            }

            if (_handlingFile || _filesToHandle.Count == 0)
                return;

            _handlingFile = true;
            StartIconAnimation();

            try
            {
                var file = _filesToHandle.Dequeue();

                if (File.Exists(file) && ReadAITaggerHandledAt(file) == null)
                {
                    var dirSettings = _directories.FirstOrDefault(x => file.StartsWith(x.Path));
                    await HandleImage(dirSettings, file);
                }
            }
            catch { }
            finally
            {
                _handlingFile = false;
                StopIconAnimation();
            }

            HandleFilesQueue();
        }

        private async Task HandleImage(DirectorySettings dirSettings, string filePath)
        {
            try
            {
                // 1. prep file (copy / resize)
                // TODO validate image: type / ... min size / ... (?)
                // TODO make a local copy
                // TODO resize local copy for our purposes

                // 2a. get donut info
                var donutTask = ExecuteDonutRvlCdip(dirSettings, filePath);

                // 2b. get azure ai info
                var azureTask = ExecuteAzureVision(dirSettings, filePath);

                await Task.WhenAll(donutTask, azureTask);

                // 3. apply tags
                var userTags = new NSMutableArray();
                if (donutTask.Result != null && !string.IsNullOrEmpty(donutTask.Result.Class))
                {
                    userTags.Add((NSString)donutTask.Result.Class);
                }
                foreach (var tag in azureTask.Result.Tags.Take(_settings.MaxTagCount - 1))
                {
                    userTags.Add((NSString)tag);
                }
                WriteUserTags(filePath, userTags, out var writeError);
            }
            catch { }

            try
            {
                // 4. apply metadata => mark as handled even if something failed above
                WriteAITaggerHandledAt(filePath);

                // 5. cleanup prep
                // TODO remove local copy
            }
            catch { }
        }

        private async Task<DonutResponse> ExecuteDonutRvlCdip(DirectorySettings dirSettings, string filePath)
        {
            try
            {
                var client = new HttpClient();

                if (!string.IsNullOrWhiteSpace(_donutKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer: {_donutKey}");
                }

                var url = $"{_donutEndpoint}/score";

                var bytes = File.ReadAllBytes(filePath);

                var requestContent = new MultipartFormDataContent
                {
                    { new ByteArrayContent(bytes), "image", filePath }
                };

                var response = await client.PostAsync(url, requestContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<DonutResponse>(responseContent);
            }
            catch
            {
                return null;
            }
        }

        private async Task<ImageResults> ExecuteAzureVision(DirectorySettings dirSettings, string filePath)
        {
            var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(_visionSubscriptionKey))
            {
                Endpoint = _visionEndpoint
            };

            var imageAnalysis = await AnalyseImage(client, filePath);
            return GetImageContents(dirSettings, imageAnalysis);
        }

        private void StartIconAnimation()
        {
            if (_animationTimer != null)
            {
                StopIconAnimation();
            }
            if (!_settings.AnimateMenuBar)
            {
                return;
            }

            _animationTimer = new Timer(OnTimerTick, null, 40, 40);
        }

        private void OnTimerTick(object state)
        {
            InvokeOnMainThread(() => _statusItem.Button.Image = NSImage.ImageNamed($"frame-{_iconAnimcationCurrFrame}"));

            _iconAnimcationCurrFrame++;
            if (_iconAnimcationCurrFrame > _iconAnimationFrameTotal)
                _iconAnimcationCurrFrame = 1;
        }

        private void StopIconAnimation()
        {
            if (_animationTimer != null)
            {
                _animationTimer.Dispose();
                _animationTimer = null;
            }

            _statusItem.Button.Image = NSImage.ImageNamed("StatusBarIcon");
        }

        private async Task<ImageAnalysis> AnalyseImage(ComputerVisionClient client, string filePath)
        {
            var features = new List<VisualFeatureTypes?>()
            {
                //VisualFeatureTypes.Categories,
                VisualFeatureTypes.Description,
                VisualFeatureTypes.Tags,
                //VisualFeatureTypes.Brands,
                //VisualFeatureTypes.Objects
            };

            try
            {
                using (var analyseImageStream = File.OpenRead(filePath))
                {
                    // if only tags and description are used this could be used instead
                    //var results2 = await client.DescribeImageInStreamAsync(analyzeImageStream);
                    return await client.AnalyzeImageInStreamAsync(analyseImageStream, visualFeatures: features);
                }
            }
            catch (Exception ex)
            {
                // TODO - define behaviour on errors
                return null;
            }
        }

        private ImageResults GetImageContents(DirectorySettings directorysettings, ImageAnalysis imageAnalysis)
        {
            var imageResults = new ImageResults();

            // Captions returns a short description of what the AI thinks is in the image - sometimes 'tag-style'
            if (imageAnalysis.Description?.Captions != null)
            {
                // TODO - can we get something sensible/useful in a 'max' filename length?
                foreach (var caption in imageAnalysis.Description.Captions.OrderByDescending(x => x.Confidence))
                {
                    imageResults.Captions.Add(caption.Text);
                }
            }

            // Tags contains items detected in the image with a confidence rating
            if (imageAnalysis.Tags != null)
            {
                foreach (var tag in imageAnalysis.Tags
                    .Where(x => !_settings.IgnoredTags.Contains(x.Name))
                    .Where(x => !directorysettings?.IgnoredTags?.Contains(x.Name) ?? true)
                    .OrderByDescending(x => x.Confidence))
                {
                    imageResults.Tags.Add(tag.Name);
                }
            }

            return imageResults;
        }

        private DateTime? ReadAITaggerHandledAt(string filePath)
        {
            try
            {
                Mono.Unix.Native.Syscall.getxattr(filePath, XATTR_AITAGGER_HANDLED, out var value);
                if (value == null)
                {
                    return null;
                }

                var now = BitConverter.ToInt64(value);
                return DateTime.FromBinary(now);
            }
            catch
            {
                return null;
            }
        }

        private void WriteAITaggerHandledAt(string filePath)
        {
            var now = BitConverter.GetBytes(DateTime.Now.ToBinary());
            Mono.Unix.Native.Syscall.setxattr(filePath, XATTR_AITAGGER_HANDLED, now);
        }

        private NSMutableArray ReadUserTags(string filePath, out NSError error)
        {
            Mono.Unix.Native.Syscall.getxattr(filePath, XATTR_FILE_TAGS, out var value);
            return BinaryPListToArray(value, out error);
        }

        private NSMutableArray BinaryPListToArray(byte[] plist, out NSError error)
        {
            var data = NSData.FromArray(plist);
            var format = NSPropertyListFormat.Binary;

            return (NSMutableArray)NSPropertyListSerialization.PropertyListWithData(data, NSPropertyListReadOptions.MutableContainersAndLeaves, ref format, out error);
        }

        private void WriteUserTags(string filePath, NSMutableArray userTags, out NSError error)
        {
            var data = NSPropertyListSerialization.DataWithPropertyList(userTags, NSPropertyListFormat.Binary, out error);
            var plist = data.ToArray();

            Mono.Unix.Native.Syscall.setxattr(filePath, XATTR_FILE_TAGS, plist);
        }
    }
}