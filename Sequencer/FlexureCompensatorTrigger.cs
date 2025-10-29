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

using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.CustomControlLibrary;
using NINA.Equipment.Equipment.MyGuider;
using NINA.Equipment.Exceptions;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Equipment.Utility;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;


namespace Michelegz.NINA.FlexureCompensator.Sequencer.Trigger {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Sequence Trigger to the N.I.N.A. sequencer via the plugin interface
    /// For ease of use this class inherits the abstract SequenceTrigger which already handles most of the running logic, like logging, exception handling etc.
    /// A complete custom implementation by just implementing ISequenceTrigger is possible too
    /// The following MetaData can be set to drive the initial values
    /// --> Name - The name that will be displayed for the item
    /// --> Description - a brief summary of what the item is doing. It will be displayed as a tooltip on mouseover in the application
    /// --> Icon - a string to the key value of a Geometry inside N.I.N.A.'s geometry resources
    ///
    /// If the item has some preconditions that should be validated, it shall also extend the IValidatable interface and add the validation logic accordingly.
    /// </summary>
    [ExportMetadata("Name", "Flexure Compensator")]
    [ExportMetadata("Description", "A trigger to correct differential flexure")]
    [ExportMetadata("Icon", "FlexureCompensatorSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Telescope")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FlexureCompensatorTrigger : SequenceTrigger, IValidatable {

        protected IProfileService profileService;
        protected ICameraMediator cameraMediator;
        protected ITelescopeMediator telescopeMediator;
        protected IGuiderMediator guiderMediator;
        protected IImageSaveMediator imageSaveMediator;
        protected IApplicationStatusMediator applicationStatusMediator;
        protected IPlateSolverFactory plateSolverFactory;
        protected IImagingMediator imagingMediator;
        protected IFilterWheelMediator filterWheelMediator;
        protected IRotatorMediator rotatorMediator;
        protected IFocuserMediator focuserMediator;
        protected IWeatherDataMediator weatherMediator;
        private readonly IProgress<ApplicationStatus> progress;
        private bool closed = false;
        protected double shiftRateRA;
        protected double shiftRateDec;
        private int lastImageCount = 0;
        private int imageCount = 0;
        private Coordinates lastCoordinates;
        private DateTime lastDateTime;
        private CancellationTokenSource dummyCancellationSource = new CancellationTokenSource();
        private Boolean running;
        private BinningMode nextItemBinning, previousItemBinning;
        private double imageRmsArcsecs;
        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        private LockPosition lastLockPosition = null;
        private FilterInfo lastFilterInfo = null;
        private int lastFocusPosition = 0;
        private double exposureItemDuration = 0;
        private double maxExposureItemDuration = 0;
        private bool hasWarnedAboutPixelScale = false;
        private bool hasWarnedAboutNaN = false;

        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();


        [ImportingConstructor]
        public FlexureCompensatorTrigger(
            IProfileService profileService,
            ICameraMediator cameraMediator,
            IGuiderMediator guiderMediator,
            ITelescopeMediator telescopeMediator,
            IImageSaveMediator imageSaveMediator,
            IApplicationStatusMediator applicationStatusMediator,
            IPlateSolverFactory plateSolverFactory,
            IImagingMediator imagingMediator,
            IFilterWheelMediator filterWheelMediator,
            IRotatorMediator rotatorMediator,
            IFocuserMediator focuserMediator,
            IWeatherDataMediator weatherMediator) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;
            this.guiderMediator = guiderMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.rotatorMediator = rotatorMediator;
            this.focuserMediator = focuserMediator;
            this.weatherMediator = weatherMediator;
            this.progress = new Progress<ApplicationStatus>(ProgressStatusUpdate);
            this.running = false;
            this.PropertyChanged += PropertyChangeListener;
            plateSolvingExposureDuration = profileService.ActiveProfile.PlateSolveSettings.ExposureTime;
            afterExposures = 1;
            shiftRateDec = 0;
            shiftRateRA = 0;
            lastImageCount = imageCount;
        }

        public FlexureCompensatorTrigger(FlexureCompensatorTrigger copyMe) :
            this(copyMe.profileService,
                    copyMe.cameraMediator,
                    copyMe.guiderMediator,
                    copyMe.telescopeMediator,
                    copyMe.imageSaveMediator,
                    copyMe.applicationStatusMediator,
                    copyMe.plateSolverFactory,
                    copyMe.imagingMediator,
                    copyMe.filterWheelMediator,
                    copyMe.rotatorMediator,
                    copyMe.focuserMediator,
                    copyMe.weatherMediator
            ) {
            CopyMetaData(copyMe);
            plateSolvingExposureDuration = profileService.ActiveProfile.PlateSolveSettings.ExposureTime;
            shiftRateDec = 0;
            shiftRateRA = 0;
            lastImageCount = imageCount;
        }

        private void PropertyChangeListener(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "Status") {
                if (Status == SequenceEntityStatus.DISABLED) {
                    TurnOff();
                } else if (Status == SequenceEntityStatus.CREATED) {
                    shiftRateDec = 0;
                    shiftRateRA = 0;
                    TurnOn();
                }
            }
        }

        public double ShiftRateRA {
            get => shiftRateRA;
            set {
                shiftRateRA = value;
                RaisePropertyChanged();
            }
        }

        public double ShiftRateDec {
            get => shiftRateDec;
            set {
                shiftRateDec = value;
                RaisePropertyChanged();
            }
        }

        public double plateSolvingExposureDuration;

        private int afterExposures;

        [JsonProperty]
        public int AfterExposures {
            get => afterExposures;
            set {
                afterExposures = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public double PlateSolvingExposureDuration {
            get => plateSolvingExposureDuration;
            set {
                plateSolvingExposureDuration = value;
                RaisePropertyChanged();
            }
        }
        public int ProgressExposures {
            get => AfterExposures > 0 ? (imageCount - lastImageCount) % AfterExposures : 0;
        }

        public int ProgressExposuresPlusOne {
            get => ProgressExposures + 1;
        }

        private Task<IExposureData> Download(CancellationToken token, IProgress<ApplicationStatus> progress) {
            progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblDownloading"] });
            return cameraMediator.Download(token);
        }

        private void AddMetaData(ImageMetaData metaData, CaptureSequence sequence) {
            metaData.Image.Binning = sequence.Binning.Name;
            metaData.Image.ExposureTime = sequence.ExposureTime;
            metaData.Image.ImageType = sequence.ImageType;
            metaData.FromProfile(profileService.ActiveProfile);

            // Fill all available info from profile
            metaData.FromTelescopeInfo(telescopeMediator.GetInfo());
            metaData.FromFilterWheelInfo(filterWheelMediator.GetInfo());
            metaData.FromRotatorInfo(rotatorMediator.GetInfo());
            metaData.FromFocuserInfo(focuserMediator.GetInfo());
            metaData.FromWeatherDataInfo(weatherMediator.GetInfo());

            if (metaData.Target.Coordinates == null || double.IsNaN(metaData.Target.Coordinates.RA))
                metaData.Target.Coordinates = metaData.Telescope.Coordinates;
        }

        private Task<IExposureData> CaptureImage(
                CaptureSequence sequence,
                CancellationToken token
                ) {
            return Task.Run(async () => {
                try {
                    IExposureData data = null;
                    progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblWaitingForCamera"] });
                    await semaphoreSlim.WaitAsync(token);

                    try {
                        if (cameraMediator.GetInfo().Connected != true) {
                            Notification.ShowWarning(Loc.Instance["LblNoCameraConnected"]);
                            throw new CameraConnectionLostException();
                        }
                        await cameraMediator.Capture(sequence, token, progress);
                        data = await Download(token, progress);
                        token.ThrowIfCancellationRequested();
                        if (data == null) {
                            Logger.Error(new CameraDownloadFailedException(sequence));
                            Notification.ShowError(string.Format(Loc.Instance["LblCameraDownloadFailed"], sequence.ExposureTime, sequence.ImageType, sequence.Gain, sequence.FilterType?.Name ?? string.Empty));
                            return null;
                        }
                        AddMetaData(data.MetaData, sequence);
                    } catch (OperationCanceledException) {
                        cameraMediator.AbortExposure();
                        throw;
                    } catch (CameraExposureFailedException ex) {
                        Logger.Error(ex.Message);
                        Notification.ShowError(ex.Message);
                        throw;
                    } catch (CameraConnectionLostException ex) {
                        Logger.Error(ex);
                        Notification.ShowError(Loc.Instance["LblCameraConnectionLost"]);
                        throw;
                    } catch (Exception ex) {
                        Notification.ShowError(Loc.Instance["LblUnexpectedError"] + Environment.NewLine + ex.Message);
                        Logger.Error(ex);
                        cameraMediator.AbortExposure();
                        throw;
                    } finally {
                        progress.Report(new ApplicationStatus() { Status = "" });
                        semaphoreSlim.Release();
                    }
                    return data;
                } finally {
                    progress.Report(new ApplicationStatus() { Status = string.Empty });
                }
            });
        }


        private async Task<PlateSolveResult> Solve(ImageSolver solver, CaptureSequence seq, PlateSolveParameter parameter, IProgress<PlateSolveProgress> solveProgress, CancellationToken ct) {
            PlateSolveResult plateSolveResult;
            progress?.Report(new ApplicationStatus() { Status = Loc.Instance["LblCameraStateExposing"] });
            var capturedImage = await CaptureImage(seq, ct);

            if (capturedImage == null) {
                plateSolveResult = new PlateSolveResult() { Success = false }; ;
            } else {
                ct.ThrowIfCancellationRequested();
                plateSolveResult = await solver.Solve(await capturedImage.ToImageData(progress, ct), parameter, progress, ct);
            }
            return plateSolveResult;
        }


        public async Task<PlateSolveResult> SnapAndSolve(IProgress<ApplicationStatus> progress, CancellationToken token, BinningMode refBinning) {
            Logger.Debug("Taking a snapshot and solving");
            var plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            var imageSolver = new ImageSolver(plateSolver, null);
            PlateSolveParameter param = new PlateSolveParameter() {
                Binning = refBinning.X,
                Coordinates = telescopeMediator.GetCurrentPosition(),
                //DownSampleFactor = FlexureCompensatorMediator.Instance.Plugin.DownSampleFactor,
                FocalLength = profileService.ActiveProfile.TelescopeSettings.FocalLength,
                MaxObjects = profileService.ActiveProfile.PlateSolveSettings.MaxObjects,
                PixelSize = profileService.ActiveProfile.CameraSettings.PixelSize,
                Regions = profileService.ActiveProfile.PlateSolveSettings.Regions,
                SearchRadius = profileService.ActiveProfile.PlateSolveSettings.SearchRadius,
                BlindFailoverEnabled = true
            };
            double exposureTime = plateSolvingExposureDuration;
            int gain = profileService.ActiveProfile.PlateSolveSettings.Gain;
            if (gain == -1) gain = profileService.ActiveProfile.CameraSettings.Gain ?? 0;
            var seq = new CaptureSequence(
                exposureTime,
                CaptureSequence.ImageTypes.SNAPSHOT, null,
                new BinningMode(refBinning.X, refBinning.Y),
                1
            );
            seq.Gain = gain;
            Logger.Debug($"Starting exposure for solving - binning {seq.Binning} gain {seq.Gain} exposure {seq.ExposureTime}s");
            PlateSolveResult result = await Solve(imageSolver, seq, param, PlateSolveStatusVM.Progress, token);
            return result;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            Logger.Debug("Executing FlexureCompensator trigger");
            var thisLockPosition = guiderMediator.GetLockPosition();
            var thisDateTime = DateTime.UtcNow;
            maxExposureItemDuration = Math.Max(maxExposureItemDuration, exposureItemDuration);
            if (previousItemBinning != null) {
                Logger.Debug($"Last lock position   : {lastLockPosition}");
                Logger.Debug($"Current lock position: {thisLockPosition}");
                if ((lastLockPosition == null || IsLockPositionStillValid(thisLockPosition)) && IsFilterOK() && IsFocusOK()) {
                    if (ProgressExposures == 0) {
                        Logger.Debug("Just after exposure item - starting capture for plate solving");
                        var result = await SnapAndSolve(progress, token, previousItemBinning);
                        if (!result.Success) {
                            Logger.Debug("Plate solve failed");
                            Notification.ShowWarning(Loc.Instance["LblPlatesolveFailed"]);
                            lastCoordinates = null;
                            return;
                        }
                        Logger.Debug($"Field plate solved - coordinates {result.Coordinates}");
                        if (lastCoordinates != null) {
                            var driftRA = (result.Coordinates - lastCoordinates).RA.ArcSeconds;
                            var driftDec = (result.Coordinates - lastCoordinates).Dec.ArcSeconds;
                            var timeInterval = DateTime.UtcNow - lastDateTime;
                            Logger.Debug($"Drifted {driftRA:F3} arcsec in RA in {timeInterval.TotalMinutes:F3} minutes");
                            Logger.Debug($"Drifted {driftDec:F3} arcsec in Dec in {timeInterval.TotalMinutes:F3} minutes");
                            var exposureRatio = timeInterval.TotalSeconds / maxExposureItemDuration;
                            if ((result.Coordinates - lastCoordinates).Distance.ArcSeconds < exposureRatio * FlexureCompensatorMediator.Instance.Plugin.MinDriftLimit * result.Pixscale) {
                                Logger.Info($"Drifted less than {exposureRatio:F2} times the minimum drift ({FlexureCompensatorMediator.Instance.Plugin.MinDriftLimit} px), leaving shift rate unchanged");
                            } else if ((result.Coordinates - lastCoordinates).Distance.ArcSeconds > exposureRatio * FlexureCompensatorMediator.Instance.Plugin.MaxDriftLimit * result.Pixscale) {
                                Logger.Info($"Drifted more than {exposureRatio:F2} times the maximum drift ({FlexureCompensatorMediator.Instance.Plugin.MaxDriftLimit} px), leaving shift rate unchanged");
                            } else {
                                var driftRateRA = driftRA / timeInterval.TotalHours;
                                var driftRateDec = driftDec / timeInterval.TotalHours;
                                ShiftRateRA -= (driftRateRA * FlexureCompensatorMediator.Instance.Plugin.Aggressivity);
                                ShiftRateDec -= (driftRateDec * FlexureCompensatorMediator.Instance.Plugin.Aggressivity);
                                Logger.Info($"New shift rate: {shiftRateRA:F2} | {shiftRateDec:F2} arcsec/hr");
                                await guiderMediator.SetShiftRate(SiderealShiftTrackingRate.Create(shiftRateRA / 3600.0, shiftRateDec / 3600.0), dummyCancellationSource.Token);
                                thisLockPosition = guiderMediator.GetLockPosition();
                                thisDateTime = DateTime.UtcNow;
                                Logger.Debug($"Updated lock position: {thisLockPosition}");
                            }
                            maxExposureItemDuration = 0;
                        } else {
                            Logger.Debug("Image plate solved but no previous image at same location exists, leaving shift rate unchanged");
                        }
                        lastDateTime = thisDateTime;
                        lastCoordinates = result.Coordinates;
                        lastLockPosition = thisLockPosition;
                        if (filterWheelMediator.GetInfo().Connected) {
                            lastFilterInfo = filterWheelMediator.GetInfo().SelectedFilter;
                            Logger.Debug($"Filter: {lastFilterInfo.Name}");
                        }
                        if (focuserMediator.GetInfo().Connected) {
                            lastFocusPosition = focuserMediator.GetInfo().Position;
                            Logger.Debug($"Focus position: {lastFocusPosition}");
                        }
                        lastImageCount = imageCount;
                    }
                } else {
                    Logger.Debug("Just after exposure item - previous exposure no longer valid, a new reference image will be taken before next light sub");
                }
                previousItemBinning = null;
            }
            if (nextItemBinning != null) {
                Logger.Debug($"Last lock position   : {lastLockPosition}");
                Logger.Debug($"Current lock position: {thisLockPosition}");
                if ((lastLockPosition == null || !IsLockPositionStillValid(thisLockPosition)) || !IsFilterOK() || !IsFocusOK()) {
                    Logger.Debug("Just before exposure item - previous exposure not valid, starting capture for plate solving");
                    var result = await SnapAndSolve(progress, token, nextItemBinning);
                    if (!result.Success) {
                        Logger.Error("Plate solve failed");
                        Notification.ShowWarning(Loc.Instance["LblPlatesolveFailed"]);
                        lastCoordinates = null;
                        lastLockPosition = null;
                        return;
                    }
                    Logger.Debug($"Field plate solved - coordinates {result.Coordinates}");
                    lastCoordinates = result.Coordinates;
                    lastDateTime = thisDateTime;
                    lastLockPosition = thisLockPosition;
                    if (filterWheelMediator.GetInfo().Connected) {
                        lastFilterInfo = filterWheelMediator.GetInfo().SelectedFilter;
                        Logger.Debug($"Filter: {lastFilterInfo.Name}");
                    }
                    if (focuserMediator.GetInfo().Connected) {
                        lastFocusPosition = focuserMediator.GetInfo().Position;
                        Logger.Debug($"Focus position: {lastFocusPosition}");
                    }
                    lastImageCount = imageCount;
                    RaisePropertyChanged(nameof(ProgressExposures));
                    Logger.Debug($"Lock position: {lastLockPosition}");
                    maxExposureItemDuration = 0;
                } else {
                    Logger.Debug("Just before exposure item - last plate solved capture still valid");
                }
                nextItemBinning = null;
            }
        }

        private void PrintLockPosition(LockPosition pos) {
            Logger.Debug($"Lock position: x={pos.X} y={pos.Y}");
        }

        private bool IsLockPositionStillValid(LockPosition pos) {

            var guiderInfo = guiderMediator.GetInfo();
            if (guiderInfo == null || guiderInfo.PixelScale <= 0) {
                if (!hasWarnedAboutPixelScale) {
                    Logger.Warning("IsLockPositionStillValid: Guider pixel scale not available. This can happen if the guider is not configured correctly (eg. missing focal lenght). The plugin will continue to function, but cannot validate unexpected guide star movements.");
                    Notification.ShowWarning("Guider pixel scale not available. Safety checks are reduced. See logs for details.") ;
                    hasWarnedAboutPixelScale = true;
                }
                // Assume the position is valid to allow the sequence to continue.
                return true;
            }

            var dist = Math.Sqrt(Math.Pow(pos.X - lastLockPosition.X, 2) + Math.Pow(pos.Y - lastLockPosition.Y, 2));
            Logger.Debug($"Dist         = {dist:F5}");
            var time = pos.EventTime - lastLockPosition.EventTime;
            Logger.Debug($"Time         = {time.TotalHours:F5}");
            var expectedDist = Math.Sqrt((Math.Pow(ShiftRateRA * Math.Cos(telescopeMediator.GetCurrentPosition().Dec / 180.0 * Math.PI) / guiderMediator.GetInfo().PixelScale, 2) + Math.Pow(ShiftRateDec / guiderMediator.GetInfo().PixelScale, 2)) * Math.Pow(time.TotalHours, 2));
            Logger.Debug($"ExpectedDist = {expectedDist:F5}");

            if (double.IsNaN(expectedDist)) {
                if (!hasWarnedAboutNaN) {
                    Logger.Warning("IsLockPositionStillValid: Calculated expected distance is NaN. This may be caused by invalid data from telescope or guider drivers.");
                    Notification.ShowWarning("A calculation error occurred. Safety checks are reduced. See logs for details.");
                    hasWarnedAboutNaN = true;
                }
                return true;
            }

            bool result = Math.Abs(dist - expectedDist) <= Math.Max(0.25 * expectedDist, 0.02);
            if (result) {
                Logger.Debug("Distance from last lock point within 25% of the expected distance");
            } else {
                Logger.Debug("Distance from last lock point NOT within 25% of the expected distance");
            }
            return result;
        }

        private bool IsFilterOK() {
            if (filterWheelMediator.GetInfo().Connected == false) {
                return true;
            }
            var ignoreThis = FlexureCompensatorMediator.Instance.Plugin.IgnoreFilterChanges;
            if (ignoreThis) {
                Logger.Debug("Filter changes are to be ignored");
                return true;
            } else {
                if (lastFilterInfo == null) return false;
                var currentFilter = filterWheelMediator.GetInfo().SelectedFilter;
                if (filterWheelMediator.GetInfo().SelectedFilter.Position == lastFilterInfo.Position) {
                    Logger.Debug($"Filter has not changed, was {lastFilterInfo.Name} and is {currentFilter.Name}");
                    return true;
                } else {
                    Logger.Debug($"Filter has changed, was {lastFilterInfo.Name} and is {currentFilter.Name}");
                    return false;
                }
            }
        }

        private bool IsFocusOK() {
            if (focuserMediator.GetInfo().Connected == false) {
                return true;
            }
            var ignoreThis = FlexureCompensatorMediator.Instance.Plugin.IgnoreFocusChanges;
            if (ignoreThis) {
                Logger.Debug("Focus changes are to be ignored");
                return true;
            } else {
                var currentPosition = focuserMediator.GetInfo().Position;
                if (currentPosition == lastFocusPosition) {
                    Logger.Debug($"Focus has not changed, was {lastFocusPosition} and is {currentPosition}");
                    return true;
                } else {
                    Logger.Debug($"focus has changed, was {lastFocusPosition} and is {currentPosition}");
                    return false;
                }
            }
        }


        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            bool result = false;
            if (nextItem is IExposureItem && ((IExposureItem)nextItem).ImageType == CaptureSequence.ImageTypes.LIGHT) {
                nextItemBinning = ((IExposureItem)nextItem).Binning;
                exposureItemDuration = ((IExposureItem)nextItem).ExposureTime;
                result = true;
                RaisePropertyChanged(nameof(ProgressExposures));
            }
            return result;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            bool result = false;
            if (previousItem is IExposureItem && ((IExposureItem)previousItem).ImageType == CaptureSequence.ImageTypes.LIGHT) {
                previousItemBinning = ((IExposureItem)previousItem).Binning;
                exposureItemDuration = ((IExposureItem)previousItem).ExposureTime;
                result = true;
                imageCount++;
                RaisePropertyChanged(nameof(ProgressExposures));
            }
            return result;
        }

        protected void TurnOff() {
            Logger.Info("Turning trigger OFF");
            this.telescopeMediator.AfterMeridianFlip -= TelescopeMediator_AfterMeridianFlip;
            this.guiderMediator.AfterDither -= GuiderMediator_Dither;
            this.imageSaveMediator.BeforeImageSaved -= ImageSaveMediator_BeforeImageSaved;
            CancellationTokenSource ct = new CancellationTokenSource();
            guiderMediator.SetShiftRate(SiderealShiftTrackingRate.Create(0, 0), ct.Token);
            guiderMediator.StopShifting(ct.Token);
            lastCoordinates = null;
            lastLockPosition = null;
            lastDateTime = DateTime.MinValue;
            maxExposureItemDuration = 0;
            running = false;
        }

        protected void TurnOn() {
            Logger.Info("Turning trigger ON");
            this.telescopeMediator.AfterMeridianFlip += TelescopeMediator_AfterMeridianFlip;
            this.guiderMediator.AfterDither += GuiderMediator_Dither;
            this.imageSaveMediator.BeforeImageSaved += ImageSaveMediator_BeforeImageSaved;
            CancellationTokenSource ct = new CancellationTokenSource();
            guiderMediator.SetShiftRate(SiderealShiftTrackingRate.Create(ShiftRateRA / 3600.0, ShiftRateDec / 3600.0), ct.Token);
            lastCoordinates = null;
            lastLockPosition = null;
            lastDateTime = DateTime.MinValue;
            maxExposureItemDuration = 0;
            running = true;
        }

        public override void SequenceBlockInitialize() {
            //ShiftRateDec = 0;
            //ShiftRateRA = 0;
            lastCoordinates = null;
            lastLockPosition = null;
            lastDateTime = DateTime.MinValue;
            maxExposureItemDuration = 0;
            Logger.Debug("Entering sequence block - registering event listeners");
            CancellationTokenSource ct = new CancellationTokenSource();
            guiderMediator.SetShiftRate(SiderealShiftTrackingRate.Create(ShiftRateRA / 3600.0, ShiftRateDec / 3600.0), ct.Token);
            //guiderMediator.StartShifting(ct.Token);
            telescopeMediator.AfterMeridianFlip += TelescopeMediator_AfterMeridianFlip;
            this.guiderMediator.AfterDither += GuiderMediator_Dither;
            imageSaveMediator.BeforeImageSaved += ImageSaveMediator_BeforeImageSaved;
            running = true;
        }

        public override void SequenceBlockTeardown() {
            Logger.Debug("Exiting sequence block - un-registering event listeners and stopping shifting");
            this.telescopeMediator.AfterMeridianFlip -= TelescopeMediator_AfterMeridianFlip;
            this.guiderMediator.AfterDither -= GuiderMediator_Dither;
            this.imageSaveMediator.BeforeImageSaved -= ImageSaveMediator_BeforeImageSaved;
            CancellationTokenSource ct = new CancellationTokenSource();
            //ShiftRateRA = 0;
            //ShiftRateDec = 0;
            guiderMediator.SetShiftRate(SiderealShiftTrackingRate.Create(0, 0), ct.Token);
            guiderMediator.StopShifting(ct.Token);
            lastCoordinates = null;
            lastLockPosition = null;
            lastDateTime = DateTime.MinValue;
            maxExposureItemDuration = 0;
            running = false;
        }

        public override void AfterParentChanged() {
            if (Parent == null) {
                SequenceBlockTeardown();
            } else {
                if (Parent.Status == SequenceEntityStatus.RUNNING) {
                    SequenceBlockInitialize();
                }
            }
        }

        private Task GuiderMediator_Dither(object sender, EventArgs e) {
            Logger.Debug("Dihter event received");
            // strictly speaking, we don't need to do anything when dithering happens. But on the odd chance
            // that dithering somehow happens before the after-light-capture flexure correction trigger is executed,
            // we clear the lastCoordinates variable so that even if the after-light measurement happens after dithering,
            // it would not be considered for calculation of the drift rate
            //lastCoordinates = null;
            //lastImageCount = imageCount;
            return Task.CompletedTask;
        }

        private Task TelescopeMediator_AfterMeridianFlip(object sender, AfterMeridianFlipEventArgs e) {
            Logger.Debug("Meridian Flip event received - restarting from shift rate zero");
            lastCoordinates = null;
            lastLockPosition = null;
            ShiftRateDec = 0;
            ShiftRateRA = 0;
            maxExposureItemDuration = 0;
            lastImageCount = imageCount;
            RaisePropertyChanged(nameof(ProgressExposures));
            var ct = new CancellationTokenSource();
            guiderMediator.SetShiftRate(SiderealShiftTrackingRate.Create(0, 0), ct.Token);
            guiderMediator.StopShifting(ct.Token);
            ct.Dispose();
            return Task.CompletedTask;
        }

        private void ProgressStatusUpdate(ApplicationStatus status) {
            if (string.IsNullOrWhiteSpace(status.Source)) {
                status.Source = "Flexure Compensator";
            }
            applicationStatusMediator.StatusUpdate(status);
        }

        private async Task ImageSaveMediator_BeforeImageSaved(object sender, BeforeImageSavedEventArgs e) {
            if (!running) {
                return;
            }
            if (e.Image.MetaData.Image.ImageType != "LIGHT") {
                return;
            }
            imageRmsArcsecs = e.Image.MetaData.Image.RecordedRMS.Total * e.Image.MetaData.Image.RecordedRMS.Scale;
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        public bool Validate() {
            var i = new List<string>();
            var cameraInfo = cameraMediator.GetInfo();
            var telescopeInfo = telescopeMediator.GetInfo();
            var guiderInfo = guiderMediator.GetInfo();
            if (!cameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }
            if (!telescopeInfo.Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
            }
            if (!guiderInfo.Connected) {
                i.Add(Loc.Instance["LblGuiderNotConnected"]);
            } else if (!guiderInfo.CanSetShiftRate) {
                i.Add("Guider doesn't support shifting lock point");
            } else if (!guiderInfo.CanGetLockPosition) {
                i.Add("Guider doesn't support reading lock position");
            }
            Issues = i;
            return i.Count == 0;
        }

        public void Dispose() {
            if (!closed) {
                this.telescopeMediator.AfterMeridianFlip -= TelescopeMediator_AfterMeridianFlip;
                this.guiderMediator.AfterDither -= GuiderMediator_Dither;
                this.imageSaveMediator.BeforeImageSaved -= ImageSaveMediator_BeforeImageSaved;
                try {
                    dummyCancellationSource?.Cancel();
                } catch { }
                closed = true;
            }
        }

        public override object Clone() {
            return new FlexureCompensatorTrigger(this);
        }

        public override string ToString() {
            return $"Trigger: {nameof(FlexureCompensatorTrigger)}";
        }

    }
}