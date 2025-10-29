using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("00aa6286-a2f7-490e-bc08-7844af7175f5")] // change done

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin

// OBTAINED FROM GeneratedAssemblyInfo.cs
// Version format will be Major.Minor.Patch.CommitCount

//[assembly: AssemblyVersion("1.0.0.0")]
//[assembly: AssemblyFileVersion("1.0.0.0")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Flexure Compensator")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("A plugin that evaluates and corrects drift due to differential flexure.")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Michele Guzzini")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Flexure Compensator")]
[assembly: AssemblyCopyright("Copyright © 2025")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.1.2.9001")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/michelegz/nina.plugin.flexurecompensator")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://github.com/michelegz/nina.plugin.flexurecompensator")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Differential,Flexure,Drift,Correction")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/michelegz/nina.plugin.flexurecompensator/blob/main/CHANGELOG.md")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]



// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]

//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"

**Note:** This plugin is derived from the original *Flexure Correction*, which is no longer maintained.
*Flexure Compensator* continues its legacy to ensure support for current NINA versions.

**What is differential flexure?**

When taking guided exposures, the guiding system will do what’s necessary to keep the stars in the same positions in its field of view.
This works on the assumption that the field of view of the guiding system and of the imaging scope are in a fixed relationship with one 
another, so that guiding a star in one will result in round star images in the other.

Unfortunately, this is not always true. When using separate guide and imaging scopes, their mounting is usually sufficient to keep them 
*approximately* in a fixed relationship, but not always in a precise way. If the guide scope mounting system, for instance, flexes a bit
while an exposure is taken, the guiding system will “think” the stars are being tracked very precisely, while they are actually 
producing short trails on the imaging sensor attached to the main scope. This is *differential flexure*: a difference in the way the 
guide scope and the imaging scope respond to gravity and movement over time.

If you use a guide scope, you *will* have some degree of differential flexure. 

**How does this plugin help?**

This plugin aims at correcting and reducing the adverse effects of differential flexure. It does so by learning how the alignment of the 
two scopes changes over time, and then instructing the guider to introduce a gradual shift in the lock position to compensate for it. 

**Requirements**

A guider capable of shifting the lock point at a set rate, and of reading the lock point position, is needed -- PHD2 is such a guider.

**How to use the plugin**

To use this plugin, add the ""Flexure Compensator"" trigger into your sequence. The trigger will detect when a light sub exposure is 
about to start, and before that happens it will take a short exposure (the duration defaults to that used for plate solving, but it is 
adjustable in the trigger). Filter, binning, gain, and focus position will be the same used by the light sub that’s about to start. 
This image will be plate solved and compared with another similar exposure that will be taken later, to determine how much the imaging 
scope has drifted. The drift will be used to estimate the rate of drift of the imaging scope in arcseconds per hour, and the guider will 
be instructed to start gradually shifting its lock point with the same rate, but in the opposite direction, to reduce the overall drift 
in subsequent images.

**Configuration**

* The plugin can be configured to ignore drifts that are either too small (e.g. within the circle of uncertainty due to seeing), or too 
large (e.g. due to sudden ""mirror flop""). These limits can be set in the plugin options page, and expressed in units of pixels of the
imaging camera: for instance, the default setting is to ignore drifts that have affected the exposure by less than 0.3 pizels, or by more 
than 5 pixels.

* To reduce the number of measurement exposures taken, and thus improve the imaging efficiency, it is possible to configure the trigger to 
take a measurement every few light subs. The trigger will then try to consider all the drift that happened during the intervening light 
subs, but it will reset the drift estimation when certain events, that would cause changes in the astrometric position of the imaging camera 
frame, happened. 

 One such event is dithering: a dithering event will cause the imaged field to change, so that the drift estimation should be reset from 
scratch. Depending on the mechanical properties of the focuser, focusing or filter wheel rotation may also cause image shift -- if so, this 
should not be considered as flexure-induced drift as it happens only between frames, and the drift estimation should be reset. By default,
the trigger ignores focuser movements and filter changes, but it can be configured to reset the drift estimation upon either, or both, of 
these events. If you don't have a NINA-controlled focuser or a fiter wheel, these configuration settings are not relevant.

 >**IMPORTANT:** When configuring the trigger to evaluate drift over more than one sub, care must be taken so that the trigger has a chance to 
 > run through the set number of subs without events that would cause drift estimation reset. For example, if the flexure correction trigger is 
 > configured to take a measurement every 3 light subs, the dithering trigger should be configured to run every 3, 4, or more exposures, and 
 > not more frequently. If focusing causes the drift estimation to reset because it causes image shift, then the sequence should be set so that
 > focusing does not occur more frequently than, in the case of the example above, 3 light subs. When there is more than one event that can 
 > cause drift estimation to be reset, it may be quite complex or impossible to determine the maximum number of subs that the flexure correction
 > trigger can safely consider. In those cases, it is probably a better idea to set that number to 1, and accept a certain reduction in imaging 
 > efficiency as the price to pay for better image quality.


* An ""aggressiveness"" parameter can also be set, to determine how much of the measured drift in each exposure will be considered 
to calculate the new shift rate.")]