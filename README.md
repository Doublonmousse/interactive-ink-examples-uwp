## What is this fork

This fork is a small utility to export stroke data out of `.nebo` files to readable file formats. Install Visual Studio 2022, open the solution file, follow the instructions to get the developper certificate and run the `Demo` application.

A file picker window will open to select one or multiple `.nebo` files, then upon validating, all files will be converted to readable formats.

`.nebo` files are zipped folders that have the following structure when unzipped :
```
.                        // parent folder (name myfile when extracting myfile.nebo)
├── index.bdom           // binary file
├── meta.json            // json with some general information about the .nebo file
├── objects              
│   └── 8b09c36d-a67c-4e5b-81e8-0caf2cc834a9.png //images are saved here
├── pages                // folder containing all pages (for example when exporting a notebook containing multiple pages)
│   └── kuocdnca         // the id of the page
│       ├── ink.bink     // binary file containing the stroke data (not easily readable)
│       ├── meta.json    // info about the page (including the name)
│       ├── page.bdom    // binary file (although a lot of text is readable upon opening the .hex file)
│       └── style.css    // css file
└── rel.json
```
The big issue is that the interesting data (stroke data) is serialized in the `ink.bink` file that's not easily readable even though a portable format from myscript themselves exist (the `.jiix` format) and the engine that runs in nebo itself supports the conversion/export to this format.

Short of myscript providing the export option in their app (at least the windows app doesn't have this export option), this repository uses the SDK to extract all `.nebo` zipped files into their unzipped variant with the addition of a `page_id.jiix` file that's a readable json containing all stroke info.

## Limits

- Because of weird limitations of UWP around file access, all files selected are copied to the app local folder, that's opened at the end of the conversion.
Also, for now, these copied files stay around..

Location of copied files
```
C:\Users\USERNAME\AppData\Local\Packages\MyScript.InteractiveInk.Demo.Uwp_ ....\LocalState\input\
```
Location of output files
```
C:\Users\USERNAME\AppData\Local\Packages\MyScript.InteractiveInk.Demo.Uwp_ ....\LocalState\output\
```

- You need to setup a myscript dev account and get the certificate file to run this example (needs an internet connection for the certificate to register)

## Getting started

### Prerequisites
This getting started section has been tested with Visual Studio 2022 and supports [UWP Build 17763](https://docs.microsoft.com/en-us/windows/uwp/updates-and-versions/choose-a-uwp-version)

### Installation

1. Clone the examples repository  `git clone https://github.com/MyScript/interactive-ink-examples-uwp.git`

2. Claim a certificate to receive the free license to start develop your application by following the first steps of [Getting Started](https://developer.myscript.com/getting-started)

3. Copy this certificate to `GetStarted\MyCertificate.cs` and `Demo\MyCertificate.cs`

4. Open `MyScript.InteractiveInk.Examples.Uwp.sln` file. `Demo` project is the one that actually does the conversion. You can select which project to launch by right-clicking the project in the solution browser and selecting "Set as startup project".
