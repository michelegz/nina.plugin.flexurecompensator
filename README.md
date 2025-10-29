# Flexure Compensator Plugin for N.I.N.A.

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL%202.0-brightgreen.svg)](https://opensource.org/licenses/MPL-2.0)

This repository contains the source code for the "Flexure Compensator" plugin for the astrophotography software [N.I.N.A.](https://nighttime-imaging.eu/).

## Project Origin

This project is a continuation of a previously available "Flexure Correction" plugin. As the original project is no longer maintained, this repository was created with original author's approval/acknowledgment to:

1.  Ensure compatibility with current versions of N.I.N.A. (based on .NET 8).
2.  Provide ongoing development and maintenance.
3.  Offer a clean and updated codebase for future contributions.

The core logic for measuring and correcting guide drift remains faithful to the excellent original work.

## Manual Installation

To install the plugin manually, follow these steps:

1. **Download the latest release**  
   Go to the [Releases](https://github.com/michelegz/nina.plugin.flexurecompensator/releases) page and download the ZIP file for the latest version.

2. **Extract the zip to the N.I.N.A. plugins folder**  
   - Navigate to the N.I.N.A. plugins directory on your system:
     ```
     %LOCALAPPDATA%\NINA\Plugins\3.0.0\
     ```
   - Extract the zip file here

3. **Restart N.I.N.A.**  
   Launch N.I.N.A., and the plugin should now be available and loaded automatically.

4. **Verify installation**  
   - Open N.I.N.A. and check the Plugins section.  
   - You should see **Flexure Compensator** listed among the installed plugins.


## Development and Building

### Prerequisites

*   Visual Studio 2022
*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   Git installed and available in the system's PATH (required for automatic versioning)

### Building the Plugin

1.  Clone the repository:
    ```sh
    git clone https://github.com/michelegz/nina.plugin.flexurecompensator.git
    ```
2.  Open the `nina.plugin.flexurecompensator.sln` solution file in VS2022.
3.  Build the solution in either `Debug` or `Release` configuration.

The Post-Build script will automatically copy the compiled artifacts to the N.I.N.A. plugins directory (`%LOCALAPPDATA%\NINA\Plugins\...\`) for immediate testing.

## Automatic Versioning

This project uses an automatic versioning system managed by the `Directory.Build.targets` file. The assembly version is dynamically generated at compile time based on Git tags.

The version format is `Major.Minor.Patch.CommitsSinceTag`.

*   `Major.Minor.Patch` are parsed from the latest Git tag that follows the `vX.Y.Z` format (e.g., `v1.0.3`).
*   `CommitsSinceTag` is the number of commits made after that tag.

To create a new release, simply create and push a new tag pointing to the desired commit.

## License

This project is licensed under **Mozilla Public License v. 2.0**