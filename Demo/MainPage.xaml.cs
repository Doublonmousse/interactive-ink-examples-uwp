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
            //await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));

            List<StorageFile> files = new List<StorageFile>();
            //somehow even selecting a folder won't 
            var folderPicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads
            };
            folderPicker.FileTypeFilter.Add("*");

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

                var firstfile = files[0];

                ResetSelection();

                try
                {
                    // close current package
                    ClosePackage();

                    // Open package and select first part
                    // Can't open files that are outside the app folder
                    // so we HAVE to open it as a stream and copy things into the app folder

                    //Debug.WriteLine(Windows.Storage.ApplicationData.Current.LocalFolder.Path + "\\" +  fileName);


                    // get the file
                    var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    var filePath = System.IO.Path.Combine(localFolder.Path.ToString(), firstfile.Name);

                    var sample_file = File.Create(filePath);

                    // need to force the copy to happen SOMEHOW
                    await copy_stream(firstfile, sample_file);
                    // wait until the file is ready

                    var filePath2 = System.IO.Path.Combine(localFolder.Path.ToString(), firstfile.Name.ToString());
                    var package = Editor.Engine.OpenPackage(filePath2);

                    Debug.WriteLine("number of parts", package.PartCount.ToString());
                    // we should iterate over the parts [TODO]
                    var part = package.GetPart(0);
                    Debug.WriteLine(part.Metadata.ToString());

                    // we should get the name of the part, or at least the ID
                    Debug.WriteLine(part.Id.ToString());
                    Editor.Part = part;
                    _packageName = part.Id.ToString();
                    Title.Text = _packageName + " - " + part.Type;

                    //unzip
                    await Task.Run(() =>
                        {
                            ZipFile.ExtractToDirectory(filePath2,localFolder.Path.ToString() + "//" + "unzip");
                        });
                    
                }
                catch (Exception ex)
                {
                    ClosePackage();

                    var msgDialog = new MessageDialog(ex.ToString());
                    await msgDialog.ShowAsync();
                }

                // Reset viewing parameters
                UcEditor.ResetView(false);

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

        private async void AppBar_Export_Click(object sender, RoutedEventArgs e)
        {
            var part = Editor.Part;
            if (part == null)
                return;
            using (var rootBlock = Editor.GetRootBlock())
            {
                IContentSelection contentSelection = rootBlock;

                if (contentSelection == null)
                {
                    Debug.WriteLine("empty selection");
                    return;
                }

                MimeType[] mimeTypes = new MimeType[] { MimeType.JIIX };

                // Show export dialog
                var fileName = await ChooseExportFilename(mimeTypes);

                string filePath = null;

                if (!string.IsNullOrEmpty(fileName))
                {
                    var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);
                }

                try
                {
                    var imagePainter = new ImagePainter
                    {
                        ImageLoader = UcEditor.ImageLoader
                    };

                    Editor.WaitForIdle();
                    Editor.Export_(contentSelection, filePath, imagePainter);


                    //access/creating of the file (only for the export)
                    var filenew = await Windows.Storage.DownloadsFolder.CreateFileAsync(fileName);

                    //read back the exported file to get data out
                    var buffer = await FileIO.ReadBufferAsync(await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath));

                    //recreate the file stream
                    await Windows.Storage.FileIO.WriteBufferAsync(filenew, buffer);
                }
                catch (Exception ex)
                {
                    var msgDialog = new MessageDialog(ex.ToString());
                    await msgDialog.ShowAsync();
                }
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
        private async System.Threading.Tasks.Task<string> ChooseExportFilename(MimeType[] mimeTypes)
        {
            var nameTextBlock = new TextBlock
            {
                Text = "Enter Export File Name",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Width = 300
            };

            var nameTextBox = new TextBox
            {
                Text = "",
                AcceptsReturn = false,
                MaxLength = 1024 * 1024,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 10),
                Width = 300
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            panel.Children.Add(nameTextBlock);
            panel.Children.Add(nameTextBox);


            var dialog = new ContentDialog
            {
                Title = "Export",
                Content = panel,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var fileName = nameTextBox.Text;
                var extensions = MimeTypeF.GetFileExtensions(mimeTypes[0]).Split(',');

                int ext;
                for (ext = 0; ext < extensions.Count(); ++ext)
                {
                    if (fileName.EndsWith(extensions[ext], StringComparison.OrdinalIgnoreCase))
                        break;
                }

                if (ext >= extensions.Count())
                    fileName += extensions[0];

                return fileName;
            }

            return null;
        }
    }
}
