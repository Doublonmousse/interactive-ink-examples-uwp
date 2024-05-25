## What is this fork

This fork is a small utility to export stroke data out of `.nebo` files to readable file formats. Install Visual Studio 2022, open the solution file, follow the instructions to get the developper certificate and run the `Demo` application.

A file picker window will open to select one or multiple `.nebo` files, then upon validating, all files will be converted to readable formats.

`.nebo` files are zipped folders that have the following structure when unzipped :


## Limits

Because of weird limitations of UWP around file access, all files selected are copied to the app local folder.


## Getting started

### Prerequisites
This getting started section has been tested with Visual Studio 2022 and supports [UWP Build 17763](https://docs.microsoft.com/en-us/windows/uwp/updates-and-versions/choose-a-uwp-version)

### Installation

1. Clone the examples repository  `git clone https://github.com/MyScript/interactive-ink-examples-uwp.git`

2. Claim a certificate to receive the free license to start develop your application by following the first steps of [Getting Started](https://developer.myscript.com/getting-started)

3. Copy this certificate to `GetStarted\MyCertificate.cs` and `Demo\MyCertificate.cs`

4. Open `MyScript.InteractiveInk.Examples.Uwp.sln` file. `Demo` project is the one that actually does the conversion. You can select which project to launch by right-clicking the project in the solution browser and selecting "Set as startup project".
