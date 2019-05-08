using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sanford.Multimedia.Midi.UI;
using Sanford.Multimedia.Midi;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace WPFMidiBand {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        #region atrributes
        private bool scrolling = false;
        private bool closing = false;
        private int outDeviceID = 0;
        private const int LowNoteID = 21;
        private const int HighNoteID = 109;
        private OutputDeviceDialog outDialog = new OutputDeviceDialog();
        private OutputDevice outDevice;
        private Sequence sequence1 = new Sequence();
        private Sequencer sequencer1 = new Sequencer();
        System.Windows.Forms.OpenFileDialog openMidiFileDialog = new System.Windows.Forms.OpenFileDialog();
        List<MessageDto> messageList = new List<MessageDto>();
        Dictionary<int, int> dicChannel = new Dictionary<int, int>();
        private bool dragStarted = false;

        string fileName = "";

        DispatcherTimer timer1 = new DispatcherTimer();

        #endregion atrributes

        #region ctor
        public MainWindow() {
            InitializeComponent();

            InitializeSequencer();

            var path = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            openMidiFileDialog.InitialDirectory = Path.Combine(path, "Midis");
            openMidiFileDialog.Multiselect = true;

            timer1.Interval = new TimeSpan(0, 0, 0, 1);
            timer1.Tick += new EventHandler(timer1_Tick);
            clock = BlackMagic.GetClock(sequencer1);
            taskBarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            listPanel.ItemsSource = playList;
        }
        #endregion ctor

        #region methods
        private void InitializeSequencer() {
            sequencer1.Stop();
            playing = false;

            outDevice = new OutputDevice(outDeviceID);
            this.sequence1.Format = 1;
            this.sequencer1.Position = 0;
            this.sequencer1.Sequence = this.sequence1;
            this.sequencer1.PlayingCompleted += new System.EventHandler(this.HandlePlayingCompleted);
            this.sequencer1.ChannelMessagePlayed += new System.EventHandler<Sanford.Multimedia.Midi.ChannelMessageEventArgs>(this.HandleChannelMessagePlayed);
            this.sequencer1.Stopped += new System.EventHandler<Sanford.Multimedia.Midi.StoppedEventArgs>(this.HandleStopped);
            this.sequencer1.SysExMessagePlayed += new System.EventHandler<Sanford.Multimedia.Midi.SysExMessageEventArgs>(this.HandleSysExMessagePlayed);
            this.sequencer1.Chased += new System.EventHandler<Sanford.Multimedia.Midi.ChasedEventArgs>(this.HandleChased);
            this.sequence1.LoadProgressChanged += HandleLoadProgressChanged;
            this.sequence1.LoadCompleted += HandleLoadCompleted;
        }

        private void ClearInstruments() {
            dicChannel.Clear();
            pianoControl1.Clear();
            guitarControl1.Clear();
            bassControl1.Clear();
        }
        #endregion methods

        #region events
        protected void OnLoad(EventArgs e) {
            if (OutputDevice.DeviceCount == 0) {
                System.Windows.MessageBox.Show("No MIDI output devices available.", "Error!",
                    MessageBoxButton.OK, MessageBoxImage.Stop);

                Close();
            } else {
                try {
                    outDevice = new OutputDevice(outDeviceID);

                    sequence1.LoadProgressChanged += HandleLoadProgressChanged;
                    sequence1.LoadCompleted += HandleLoadCompleted;
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Error!",
                        MessageBoxButton.OK, MessageBoxImage.Stop);

                    Close();
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e) {
            closing = true;

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e) {
            sequence1.Dispose();

            if (outDevice != null) {
                outDevice.Dispose();
            }

            outDialog.Dispose();

            base.OnClosed(e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
            Close();
        }

        private void HandleLoadProgressChanged(object sender, ProgressChangedEventArgs e) {
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                    new Action(
                        delegate () {
                            needleRotation.Angle = (e.ProgressPercentage / 100.0) * 360.0;
                        }
                    )
            );
        }

        private void HandleLoadCompleted(object sender, AsyncCompletedEventArgs e) {
            Opening = false;
            taskBarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            TitleTextBlock.Text = this.Title = $"WPF Midi Band - {currentFileName}";
            ClearInstruments();

            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                delegate () {
                    var sbClockClose = (Storyboard)FindResource("sbClockClose");
                    sbClockClose.Begin();
                    this.Cursor = Cursors.Arrow;
                    sequencer1.Position = 0;
                    slider1.Value = 0;
                    slider1.Maximum = sequence1.GetLength();
                }
                )
            );

            MyLoaded = true;
            clock.Tempo = DefaultTempo; // important to set DefaultTempo back before play
            CalculateTime();
            if (AutoStart) {
                PlayStateChange();
                AutoStart = false;
            }
        }


        private void HandleChannelMessagePlayed(object sender, ChannelMessageEventArgs e) {
            if (closing) {
                return;
            }

            outDevice.Send(e.Message);

            if (e.Message.Command == ChannelCommand.ProgramChange) {
                if (!dicChannel.ContainsKey(e.Message.MidiChannel)) {
                    dicChannel.Add(e.Message.MidiChannel, e.Message.Data1);
                }
            }

            if (e.Message.MidiChannel == 9) // Channel 9 is reserved for drums
            {
                this.Dispatcher.BeginInvoke(
                  DispatcherPriority.Normal,
                    new Action(
                        delegate () {
                            drumControl1.Send(e.Message);
                        }
                    ));
            } else if (dicChannel.ContainsKey(e.Message.MidiChannel)) {
                switch (dicChannel[e.Message.MidiChannel]) {
                    case (int)MIDIInstrument.AcousticGrandPiano://1
                    case (int)MIDIInstrument.BrightAcousticPiano://2
                    case (int)MIDIInstrument.ElectricGrandPiano://3
                    case (int)MIDIInstrument.HonkytonkPiano://4
                    case (int)MIDIInstrument.ElectricPiano1://5
                    case (int)MIDIInstrument.ElectricPiano2://6
                    case (int)MIDIInstrument.Harpsichord://7
                    case (int)MIDIInstrument.Clavinet://8
                        pianoControl1.Send(e.Message);
                        break;
                    case (int)MIDIInstrument.AcousticGuitarnylon://25
                    case (int)MIDIInstrument.AcousticGuitarsteel://26
                    case (int)MIDIInstrument.ElectricGuitarjazz://27
                    case (int)MIDIInstrument.ElectricGuitarclean://28
                    case (int)MIDIInstrument.ElectricGuitarmuted://29
                    case (int)MIDIInstrument.OverdrivenGuitar://30
                    case (int)MIDIInstrument.DistortionGuitar://31
                    case (int)MIDIInstrument.GuitarHarmonics://32
                        this.Dispatcher.BeginInvoke(
                          DispatcherPriority.Normal,
                            new Action(
                                delegate () {
                                    guitarControl1.Send(e.Message);
                                }
                            ));
                        break;
                    case (int)MIDIInstrument.AcousticBass://33
                    case (int)MIDIInstrument.ElectricBassfinger://34
                    case (int)MIDIInstrument.ElectricBasspick://35
                    case (int)MIDIInstrument.FretlessBass://36
                    case (int)MIDIInstrument.SlapBass1://37
                    case (int)MIDIInstrument.SlapBass2://38
                    case (int)MIDIInstrument.SynthBass1://39
                    case (int)MIDIInstrument.SynthBass2://40
                        this.Dispatcher.BeginInvoke(
                          DispatcherPriority.Normal,
                            new Action(
                                delegate () {
                                    bassControl1.Send(e.Message);
                                }
                            ));
                        break;
                    default:
                        pianoControl1.Send(e.Message);
                        break;
                }
            }
        }

        private void HandleChased(object sender, ChasedEventArgs e) {
            foreach (ChannelMessage message in e.Messages) {
                outDevice.Send(message);
            }
        }

        private void HandleSysExMessagePlayed(object sender, SysExMessageEventArgs e) {
            outDevice.Send(e.Message); //Sometimes causes an exception to be thrown because the output device is overloaded.
        }

        private void HandleStopped(object sender, StoppedEventArgs e) {
            foreach (ChannelMessage message in e.Messages) {
                outDevice.Send(message);
            }
        }

        private void HandlePlayingCompleted(object sender, EventArgs e) {
            var cArray = new string(' ', 88).ToCharArray();
            var noteList = new List<string>();
            var fretList = new List<string>();
            var count = messageList.Count;
            foreach (var message in messageList) {
                if (message.MidiChannel == 1) {
                    var noteId = message.Data1 - LowNoteID;

                    var s = string.Format("{0}: {1}", message.Ticks.ToString("000000000"), new string(cArray));

                    noteList.Add(s);
                }
            }

            //Calculate diffs
            MessageDto currentMessageNoteOn = null;
            MessageDto previousMessageNoteOn = null;
            count = messageList.Count();
            for (var i = 0; i < count; i++) {
                var message = messageList[i];
                if (message.ChannelCommand == ChannelCommand.NoteOn &&
                    message.Data2 > 0) {
                    if (currentMessageNoteOn != null)
                        previousMessageNoteOn = currentMessageNoteOn;

                    currentMessageNoteOn = messageList[i];

                    if (previousMessageNoteOn == null) {
                        currentMessageNoteOn.FretPosition = 3; //first note must fall at the middle of the fret
                    } else {
                        currentMessageNoteOn.NoteDiffToPrevious = currentMessageNoteOn.Data1 - previousMessageNoteOn.Data1;
                        previousMessageNoteOn.NoteDiffToNext = previousMessageNoteOn.Data1 - currentMessageNoteOn.Data1;
                        currentMessageNoteOn.TickDiffToPrevious = (int)(currentMessageNoteOn.Ticks - previousMessageNoteOn.Ticks);
                        previousMessageNoteOn.TickDiffToNext = (int)(previousMessageNoteOn.Ticks - currentMessageNoteOn.Ticks);

                        if (currentMessageNoteOn.Data1 == previousMessageNoteOn.Data1) {
                            currentMessageNoteOn.FretPosition = previousMessageNoteOn.FretPosition; //keep the same fret position as the previous note
                        } else if (currentMessageNoteOn.Data1 > previousMessageNoteOn.Data1) {
                            currentMessageNoteOn.FretPosition = previousMessageNoteOn.FretPosition + 1; //one fret to the right
                        } else if (currentMessageNoteOn.Data1 < previousMessageNoteOn.Data1) {
                            currentMessageNoteOn.FretPosition = previousMessageNoteOn.FretPosition - 1; //one fret to the left
                        }
                    }
                }
            }


            //var fret = "||";
            //var id = 0;

            //count = messageList.Count;
            //foreach (var message in messageList)
            //{
            //    if (message.MidiChannel == 1)
            //    {
            //        var noteId = message.Data1 - LowNoteID;

            //        if (message.ChannelCommand == ChannelCommand.NoteOn)
            //        {
            //            if (message.Data2 > 0)
            //            {
            //                string s = string.Format("{0}: {1}", message.FretPosition, new string(cArray));
            //            }
            //        }
            //    }

            //    sequence1.LoadAsync(fileName);
            //}
            if (AutoLoop && playList.Count == 1) {
                //BlackMagic.ResetSequencer(sequencer1);
                this.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(
                        delegate () {
                            sequencer1.Position = 0;
                            sequencer1.Start();
                        }
                    )
                );
                return;
            }
            timer1.Stop();
            playing = false;
            if (AutoLoop || (playIndex + 1) < playList.Count) {
                AutoStart = true;
                playIndex = (playIndex + 1) % playList.Count;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                this.DragMove();
            }
        }
        List<int> pressedKeys = new List<int>();
        private void pianoControl1_PianoKeyDown(object sender, PianoKeyEventArgs e) {
            #region Guard

            if (playing) {
                return;
            }

            #endregion

            pressedKeys.Add(e.NoteID);
            outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, e.NoteID, 127));
        }

        private void pianoControl1_PianoKeyUp(object sender, PianoKeyEventArgs e) {
            #region Guard

            if (playing) {
                return;
            }

            #endregion

            outDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, e.NoteID, 0));
        }
        private bool _playing = false;
        private bool _loaded = false;
        private bool AutoStart = false;
        private bool AutoLoop = true;
        private bool MyLoaded {
            get {
                return _loaded;
            }
            set {
                _loaded = value;
                slider1.IsEnabled = value;
            }
        }
        private BitmapImage playImage = new BitmapImage(new Uri("pack://application:,,,/WPFMidiBand;component/Images/play.png"));
        private BitmapImage pauseImage = new BitmapImage(new Uri("pack://application:,,,/WPFMidiBand;component/Images/pause.png"));
        private bool playing {
            get {
                return _playing;
            }
            set {
                _playing = value;
                this.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(
                        delegate () {
                            PlayButton.Source = value ? pauseImage : playImage;
                        }
                    )
                );
                if (value) { // fix the bug that key(s) don't release when pressed key(s) then play
                    foreach (var NoteID in pressedKeys) {
                        outDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, NoteID, 0));
                    }
                    pressedKeys.Clear();
                }
            }
        }
        private class PlayItem {
            public string FilePath { get; private set; }
            public string FileName {
                get {
                    return Path.GetFileName(FilePath);
                }
            }
            public PlayItem(string path) {
                FilePath = path;
            }
            public override string ToString() {
                return FilePath;
            }
        }
        private BindingList<PlayItem> playList = new BindingList<PlayItem>();
        private int _playIndex = -1;
        private int playIndex {
            get {
                return _playIndex;
            }
            set {
                _playIndex = value;
                if (value == -1) return;
                this.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                        new Action(
                        delegate () {
                            supressSwitch = true;
                            if (listPanel.SelectedIndex != value) listPanel.SelectedIndex = value;
                            supressSwitch = false;
                            Open(playList[value].FilePath);
                        }
                    )
                );
            }
        }
        private MidiInternalClock clock;
        private long musicLength = 0;
        private TimeSpan musicTimeSpan;
        private string currentFileName;
        private void timer1_Tick(object sender, EventArgs e) {
            if (!scrolling) {
                slider1.Value = sequencer1.Position;
            }
            var time = Ticks2Time(sequencer1.Position);
            var percent = time * 100 / musicLength;
            taskBarItemInfo.ProgressValue = percent / 100.0;
            var now = makeTimeSpan(time);
            TitleTextBlock.Text = this.Title = $"WPF Midi Band - {now}/{musicTimeSpan} - {currentFileName}";
        }
        private const long MIN_TIME_SPAN = (long)1e7; // at least 1s
        private static TimeSpan makeTimeSpan(long time) { // time in 100 nanosecond
            var remainder = time % MIN_TIME_SPAN;
            if (remainder > 0) time += MIN_TIME_SPAN - remainder;
            return new TimeSpan(time);
        }
        private bool Opening = false;
        public void Open(string loadFileName) {
            if (Opening) return;
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                    delegate () {
                        try {
                            taskBarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                            fileName = loadFileName;
                            Opening = true;
                            playing = false;
                            sequencer1.Stop();
                            timer1.Stop();
                            sequence1.Clear();
                            MyLoaded = false;
                            //toolStripProgressBar1.Visible = true;
                            currentFileName = Path.GetFileName(fileName);
                            sequence1.LoadAsync(fileName);
                            this.Cursor = Cursors.Wait;
                            //openToolStripMenuItem.Enabled = false;
                            var sbClockOpen = (Storyboard)FindResource("sbClockOpen");
                            grdClock.Visibility = Visibility.Visible;
                            sbClockOpen.Begin();
                        } catch (Exception ex) {
                            MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
                        }
                    }
                )
            );
        }
        private void LoadFile() {
            if (openMidiFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                var files = FilterFile(openMidiFileDialog.FileNames);
                if (files.Length > 0) {
                    SetFileList(files);
                }
            }
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e) {
            e.Handled = true;
            AutoStart = false;
            LoadFile();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e) {
            e.Handled = true;
            this.Close();
        }

        private void btnList_Click(object sender, RoutedEventArgs e) {
            e.Handled = true;
            if (instrumentsPanel.Visibility == Visibility.Visible) {
                instrumentsPanel.Visibility = Visibility.Collapsed;
                listPanel.Visibility = Visibility.Visible;
            } else {
                instrumentsPanel.Visibility = Visibility.Visible;
                listPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) {
            e.Handled = true;
            this.WindowState = WindowState.Minimized;
        }

        private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            //if (!dragStarted)
            //    sequencer1.Position = (int)e.NewValue;
        }

        private void slider1_DragStarted(object sender, DragStartedEventArgs e) {
            this.dragStarted = true;
        }

        private void slider1_DragCompleted(object sender, DragCompletedEventArgs e) {
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                    delegate () {
                        sequencer1.Position = (int)((Slider)sender).Value;
                    }
                )
            );
            this.dragStarted = false;
        }
        #endregion events


        private void btnPlay_Click(object sender, RoutedEventArgs e) {
            e.Handled = true;
            PlayStateChange();
        }

        private void thumbOpen_Click(object sender, EventArgs e) {
            AutoStart = false;
            LoadFile();
        }

        private void thumbPlay_Click(object sender, EventArgs e) {
            if (!playing) PlayStateChange();
        }

        private void thumbPause_Click(object sender, EventArgs e) {
            if (playing) PlayStateChange();
        }

        private void PlayStateChange() {
            if (Opening) return; // loading
            if (!MyLoaded) { // have no file selected
                AutoStart = true;
                LoadFile();
                return;
            }
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                    delegate () {
                        try {
                            if (playing) {
                                taskBarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                                playing = false;
                                sequencer1.Stop();
                                timer1.Stop();
                            } else {
                                taskBarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                                playing = true;
                                sequencer1.Continue();
                                timer1.Start();
                            }
                        } catch (Exception ex) {
                            MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
                        }
                    }
                )
            );
        }
        private List<TempoLogger> tempos = new List<TempoLogger>();
        private TempoChangeBuilder tempoChangeBuilder = new TempoChangeBuilder();
        private const long timeRatio = 10L;
        private const int DefaultTempo = 400000;
        private void CalculateTime() {
            tempos.Clear();
            tempos.Add(new TempoLogger(DefaultTempo, 0));
            foreach (var track in sequence1) {
                foreach (var evt in track.Iterator()) {
                    if (evt.MidiMessage is MetaMessage message) {
                        if (message.MetaType == MetaType.Tempo) {
                            tempoChangeBuilder.Initialize(message);
                            var tempo = tempoChangeBuilder.Tempo;
                            tempos.Add(new TempoLogger(tempo, evt.AbsoluteTicks));
                        }
                    }
                }
            }
            tempos.Sort();
            long time = 0;
            var last = tempos[0];
            foreach (var log in tempos) {
                time += timeRatio * (log.Tick - last.Tick) * last.Tempo;
                log.TimeSum = time;
                last = log;
            }
            time += timeRatio * (sequence1.GetLength() - last.Tick) * last.Tempo;
            musicLength = time / sequence1.Division;
            musicTimeSpan = makeTimeSpan(musicLength);
        }
        private long Ticks2Time(int ticks) {
            var index = tempos.BinarySearch(new TempoLogger(DefaultTempo, ticks));
            if (index < 0) {
                index = (~index) - 1;
            }
            var tempo = tempos[index];
            return (tempo.TimeSum + timeRatio * (ticks - tempo.Tick) * tempo.Tempo) / sequence1.Division;
        }
        private bool supressSwitch = false;
        private void SetFileList(string[] list) {
            if (playing) PlayStateChange();
            supressSwitch = true;
            playList.Clear();
            foreach (var file in list) {
                playList.Add(new PlayItem(file));
            }
            supressSwitch = false;
            playIndex = 0;
        }

        private void Window_DragEnter(object sender, DragEventArgs e) {
            var allow = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                var filePath = e.Data.GetData(DataFormats.FileDrop) as string[];
                allow = new List<string>(e.Data.GetData(DataFormats.FileDrop) as string[]).Find(fileFilter) != null;
            }
            e.Effects = allow ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            var filePath = e.Data.GetData(DataFormats.FileDrop) as string[];
            AutoStart = true;
            SetFileList(FilterFile(filePath));
        }
        private static Predicate<string> fileFilter = path => Path.GetExtension(path) == ".mid";
        private static string[] FilterFile(string[] files) {
            return new List<string>(files).FindAll(fileFilter).ToArray();
        }

        private void listPanel_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!supressSwitch && _loaded) {
                AutoStart = true;
                playIndex = listPanel.SelectedIndex;
            } else {
                listPanel.SelectedIndex = playIndex;
            }
        }
    }

    #region MIDIInstrument
    public enum MIDIInstrument {
        //PIANO
        AcousticGrandPiano,//1
        BrightAcousticPiano,//2
        ElectricGrandPiano,//3
        HonkytonkPiano,//4
        ElectricPiano1,//5
        ElectricPiano2,//6
        Harpsichord,//7
        Clavinet,//8


        //CHROMATICPERCUSSION
        Celesta,//9
        Glockenspiel,//10
        MusicBox,//11
        Vibraphone,//12
        Marimba,//13
        Xylophone,//14
        TubularBells,//15
        Dulcimer,//16


        //ORGAN
        DrawbarOrgan,//17
        PercussiveOrgan,//18
        RockOrgan,//19
        ChurchOrgan,//20
        ReedOrgan,//21
        Accordion,//22
        Harmonica,//23
        TangoAccordion,//24


        //GUITAR
        AcousticGuitarnylon,//25
        AcousticGuitarsteel,//26
        ElectricGuitarjazz,//27
        ElectricGuitarclean,//28
        ElectricGuitarmuted,//29
        OverdrivenGuitar,//30
        DistortionGuitar,//31
        GuitarHarmonics,//32

        //BASS
        AcousticBass,//33
        ElectricBassfinger,//34
        ElectricBasspick,//35
        FretlessBass,//36
        SlapBass1,//37
        SlapBass2,//38
        SynthBass1,//39
        SynthBass2,//40

        //STRINGS
        Violin,//41
        Viola,//42
        Cello,//43
        Contrabass,//44
        TremoloStrings,//45
        PizzicatoStrings,//46
        OrchestralHarp,//47
        Timpani,//48

        //ENSEMBLE
        StringEnsemble1,//49
        StringEnsemble2,//50
        SynthStrings1,//51
        SynthStrings2,//52
        ChoirAahs,//53
        VoiceOohs,//54
        SynthChoir,//55
        OrchestraHit,//56
        //BRASS
        Trumpet,//57
        Trombone,//58
        Tuba,//59
        MutedTrumpet,//60
        FrenchHorn,//61
        BrassSection,//62
        SynthBrass1,//63
        SynthBrass2,//64
        //REED
        SopranoSax,//65
        AltoSax,//66
        TenorSax,//67
        BaritoneSax,//68
        Oboe,//69
        EnglishHorn,//70
        Bassoon,//71
        Clarinet,//72

        //PIPE
        Piccolo,//73
        Flute,//74
        Recorder,//75
        PanFlute,//76
        BlownBottle,//77
        Shakuhachi,//78
        Whistle,//79
        Ocarina,//80
        //SYNTHLEAD
        Lead1square,//81
        Lead2sawtooth,//82
        Lead3calliope,//83
        Lead4chiff,//84
        Lead5charang,//85
        Lead6voice,//86
        Lead7fifths,//87
        Lead8basslead,//88
        //SYNTHPAD
        Pad1newage,//89
        Pad2warm,//90
        Pad3polysynth,//91
        Pad4choir,//92
        Pad5bowed,//93
        Pad6metallic,//94
        Pad7halo,//95
        Pad8sweep,//96

        //SynthEffects,
        FX1rain,//97
        FX2soundtrack,//98
        FX3crystal,//99
        x0FX4atmosphere,//100
        x1FX5brightness,//101
        x2FX6goblins,//102
        x3FX7echoes,//103
        x4FX8scifi,//104
        //ETHNIC
        Sitar,//105
        Banjo,//106
        Shamisen,//107
        Koto,//108
        Kalimba,//109
        Bagpipe,//110
        Fiddle,//111
        Shanai,//112
        //PERCUSSIVE
        TinkleBell,//113
        Agogo,//114
        SteelDrums,//115
        Woodblock,//116
        TaikoDrum,//117
        MelodicTom,//118
        SynthDrum,//119
        //SOUNDEFFECTS
        ReverseCymbal,//120
        GuitarFretNoise,//121
        BreathNoise,//122
        Seashore,//123
        BirdTweet,//124
        TelephoneRing,//125
        Helicopter,//126
        Applause,//127
        Gunshot//128
    }
    #endregion MIDIInstrument
}
