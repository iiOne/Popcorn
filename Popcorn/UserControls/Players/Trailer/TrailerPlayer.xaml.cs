﻿using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using Popcorn.Messaging;
using xZune.Vlc.Interop.Media;
using Popcorn.ViewModel.Players.Trailer;

namespace Popcorn.UserControls.Players.Trailer
{
    /// <summary>
    /// Interaction logic for TrailerPlayer.xaml
    /// </summary>
    public partial class TrailerPlayer : IDisposable
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the TrailerPlayer class.
        /// </summary>
        public TrailerPlayer()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region Method -> Onloaded

        /// <summary>
        /// Subscribe to events and play the trailer when control has been loaded
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">EventArgs</param>
        protected override void OnLoaded(object sender, EventArgs e)
        {
            if (Player.State == MediaState.Paused)
            {
                PlayMedia();
            }
            else
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += (s1, e1) => Dispose();
                }

                var vm = DataContext as TrailerPlayerViewModel;
                if (vm == null)
                {
                    return;
                }

                if (vm.MediaUri == null)
                    return;

                // start the timer used to report time on MediaPlayerSliderProgress
                MediaPlayerTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
                MediaPlayerTimer.Tick += MediaPlayerTimerTick;
                MediaPlayerTimer.Start();

                // start the activity timer used to manage visibility of the PlayerStatusBar
                ActivityTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(3)};
                ActivityTimer.Tick += OnInactivity;
                ActivityTimer.Start();

                InputManager.Current.PreProcessInput += OnActivity;

                vm.StoppedPlayingMedia += OnStoppedPlayingMedia;
                Player.VlcMediaPlayer.EndReached += MediaPlayerEndReached;

                Player.LoadMedia(vm.MediaUri);
                PlayMedia();
            }
        }

        #endregion

        #region Method -> MediaPlayerEndReached

        /// <summary>
        /// When a trailer has been fully played, send the StopPlayingTrailerMessage message
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">EventArgs</param>
        protected override void MediaPlayerEndReached(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                var vm = DataContext as TrailerPlayerViewModel;
                if (vm == null)
                {
                    return;
                }

                if (vm.IsInFullScreenMode)
                {
                    vm.IsInFullScreenMode = !vm.IsInFullScreenMode;
                    Messenger.Default.Send(new ChangeScreenModeMessage(vm.IsInFullScreenMode));
                }

                Messenger.Default.Send(new StopPlayingTrailerMessage());
            });
        }

        #endregion

        #region Method -> PlayMedia

        /// <summary>
        /// Play the trailer
        /// </summary>
        protected override void PlayMedia()
        {
            Player.Play();
            MediaPlayerIsPlaying = true;

            MediaPlayerStatusBarItemPlay.Visibility = Visibility.Collapsed;
            MediaPlayerStatusBarItemPause.Visibility = Visibility.Visible;
        }

        #endregion

        #region Method -> PauseMedia

        /// <summary>
        /// Pause the trailer
        /// </summary>
        protected override void PauseMedia()
        {
            Player.PauseOrResume();
            MediaPlayerIsPlaying = false;

            MediaPlayerStatusBarItemPlay.Visibility = Visibility.Visible;
            MediaPlayerStatusBarItemPause.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Method -> OnStoppedPlayingMedia

        /// <summary>
        /// When media has finished playing, stop player and dispose control
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">EventArgs</param>
        private void OnStoppedPlayingMedia(object sender, EventArgs e)
        {
            Dispose();
        }

        #endregion

        #region Method -> MediaPlayerTimerTick

        /// <summary>
        /// Report the playing progress on the timeline
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">EventArgs</param>
        protected override void MediaPlayerTimerTick(object sender, EventArgs e)
        {
            if ((Player != null) && (!UserIsDraggingMediaPlayerSlider))
            {
                MediaPlayerSliderProgress.Minimum = 0;
                MediaPlayerSliderProgress.Maximum = Player.Length.TotalSeconds;
                MediaPlayerSliderProgress.Value = Player.Time.TotalSeconds;
            }
        }

        #endregion

        #region Method -> MediaPlayerPlayCanExecute

        /// <summary>
        /// Each time the CanExecute play command change, update the visibility of Play/Pause buttons in the player
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">CanExecuteRoutedEventArgs</param>
        protected override void MediaPlayerPlayCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (MediaPlayerStatusBarItemPlay != null && MediaPlayerStatusBarItemPause != null)
            {
                e.CanExecute = Player != null;
                if (MediaPlayerIsPlaying)
                {
                    MediaPlayerStatusBarItemPlay.Visibility = Visibility.Collapsed;
                    MediaPlayerStatusBarItemPause.Visibility = Visibility.Visible;
                }
                else
                {
                    MediaPlayerStatusBarItemPlay.Visibility = Visibility.Visible;
                    MediaPlayerStatusBarItemPause.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Method -> MediaPlayerPauseCanExecute

        /// <summary>
        /// Each time the CanExecute play command change, update the visibility of Play/Pause buttons in the media player
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">CanExecuteRoutedEventArgs</param>
        protected override void MediaPlayerPauseCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (MediaPlayerStatusBarItemPlay != null && MediaPlayerStatusBarItemPause != null)
            {
                e.CanExecute = MediaPlayerIsPlaying;
                if (MediaPlayerIsPlaying)
                {
                    MediaPlayerStatusBarItemPlay.Visibility = Visibility.Collapsed;
                    MediaPlayerStatusBarItemPause.Visibility = Visibility.Visible;
                }
                else
                {
                    MediaPlayerStatusBarItemPlay.Visibility = Visibility.Visible;
                    MediaPlayerStatusBarItemPause.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Method -> MediaSliderProgressDragCompleted

        /// <summary>
        /// Report when user has finished dragging the media player progress
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">DragCompletedEventArgs</param>
        protected override void MediaSliderProgressDragCompleted(object sender, DragCompletedEventArgs e)
        {
            UserIsDraggingMediaPlayerSlider = false;
            Player.Time = TimeSpan.FromSeconds(MediaPlayerSliderProgress.Value);
        }

        #endregion

        #region Method -> MovieSliderProgress_ValueChanged

        /// <summary>
        /// Report runtime when movie player progress changed
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">RoutedPropertyChangedEventArgs</param>
        protected override void MediaSliderProgressValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MoviePlayerTextProgressStatus.Text =
                TimeSpan.FromSeconds(MediaPlayerSliderProgress.Value)
                    .ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture) + " / " +
                TimeSpan.FromSeconds(Player.Length.TotalSeconds)
                    .ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture);
        }

        #endregion

        #region Method -> OnInactivity

        /// <summary>
        /// Hide the PlayerStatusBar on mouse inactivity 
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">EventArgs</param>
        private void OnInactivity(object sender, EventArgs e)
        {
            // remember mouse position
            InactiveMousePosition = Mouse.GetPosition(Container);

            if (!PlayerStatusBar.Opacity.Equals(1.0))
                return;

            var opacityAnimation = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)),
                    new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new PowerEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    })
                }
            };

            PlayerStatusBar.BeginAnimation(OpacityProperty, opacityAnimation);
            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
            {
                await Task.Delay(500);
                PlayerStatusBar.Visibility = Visibility.Collapsed;
            });
        }

        #endregion

        #region Method -> OnActivity

        /// <summary>
        /// Show the PlayerStatusBar on mouse activity 
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">EventArgs</param>
        private void OnActivity(object sender, PreProcessInputEventArgs e)
        {
            var inputEventArgs = e.StagingItem.Input;
            if (inputEventArgs is MouseEventArgs || inputEventArgs is KeyboardEventArgs)
            {
                if (e.StagingItem.Input is MouseEventArgs)
                {
                    var mouseEventArgs = (MouseEventArgs) e.StagingItem.Input;

                    // no button is pressed and the position is still the same as the application became inactive
                    if (mouseEventArgs.LeftButton == MouseButtonState.Released &&
                        mouseEventArgs.RightButton == MouseButtonState.Released &&
                        mouseEventArgs.MiddleButton == MouseButtonState.Released &&
                        mouseEventArgs.XButton1 == MouseButtonState.Released &&
                        mouseEventArgs.XButton2 == MouseButtonState.Released &&
                        InactiveMousePosition == mouseEventArgs.GetPosition(Container))
                        return;
                }

                if (!PlayerStatusBar.Opacity.Equals(0.0))
                    return;

                var opacityAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    KeyFrames = new DoubleKeyFrameCollection
                    {
                        new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(0)),
                        new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0), new PowerEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        })
                    }
                };

                PlayerStatusBar.BeginAnimation(OpacityProperty, opacityAnimation);
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    PlayerStatusBar.Visibility = Visibility.Visible;
                });
            }
        }

        #endregion

        #region Method -> ChangeMediaVolume

        /// <summary>
        /// Change the media's volume
        /// </summary>
        /// <param name="newValue">New volume value</param>
        protected override void ChangeMediaVolume(int newValue)
        {
            Player.Volume = newValue;
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Free resources
        /// </summary>
        public void Dispose()
        {
            if (Disposed)
                return;

            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
            {
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                MediaPlayerTimer.Tick -= MediaPlayerTimerTick;
                MediaPlayerTimer.Stop();

                ActivityTimer.Tick -= OnInactivity;
                ActivityTimer.Stop();

                InputManager.Current.PreProcessInput -= OnActivity;

                Player.VlcMediaPlayer.EndReached -= MediaPlayerEndReached;
                MediaPlayerIsPlaying = false;

                await Player.StopAsync();
                Player.Dispose();

                var vm = DataContext as TrailerPlayerViewModel;
                if (vm != null)
                {
                    vm.StoppedPlayingMedia -= OnStoppedPlayingMedia;
                }

                Disposed = true;

                GC.SuppressFinalize(this);
            });
        }

        #endregion
    }
}