// Copyright @ MyScript. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.IO;

using MyScript.IInk.UIReferenceImplementation;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Networking.NetworkOperators;
using Windows.Networking.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO.Compression;



namespace MyScript.IInk.Demo
{
    public class FlyoutCommand : System.Windows.Input.ICommand
    {
        public delegate void InvokedHandler(FlyoutCommand command);

        public string Id { get; set; }
        private InvokedHandler _handler = null;

        public FlyoutCommand(string id, InvokedHandler handler)
        {
            Id = id;
            _handler = handler;
        }

        public bool CanExecute(object parameter)
        {
            return _handler != null;
        }

        public void Execute(object parameter)
        {
            _handler(this);
        }

        public event EventHandler CanExecuteChanged;
    }

    public sealed partial class MainPage
    {
        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register("Editor", typeof(Editor), typeof(MainPage),
                new PropertyMetadata(default(Editor)));

        public Editor Editor
        {
            get => GetValue(EditorProperty) as Editor;
            set => SetValue(EditorProperty, value);
        }
    }

    public sealed partial class MainPage
    {
        private Graphics.Point _lastPointerPosition;
        private IContentSelection _lastContentSelection;

        private int _filenameIndex;
        private string _packageName;

        // Offscreen rendering
        private float _dpiX = 96;
        private float _dpiY = 96;

        public MainPage()
        {
            _filenameIndex = 0;
            _packageName = "";

            InitializeComponent();
            Initialize(App.Engine);
            Export_content(); // start the selection
        }

        private void Initialize(Engine engine)
        {
            // Initialize the editor with the engine
            var info = DisplayInformation.GetForCurrentView();
            _dpiX = info.RawDpiX;
            _dpiY = info.RawDpiY;
            var pixelDensity = UcEditor.GetPixelDensity();

            if (pixelDensity > 0.0f)
            {
                _dpiX /= pixelDensity;
                _dpiY /= pixelDensity;
            }

            // RawDpi properties can return 0 when the monitor does not provide physical dimensions and when the user is
            // in a clone or duplicate multiple -monitor setup.
            if (_dpiX == 0 || _dpiY == 0)
                _dpiX = _dpiY = 96;

            FontMetricsProvider.Initialize();

            var renderer = engine.CreateRenderer(_dpiX, _dpiY, UcEditor);
            renderer.AddListener(new RendererListener(UcEditor));
            var toolController = engine.CreateToolController();
            Initialize(Editor = engine.CreateEditor(renderer, toolController));

            NewFile();
        }

        private void Initialize(Editor editor)
        {
            editor.SetViewSize((int)ActualWidth, (int)ActualHeight);
            editor.SetFontMetricsProvider(new FontMetricsProvider(_dpiX, _dpiY));
            editor.AddListener(new EditorListener(UcEditor));
        }

        private void ResetSelection()
        {
            if (_lastContentSelection != null)
            {
                var contentBlock = _lastContentSelection as ContentBlock;
                if (contentBlock != null)
                    contentBlock.Dispose();
                else
                {
                    var contentSelection = _lastContentSelection as ContentSelection;
                    contentSelection?.Dispose();
                }
                _lastContentSelection = null;
            }
        }

        private async void AppBar_ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Clear();
        }

        private void AppBar_NewPackageButton_Click(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private async void AppBar_NewPartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Editor.Part == null)
            {
                NewFile();
                return;
            }

            var partType = Editor.Engine.SupportedPartTypes.ToList()[5];

            if (!string.IsNullOrEmpty(partType))
            {
                ResetSelection();

                var previousPart = Editor.Part;
                var package = previousPart.Package;

                try
                {
                    Editor.Part = null;

                    var part = package.CreatePart(partType);
                    Editor.Part = part;
                    Title.Text = _packageName + " - " + part.Type;

                    previousPart.Dispose();
                }
                catch (Exception ex)
                {
                    Editor.Part = previousPart;
                    Title.Text = _packageName + " - " + Editor.Part.Type;

                    var msgDialog = new MessageDialog(ex.ToString());
                    await msgDialog.ShowAsync();
                }

                // Reset viewing parameters
                UcEditor.ResetView(false);
            }
        }

        private void AppBar_PreviousPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = Editor.Part;

            if (part != null)
            {
                var package = part.Package;
                var index = package.IndexOfPart(part);

                if (index > 0)
                {
                    ResetSelection();
                    Editor.Part = null;

                    while (--index >= 0)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            Editor.Part = newPart;
                            Title.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Can't set this part, try the previous one
                            Editor.Part = null;
                            Title.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index < 0)
                    {
                        // Restore current part if none can be set
                        Editor.Part = part;
                        Title.Text = _packageName + " - " + part.Type;
                    }

                    // Reset viewing parameters
                    UcEditor.ResetView(false);
                }
            }
        }

        private void AppBar_NextPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = Editor.Part;

            if (part != null)
            {
                var package = part.Package;
                var count = package.PartCount;
                var index = package.IndexOfPart(part);

                if (index < count - 1)
                {
                    ResetSelection();
                    Editor.Part = null;

                    while (++index < count)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            Editor.Part = newPart;
                            Title.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Can't set this part, try the next one
                            Editor.Part = null;
                            Title.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index >= count)
                    {
                        // Restore current part if none can be set
                        Editor.Part = part;
                        Title.Text = _packageName + " - " + part.Type;
                    }

                    // Reset viewing parameters
                    UcEditor.ResetView(false);
                }
            }
        }

        private async void AppBar_OpenPackageButton_Click(object sender, RoutedEventArgs e)
        {
            Export_content();
        }
        
        async void Export_content()
        {
            //await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));

            List<StorageFile> files = new List<StorageFile>();
            //somehow even selecting a folder won't 
            var folderPicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads
            };
            folderPicker.FileTypeFilter.Add(".nebo");
            folderPicker.CommitButtonText = "select nebo files to convert";

            // create input and output directories
            await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("input", CreationCollisionOption.OpenIfExists);
            await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("output", CreationCollisionOption.OpenIfExists);

            var files_sel = await folderPicker.PickMultipleFilesAsync(); //should be file
            if (files_sel != null)
            {
                // List iink files inside the chosen folder (nebo files in disguise !)

                var items = files_sel; //the files selected
                foreach (var item in items)
                {
                    if (item.IsOfType(StorageItemTypes.File) && (item.Path.EndsWith(".iink") || item.Path.EndsWith(".nebo")))
                        files.Add(item);
                }
                if (files.Count == 0)
                    return;



                foreach (StorageFile current_file in files)
                {
                    ResetSelection();
                    try
                    {
                        // close current package
                        ClosePackage();

                        // Open package and select first part
                        // Can't open files that are outside the app folder
                        // so we HAVE to open it as a stream and copy things into the app folder

                        // copy the file into "input"
                        var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                        var filePath = System.IO.Path.Combine(localFolder.Path.ToString() + "//input//", current_file.Name);
                        // we do an input and output folder to be cleaner
                        try
                        {
                            var sample_file = File.Create(filePath);
                            await copy_stream(current_file, sample_file);
                        }
                        catch
                        {
                            Debug.WriteLine("File already copied, skipping");
                        }
                        // wait until the file is copied to file before opening it

                        var package = Editor.Engine.OpenPackage(filePath);

                        Debug.WriteLine("number of parts", package.PartCount.ToString());


                        //unzip the full package
                        await Task.Run(() =>
                        {
                            ZipFile.ExtractToDirectory(filePath, localFolder.Path.ToString() + "//" + "output" + "//" + current_file.Name.ToString(), true);
                        });

                        // we should iterate over the parts
                        for (int i = 0; i < package.PartCount; i++)
                        {
                            var part = package.GetPart(i);
                            Debug.WriteLine("names : ", current_file.Name.ToString(), part.Id.ToString()); //verify names

                            Editor.Part = part;
                            _packageName = part.Id.ToString();
                            Title.Text = _packageName + " - " + part.Type;
                            AppBar_Export_Click(current_file.Name, part.Id.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        ClosePackage();

                        var msgDialog = new MessageDialog(ex.ToString());
                        await msgDialog.ShowAsync();
                    }

                }

                // Reset viewing parameters
                UcEditor.ResetView(false);
                //display the tmp folder

                StorageFolder output = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path.ToString(), "output"));
                await Windows.System.Launcher.LaunchFolderAsync(output);


            }
        }

        private async Task copy_stream(StorageFile file_start, FileStream file_target)
        {
            var stream = await file_start.OpenStreamForReadAsync();
            stream.CopyTo(file_target);
            stream.Flush();
            stream.Close();
            file_target.Flush();
            file_target.Close();
        }

        private async void AppBar_Export_Click(string package_name, string part_id)
        {
            // init variables
            IContentSelection contentSelection = null;
            string filePath = null;

            var part = Editor.Part;
            if (part == null)
                return;
            using (var rootBlock = Editor.GetRootBlock())
            {
                contentSelection = rootBlock;

                if (contentSelection == null)
                {
                    Debug.WriteLine("empty selection/file, skipping");
                    return;
                }

                MimeType[] mimeTypes = new MimeType[] { MimeType.JIIX };

                var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                filePath = System.IO.Path.Combine(localFolder.Path.ToString() + "//output//" + package_name + "//" + "pages" + "//" + part_id + "//", part_id + ".jiix");
            }

            try
            {
                var imagePainter = new ImagePainter
                {
                    ImageLoader = UcEditor.ImageLoader
                };

                Editor.WaitForIdle();

                // check if the file already exist or not
                try
                {
                    Editor.Export_(contentSelection, filePath, imagePainter);
                }
                catch
                {
                    Debug.WriteLine("the file already exists, skipping");
                }
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }


        private void ClosePackage()
        {
            var part = Editor.Part;
            var package = part?.Package;
            Editor.Part = null;
            part?.Dispose();
            package?.Dispose();
            Title.Text = "";
        }

        private async void NewFile()
        {
            var cancelable = Editor.Part != null;
            var partType = Editor.Engine.SupportedPartTypes.ToList()[5];
            if (string.IsNullOrEmpty(partType))
                return;

            ResetSelection();

            try
            {
                // Save and close current package
                ClosePackage();

                // Create package and part
                var packageName = MakeUntitledFilename();
                var package = Editor.Engine.CreatePackage(packageName);
                var part = package.CreatePart(partType);
                Editor.Part = part;
                _packageName = System.IO.Path.GetFileName(packageName);
                Title.Text = _packageName + " - " + part.Type;
            }
            catch (Exception ex)
            {
                ClosePackage();

                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var tempFolder = Editor.Engine.Configuration.GetString("content-package.temp-folder");
            string fileName;
            string folderName;

            do
            {
                var baseName = "File" + (++_filenameIndex) + ".iink";
                fileName = System.IO.Path.Combine(localFolder, baseName);
                var tempName = baseName + "-file";
                folderName = System.IO.Path.Combine(tempFolder, tempName);
            }
            while (System.IO.File.Exists(fileName) || System.IO.File.Exists(folderName));

            return fileName;
        }

    }
}

