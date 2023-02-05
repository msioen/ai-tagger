using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AITagger.Utils;
using AppKit;
using Foundation;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using ObjCRuntime;
using ThreadNetwork;

namespace AITagger
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        private static readonly string _visionSubscriptionKey = "SUBSCRIPTION_KEY";
        private static readonly string _visionEndpoint = "ENDPOINT";

        private static readonly int _maxTagCount = 3;

        private readonly NSStatusItem _statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square);

        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            // setup menu
            _statusItem.Button.Image = NSImage.ImageNamed("StatusBarIcon");

            var menu = new NSMenu();

            var testItem = new NSMenuItem("Analyse File", OnAnalyseFile);
            menu.AddItem(testItem);

            _statusItem.Menu = menu;
        }

        public override void WillTerminate(NSNotification notification)
        {
        }

        private async Task HandleImage(ComputerVisionClient client, string filePath)
        {
            var toChangeFilePath = filePath;


            // TODO validate image: type / ... min size / ...

            // TODO make a local copy
            // TODO resize local copy for our purposes

            // tag/scan/execute
            // possibly other actions like OCR / other data sources to combine in calculation?
            var imageAnalysis = await AnalyseImage(client, toChangeFilePath);
            var imageResults = GetImageContents(imageAnalysis);

            // apply results

            // show notification (debug purposes - instead of writing tags)
            var notification = $"Caption: {imageResults.Captions.FirstOrDefault()}{Environment.NewLine}Tags: {string.Join(", ", imageResults.Tags)}";
            NotificationService.Instance.ShowNotification("Image Analysis Results", notification);

            // add tags to file (overwrites current tags)
            //var userTags = new NSMutableArray();
            //foreach(var tag in imageResults.Tags)
            //{
            //    userTags.Add((NSString)tag);
            //}
            //WriteUserTags(toChangeFilePath, userTags, out var writeError);
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

        private ImageResults GetImageContents(ImageAnalysis imageAnalysis)
        {
            var imageResults = new ImageResults();

            // Captions returns a short description of what the AI thinks is in the image - sometimes 'tag-style'
            if (imageAnalysis.Description?.Captions != null)
            {
                // TODO - can we get something sensible/useful in a 'max' filename length?
                foreach(var caption in imageAnalysis.Description.Captions.OrderByDescending(x => x.Confidence))
                {
                    imageResults.Captions.Add(caption.Text);
                }
            }

            // Tags contains items detected in the image with a confidence rating
            if (imageAnalysis.Tags != null)
            {
                // TODO - in future depending on settings/folder/...
                string[] ignoredTags = new string[] { "text", "font", "line", "number" };

                foreach(var tag in imageAnalysis.Tags
                    .Where(x => !ignoredTags.Contains(x.Name))
                    .OrderByDescending(x => x.Confidence)
                    .Take(_maxTagCount))
                {
                    imageResults.Tags.Add(tag.Name);
                }
            }

            return imageResults;
        }

        private NSMutableArray ReadUserTags(string filePath, out NSError error)
        {
            Mono.Unix.Native.Syscall.getxattr(filePath, "com.apple.metadata:_kMDItemUserTags", out var value);
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

            Mono.Unix.Native.Syscall.setxattr(filePath, "com.apple.metadata:_kMDItemUserTags", plist);
        }

        private async void OnAnalyseFile(object sender, EventArgs e)
        {
            var openPanel = new NSOpenPanel();
            openPanel.CanChooseDirectories = false;
            openPanel.CanChooseFiles = true;
            openPanel.RunModal();

            if (openPanel.Urls == null || openPanel.Urls.Length <= 0)
            {
                return;
            }

            var filePath = openPanel.Urls.FirstOrDefault()?.Path;

            var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(_visionSubscriptionKey))
            {
                Endpoint = _visionEndpoint
            };

            await HandleImage(client, filePath);

            //await AnalyzeImageLocal(client, testFile);
            //await ReadFileLocal(client, testFile);
        }

        private static async Task AnalyzeImageLocal(ComputerVisionClient client, string localImage)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("ANALYZE IMAGE - LOCAL IMAGE");
            Console.WriteLine();

            // Creating a list that defines the features to be extracted from the image. 
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
            {
                VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
                VisualFeatureTypes.Faces, VisualFeatureTypes.Tags,
                VisualFeatureTypes.Brands, VisualFeatureTypes.Objects
            };

            Console.WriteLine($"Analyzing the local image {Path.GetFileName(localImage)}...");
            Console.WriteLine();

            using (Stream analyzeImageStream = File.OpenRead(localImage))
            {
                // Analyze the local image.
                ImageAnalysis results = await client.AnalyzeImageInStreamAsync(analyzeImageStream, visualFeatures: features);

                // Sunmarizes the image content.
                if (null != results.Description && null != results.Description.Captions)
                {
                    Console.WriteLine("Summary:");
                    foreach (var caption in results.Description.Captions)
                    {
                        Console.WriteLine($"{caption.Text} with confidence {caption.Confidence}");
                    }
                    Console.WriteLine();
                }

                // Display categories the image is divided into.
                Console.WriteLine("Categories:");
                foreach (var category in results.Categories)
                {
                    Console.WriteLine($"{category.Name} with confidence {category.Score}");
                }
                Console.WriteLine();

                // Image tags and their confidence score
                if (null != results.Tags)
                {
                    Console.WriteLine("Tags:");
                    foreach (var tag in results.Tags)
                    {
                        Console.WriteLine($"{tag.Name} {tag.Confidence}");
                    }
                    Console.WriteLine();
                }

                // Objects
                if (null != results.Objects)
                {
                    Console.WriteLine("Objects:");
                    foreach (var obj in results.Objects)
                    {
                        Console.WriteLine($"{obj.ObjectProperty} with confidence {obj.Confidence} at location {obj.Rectangle.X}, " +
                          $"{obj.Rectangle.X + obj.Rectangle.W}, {obj.Rectangle.Y}, {obj.Rectangle.Y + obj.Rectangle.H}");
                    }
                    Console.WriteLine();
                }

                // Detected faces, if any.
                // interesting? objects also detects persons
                if (null != results.Faces)
                {
                    Console.WriteLine("Faces:");
                    foreach (var face in results.Faces)
                    {
                        Console.WriteLine($"A {face.Gender} of age {face.Age} at location {face.FaceRectangle.Left}, {face.FaceRectangle.Top}, " +
                          $"{face.FaceRectangle.Left + face.FaceRectangle.Width}, {face.FaceRectangle.Top + face.FaceRectangle.Height}");
                    }
                    Console.WriteLine();
                }

                // Well-known brands, if any.
                if (null != results.Brands)
                {
                    Console.WriteLine("Brands:");
                    foreach (var brand in results.Brands)
                    {
                        Console.WriteLine($"Logo of {brand.Name} with confidence {brand.Confidence} at location {brand.Rectangle.X}, " +
                          $"{brand.Rectangle.X + brand.Rectangle.W}, {brand.Rectangle.Y}, {brand.Rectangle.Y + brand.Rectangle.H}");
                    }
                    Console.WriteLine();
                }

                // Celebrities in image, if any.
                if (null != results.Categories)
                {
                    Console.WriteLine("Celebrities:");
                    foreach (var category in results.Categories)
                    {
                        if (category.Detail?.Celebrities != null)
                        {
                            foreach (var celeb in category.Detail.Celebrities)
                            {
                                Console.WriteLine($"{celeb.Name} with confidence {celeb.Confidence} at location {celeb.FaceRectangle.Left}, " +
                                  $"{celeb.FaceRectangle.Top},{celeb.FaceRectangle.Height},{celeb.FaceRectangle.Width}");
                            }
                        }
                    }
                    Console.WriteLine();
                }

                // Popular landmarks in image, if any.
                if (null != results.Categories)
                {
                    Console.WriteLine("Landmarks:");
                    foreach (var category in results.Categories)
                    {
                        if (category.Detail?.Landmarks != null)
                        {
                            foreach (var landmark in category.Detail.Landmarks)
                            {
                                Console.WriteLine($"{landmark.Name} with confidence {landmark.Confidence}");
                            }
                        }
                    }
                    Console.WriteLine();
                }
            }
        }

        private static async Task ReadFileLocal(ComputerVisionClient client, string localFile)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("READ FILE FROM LOCAL");
            Console.WriteLine();

            // Read text from URL
            var textHeaders = await client.ReadInStreamAsync(File.OpenRead(localFile));
            // After the request, get the operation location (operation ID)
            string operationLocation = textHeaders.OperationLocation;
            Thread.Sleep(2000);

            // <snippet_extract_response>
            // Retrieve the URI where the recognized text will be stored from the Operation-Location header.
            // We only need the ID and not the full URL
            const int numberOfCharsInOperationId = 36;
            string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

            // Extract the text
            ReadOperationResult results;
            Console.WriteLine($"Reading text from local file {Path.GetFileName(localFile)}...");
            Console.WriteLine();
            do
            {
                results = await client.GetReadResultAsync(Guid.Parse(operationId));
            }
            while ((results.Status == OperationStatusCodes.Running ||
                results.Status == OperationStatusCodes.NotStarted));
            // </snippet_extract_response>

            // <snippet_extract_display>
            // Display the found text.
            Console.WriteLine();
            var textUrlFileResults = results.AnalyzeResult.ReadResults;
            foreach (ReadResult page in textUrlFileResults)
            {
                foreach (Line line in page.Lines)
                {
                    Console.WriteLine(line.Text);
                }
            }
            Console.WriteLine();
        }


    }
}

