#region copyright
/*
    Copyright © 2025 Michele Guzzini <michele.gz@gmail.com>

    From an idea by Francesco Meschia <francesco.meschia@gmail.com>

    This source code incorporates portions based on the N.I.N.A. Plugin Template 
    by Stefan Berg, released under The Unlicense (public domain).

    The combined work is distributed under the Mozilla Public License, version 2.0.

    You can obtain a copy of the MPL 2.0 at: http://mozilla.org/MPL/2.0/
*/
#endregion

using Michelegz.NINA.FlexureCompensator.Properties;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Image.ImageData;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Settings = Michelegz.NINA.FlexureCompensator.Properties.Settings;

namespace Michelegz.NINA.FlexureCompensator {

    // Mediator between plugin class and trigger class
    public class FlexureCompensatorMediator {
        private FlexureCompensatorMediator() { }

        private static readonly Lazy<FlexureCompensatorMediator> lazy = new Lazy<FlexureCompensatorMediator>(() => new FlexureCompensatorMediator());

        public static FlexureCompensatorMediator Instance { get => lazy.Value; }
        public void RegisterPlugin(FlexureCompensatorPlugin plugin) {
            this.Plugin = plugin;
        }

        public FlexureCompensatorPlugin Plugin { get; private set; }
    }


    #region DefaultPluginTemplateDefinitions

    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    /// 
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "FlexureCompensator_Options" where FlexureCompensator corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class FlexureCompensatorPlugin : PluginBase, INotifyPropertyChanged {
        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;

        [ImportingConstructor]
        public FlexureCompensatorPlugin(IProfileService profileService, IOptionsVM options) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            FlexureCompensatorMediator.Instance.RegisterPlugin(this);
            // This helper class can be used to store plugin settings that are dependent on the current profile
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;

        }

        /*
        //kept from template just as reminder for possibile future implementations
        public override Task Teardown() {
            // Make sure to unregister an event when the object is no longer in use. Otherwise garbage collection will be prevented.
            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }
        */

       
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    
    #endregion

    #region FlexureCompensator


        public double Aggressivity {
            get {
                var aggressivity = pluginSettings.GetValueDouble(nameof(Aggressivity), Double.NaN);
                if (Double.IsNaN(aggressivity)) {
                    aggressivity = Properties.Settings.Default.Aggressivity;
                    pluginSettings.SetValueDouble(nameof(Aggressivity), aggressivity);
                }
                return aggressivity;
            }
            set {
                if (Aggressivity != value) {
                    pluginSettings.SetValueDouble(nameof(Aggressivity), value);
                    RaisePropertyChanged();
                }
            }
        }

        public double MinDriftLimit {
            get {
                var driftLimit = pluginSettings.GetValueDouble(nameof(MinDriftLimit), Double.NaN);
                if (Double.IsNaN(driftLimit)) {
                    driftLimit = Properties.Settings.Default.MinDriftLimit;
                    pluginSettings.SetValueDouble(nameof(MinDriftLimit), driftLimit);
                }
                return driftLimit;
            }
            set {
                if (MinDriftLimit != value) {
                    pluginSettings.SetValueDouble(nameof(MinDriftLimit), value);
                    RaisePropertyChanged();
                }
            }
        }

        public double MaxDriftLimit {
            get {
                var driftLimit = pluginSettings.GetValueDouble(nameof(MaxDriftLimit), Double.NaN);
                if (Double.IsNaN(driftLimit)) {
                    driftLimit = Properties.Settings.Default.MaxDriftLimit;
                    pluginSettings.SetValueDouble(nameof(MaxDriftLimit), driftLimit);
                }
                return driftLimit;
            }
            set {
                if (MaxDriftLimit != value) {
                    pluginSettings.SetValueDouble(nameof(MaxDriftLimit), value);
                    RaisePropertyChanged();
                }
            }
        }

        public double MinDuration {
            get {
                var duration = pluginSettings.GetValueDouble(nameof(MinDuration), Double.NaN);
                if (Double.IsNaN(duration)) {
                    duration = Properties.Settings.Default.MinDuration;
                    pluginSettings.SetValueDouble(nameof(MinDuration), duration);
                }
                return duration;
            }
            set {
                if (MinDuration != value) {
                    pluginSettings.SetValueDouble(nameof(MinDuration), value);
                    RaisePropertyChanged();
                }
            }
        }

        public bool IgnoreFilterChanges {
            get {
                bool ignore;
                if (!profileService.ActiveProfile.PluginSettings.TryGetValue(Guid.Parse(this.Identifier), nameof(IgnoreFilterChanges), out ignore)) {
                    ignore = Properties.Settings.Default.IgnoreFilterChanges;
                    pluginSettings.SetValueBoolean(nameof(IgnoreFilterChanges), ignore);
                }
                return ignore;
            }
            set {
                if (IgnoreFilterChanges != value) {
                    pluginSettings.SetValueBoolean(nameof(IgnoreFilterChanges), value);
                    RaisePropertyChanged();
                }
            }
        }

        public bool IgnoreFocusChanges {
            get {
                bool ignore;
                if (!profileService.ActiveProfile.PluginSettings.TryGetValue(Guid.Parse(this.Identifier), nameof(IgnoreFocusChanges), out ignore)) {
                    ignore = Properties.Settings.Default.IgnoreFocusChanges;
                    pluginSettings.SetValueBoolean(nameof(IgnoreFocusChanges), ignore);
                }
                return ignore;
            }
            set {
                if (IgnoreFocusChanges != value) {
                    pluginSettings.SetValueBoolean(nameof(IgnoreFocusChanges), value);
                    RaisePropertyChanged();
                }
            }
        }


        #endregion

    }
}

